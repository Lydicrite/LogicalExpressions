using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogicalExpressions.Core;
using LogicalExpressions.Parsing;
using LogicalExpressions.Optimization.BDD;
using LogicalExpressions.Optimization.VariableOrdering;
using LogicalExpressions.Optimization.VariableOrdering.Strategies.StaticStrategies;
using LogicalExpressions.Optimization.VariableOrdering.Strategies.Dynamic;
using LogicalExpressions.Compilation.Visitors;

namespace MSTests
{
    [TestClass]
    public sealed class VariableOrderingBenchmarks
    {
        private static readonly Dictionary<string, IVariableOrderingStrategy> _strategies = new()
        {
            ["Alpha"] = VariableOrderOptimizer.Default,
            ["Freq"] = VariableOrderOptimizer.Frequency,
            ["Random"] = VariableOrderOptimizer.Random,
            ["Sifting"] = VariableOrderOptimizer.Sifting,
            ["Auto"] = VariableOrderOptimizer.Auto
        };

        [DataTestMethod]
        [Timeout(60000)]
        [Description("Бенчмарк методов оптимизации порядка переменных BDD")]
        // 1. Простое выражение, разные стратегии
        [DataRow("(A | B) & (C | D) & (E | F)", "Alpha")]
        [DataRow("(A | B) & (C | D) & (E | F)", "Sifting")]

        // 2. Выражение, чувствительное к порядку (например, (x1 + y1) (x2 + y2)... vs (x1 + x2 ...) (y1 + y2...))
        [DataRow("(A & B) | (C & D) | (E & F) | (G & H)", "Freq,Sifting")] 
        [DataRow("(A & B) | (C & D) | (E & F) | (G & H)", "Alpha,Sifting")]
        [DataRow("(A & B) | (C & D) | (E & F) | (G & H)", "Random,Freq,Sifting")]
        [DataRow("(A & B) | (C & D) | (E & F) | (G & H)", "Random,Alpha,Sifting")]

        // 3. Случайное сложное выражение
        [DataRow("((A ^ B) => (C & D)) <=> ((E | F) & !(G ^ H))", "Random,Sifting")]
        [DataRow("((A ^ B) => (C & D)) <=> ((E | F) & !(G ^ H))", "Auto")]

        public void Benchmark_VariableOrdering(string exprStr, string strategyNames)
        {
            Console.WriteLine($"\n=== Бенчмарк: {strategyNames} ===");
            Console.WriteLine($"Выражение: {exprStr}");

            // Парсинг
            var root = ExpressionParser.Parse(exprStr);
            var expr = new LogicalExpression(root);
            var initialVars = expr.Variables;

            // 1. Замер неоптимизированного BDD (алфавитный порядок по умолчанию)
            long initialSize = MeasureBddSize(root, initialVars);
            Console.WriteLine($"[Initial] Nodes: {initialSize}, Order: {string.Join(", ", initialVars)}");

            // 2. Применение стратегий по очереди
            var currentVars = initialVars;
            var names = strategyNames.Split(',', StringSplitOptions.RemoveEmptyEntries);
            
            long totalTimeMs = 0;

            foreach (var name in names)
            {
                var strategyName = name.Trim();
                if (!_strategies.TryGetValue(strategyName, out var strategy))
                {
                    Assert.Inconclusive($"Стратегия '{strategyName}' не найдена.");
                    return;
                }

                var sw = Stopwatch.StartNew();
                var newOrder = strategy.OptimizeOrder(root, currentVars).ToList();
                sw.Stop();
                
                long size = MeasureBddSize(root, newOrder);
                totalTimeMs += sw.ElapsedMilliseconds;
                currentVars = newOrder;

                Console.WriteLine($"[{strategyName}] Time: {sw.ElapsedMilliseconds} ms, Nodes: {size}, Order: {string.Join(", ", newOrder)}");
            }

            // 3. Итоговый результат
            long finalSize = MeasureBddSize(root, currentVars);
            Console.WriteLine($"\n[Final] Total Time: {totalTimeMs} ms");
            Console.WriteLine($"[Final] Nodes Reduction: {initialSize} -> {finalSize} ({100.0 * (initialSize - finalSize) / initialSize:F1}% reduction)");

            // Проверка корректности (семантика не должна меняться)
            // Строим BDD с новым порядком и проверяем эквивалентность с исходным
            // (в данном случае просто убеждаемся, что оптимизатор вернул валидный набор переменных)
            Assert.AreEqual(initialVars.Count, currentVars.Count);
            Assert.IsTrue(new HashSet<string>(initialVars).SetEquals(currentVars), "Набор переменных изменился после оптимизации!");
        }

        private static long MeasureBddSize(LogicalExpressions.Core.Nodes.LogicNode root, IReadOnlyList<string> varOrder)
        {
            var manager = new BDDManager();
            var varMap = varOrder.Select((v, idx) => (v, idx)).ToDictionary(x => x.v, x => x.idx);
            var visitor = new BDDBuilderVisitor(manager, varMap);
            root.Accept(visitor);
            return BDDManager.GetNodeCount(visitor.Result);
        }
    }
}
