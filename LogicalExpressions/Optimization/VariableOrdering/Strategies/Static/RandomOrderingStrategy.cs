using System;
using System.Collections.Generic;
using System.Linq;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Optimization.VariableOrdering.Strategies.StaticStrategies
{
    /// <summary>
    /// Стратегия, упорядочивающая переменные случайным образом.
    /// </summary>
    public class RandomOrderingStrategy : IVariableOrderingStrategy
    {
        private readonly Random _random;

        public RandomOrderingStrategy(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public IEnumerable<string> OptimizeOrder(LogicNode root, IReadOnlyList<string> currentVariables)
        {
            return currentVariables.OrderBy(_ => _random.Next());
        }
    }
}
