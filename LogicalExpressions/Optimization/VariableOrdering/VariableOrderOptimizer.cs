using System;
using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Optimization.VariableOrdering.Strategies.StaticStrategies;
using LogicalExpressions.Optimization.VariableOrdering.Strategies.Dynamic;
using LogicalExpressions.Optimization.VariableOrdering.Strategies.Meta;

namespace LogicalExpressions.Optimization.VariableOrdering
{
    /// <summary>
    /// Фасад для оптимизации порядка переменных.
    /// </summary>
    public static class VariableOrderOptimizer
    {
        /// <summary>
        /// Стратегия по умолчанию (алфавитный порядок).
        /// </summary>
        public static IVariableOrderingStrategy Default { get; } = new AlphabeticalOrderingStrategy();

        /// <summary>
        /// Автоматическая стратегия (выбирает лучшую комбинацию на основе анализа).
        /// </summary>
        public static IVariableOrderingStrategy Auto { get; } = new AutoOrderingStrategy();

        /// <summary>
        /// Стратегия частотного анализа.
        /// </summary>
        public static IVariableOrderingStrategy Frequency { get; } = new FrequencyOrderingStrategy();

        /// <summary>
        /// Стратегия случайного порядка.
        /// </summary>
        public static IVariableOrderingStrategy Random { get; } = new RandomOrderingStrategy();

        /// <summary>
        /// Стратегия просеивания (Sifting) - медленная, но эффективная.
        /// </summary>
        public static IVariableOrderingStrategy Sifting { get; } = new SiftingStrategy();

        /// <summary>
        /// Комбинирует несколько стратегий последовательно.
        /// </summary>
        public static IVariableOrderingStrategy Composite(params IVariableOrderingStrategy[] strategies)
        {
            return new CompositeStrategy(strategies);
        }

        private class CompositeStrategy : IVariableOrderingStrategy
        {
            private readonly IVariableOrderingStrategy[] _strategies;

            public CompositeStrategy(IVariableOrderingStrategy[] strategies)
            {
                _strategies = strategies;
            }

            public IEnumerable<string> OptimizeOrder(LogicNode root, IReadOnlyList<string> currentVariables)
            {
                var vars = currentVariables;
                foreach (var strategy in _strategies)
                {
                    vars = strategy.OptimizeOrder(root, vars).ToList();
                }
                return vars;
            }
        }
    }
}
