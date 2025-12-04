using System;
using System.Collections.Generic;
using System.Linq;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Optimization.BDD;
using LogicalExpressions.Compilation.Visitors;

namespace LogicalExpressions.Optimization.VariableOrdering.Strategies.Dynamic
{
    /// <summary>
    /// Стратегия просеивания (Sifting).
    /// Пытается переместить каждую переменную во все возможные позиции,
    /// выбирая ту, которая минимизирует размер BDD.
    /// </summary>
    public class SiftingStrategy : IVariableOrderingStrategy
    {
        public IEnumerable<string> OptimizeOrder(LogicNode root, IReadOnlyList<string> currentVariables)
        {
            var vars = currentVariables.ToList();
            int n = vars.Count;
            if (n <= 1) return vars;

            // 1. Строим начальный BDD с текущим порядком
            var manager = new BDDManager();
            var initialMap = vars.Select((v, idx) => (v, idx)).ToDictionary(x => x.v, x => x.idx);
            var visitor = new BDDBuilderVisitor(manager, initialMap);
            root.Accept(visitor);
            var bddRoot = visitor.Result;

            // Начальный размер
            long currentMinSize = BDDManager.GetNodeCount(bddRoot);
            var currentBestOrder = new List<string>(vars);

            // Sifting
            bool improved = true;
            while (improved)
            {
                improved = false;
                // Проходим по каждой переменной в текущем порядке
                for (int i = 0; i < n; i++)
                {
                    // Запоминаем состояние перед сдвигом переменной i
                    var bestPosForVar = i;
                    var minSizeForVar = currentMinSize;
                    var originalRoot = bddRoot; // Текущий корень перед экспериментами с переменной i
                    
                    // Двигаем переменную i вниз до конца
                    var tempRoot = originalRoot;
                    for (int pos = i; pos < n - 1; pos++)
                    {
                        // Swap(pos, pos+1)
                        tempRoot = manager.Swap(tempRoot, pos);
                        
                        long size = BDDManager.GetNodeCount(tempRoot);
                        if (size < minSizeForVar)
                        {
                            minSizeForVar = size;
                            bestPosForVar = pos + 1;
                        }
                    }

                    // Теперь tempRoot соответствует порядку, где переменная i (изначально) ушла в самый низ.
                    // Двигаем её вверх с самого низа до самого верха.
                    // Текущая позиция переменной - (n-1).
                    
                    for (int pos = n - 2; pos >= 0; pos--)
                    {
                        // Swap(pos, pos+1) поднимает переменную с pos+1 на pos
                        tempRoot = manager.Swap(tempRoot, pos);
                        
                        long size = BDDManager.GetNodeCount(tempRoot);
                        if (size < minSizeForVar)
                        {
                            minSizeForVar = size;
                            bestPosForVar = pos;
                        }
                    }

                    // Теперь tempRoot соответствует порядку, где переменная на позиции 0.
                    // Нужно вернуть её на bestPosForVar.
                    // Сейчас она на 0.
                    for (int pos = 0; pos < bestPosForVar; pos++)
                    {
                        tempRoot = manager.Swap(tempRoot, pos);
                    }

                    // Если нашли улучшение
                    if (minSizeForVar < currentMinSize)
                    {
                        currentMinSize = minSizeForVar;
                        bddRoot = tempRoot;
                        
                        // Обновляем список переменных currentBestOrder
                        // Мы знаем, что переменная, которая была на позиции i, теперь на bestPosForVar.
                        // Но мы делали серию свопов.
                        // Проще всего воспроизвести изменения в списке.
                        var varToMove = currentBestOrder[i];
                        currentBestOrder.RemoveAt(i);
                        currentBestOrder.Insert(bestPosForVar, varToMove);
                        
                        improved = true;
                        // В классическом Sifting мы продолжаем с следующей переменной,
                        // но так как порядок изменился, индексы i могут сместиться.
                        // Упрощенно: если улучшили, фиксируем и идем дальше.
                    }
                    else
                    {
                        // Возвращаем BDD в исходное состояние (до начала движения этой переменной),
                        // так как мы могли найти локальный минимум, который хуже глобального, 
                        // или просто не улучшили.
                        // Хотя стоп, мы уже вернули BDD в состояние bestPosForVar.
                        // Если bestPosForVar == i, то bddRoot (tempRoot) эквивалентен originalRoot (структурно может отличаться ID, но изоморфен).
                        // Мы просто обновляем bddRoot на tempRoot (так как это корректное представление для текущей позиции).
                        bddRoot = tempRoot;
                    }
                }
            }

            return currentBestOrder;
        }
    }
}
