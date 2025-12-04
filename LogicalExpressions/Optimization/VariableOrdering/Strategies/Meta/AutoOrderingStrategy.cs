using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Optimization.BDD;
using LogicalExpressions.Compilation.Visitors;
using LogicalExpressions.Optimization.VariableOrdering.Strategies.StaticStrategies;
using LogicalExpressions.Optimization.VariableOrdering.Strategies.Dynamic;

namespace LogicalExpressions.Optimization.VariableOrdering.Strategies.Meta
{
    /// <summary>
    /// Автоматическая стратегия оптимизации.
    /// Анализирует сложность выражения и выбирает оптимальную комбинацию стратегий.
    /// Использует параллельное выполнение для сравнения кандидатов.
    /// </summary>
    public class AutoOrderingStrategy : IVariableOrderingStrategy
    {
        public IEnumerable<string> OptimizeOrder(LogicNode root, IReadOnlyList<string> currentVariables)
        {
            int varCount = currentVariables.Count;

            // 1. Параллельный выбор лучшей стартовой стратегии.
            // Запускаем Alphabetical и Frequency (и Random для разнообразия) параллельно.
            // Если переменных немного, это дешево.
            // Если переменных много, построение BDD может быть долгим, но на многоядерных CPU мы выиграем время.
            
            List<string> bestStartOrder;

            // Лимит на использование параллелизма, чтобы не забить память при огромных BDD
            if (varCount <= 40)
            {
                bestStartOrder = PickBestStartOrderParallel(root, currentVariables);
            }
            else
            {
                // Для больших выражений частотная стратегия обычно лучше и быстрее,
                // чем строить несколько огромных BDD параллельно.
                bestStartOrder = new FrequencyOrderingStrategy().OptimizeOrder(root, currentVariables).ToList();
            }

            // 2. Динамическая оптимизация (Sifting).
            // Sifting (особенно с Swap-оптимизацией) достаточно быстр для N < 50-100.
            if (varCount <= 60)
            {
                return new SiftingStrategy().OptimizeOrder(root, bestStartOrder);
            }
            
            return bestStartOrder;
        }

        private static List<string> PickBestStartOrderParallel(LogicNode root, IReadOnlyList<string> vars)
        {
            var candidates = new IVariableOrderingStrategy[]
            {
                new AlphabeticalOrderingStrategy(),
                new FrequencyOrderingStrategy(),
                new RandomOrderingStrategy(42) // Детерминированный сид для воспроизводимости
            };

            // Запускаем стратегии и оценку параллельно
            var tasks = candidates.Select(strategy => Task.Run(() => 
            {
                var order = strategy.OptimizeOrder(root, vars).ToList();
                long size = MeasureBddSize(root, order);
                return (Order: order, Size: size);
            })).ToArray();

            Task.WaitAll(tasks);

            // Выбираем лучший результат
            var best = tasks
                .Select(t => t.Result)
                .OrderBy(x => x.Size)
                .First();

            return best.Order;
        }

        private static long MeasureBddSize(LogicNode root, List<string> varOrder)
        {
            // Важно: Создаем свой BDDManager для каждого потока, так как он не потокобезопасен (кеши).
            var manager = new BDDManager();
            var varMap = varOrder.Select((v, idx) => (v, idx)).ToDictionary(x => x.v, x => x.idx);
            var visitor = new BDDBuilderVisitor(manager, varMap);
            root.Accept(visitor);
            return BDDManager.GetNodeCount(visitor.Result);
        }
    }
}
