using System;
using System.Collections.Generic;
using System.Linq;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Optimization.VariableOrdering.Strategies.StaticStrategies
{
    /// <summary>
    /// Стратегия, упорядочивающая переменные в алфавитном порядке (по умолчанию).
    /// </summary>
    public class AlphabeticalOrderingStrategy : IVariableOrderingStrategy
    {
        public IEnumerable<string> OptimizeOrder(LogicNode root, IReadOnlyList<string> currentVariables)
        {
            return currentVariables.OrderBy(v => v, StringComparer.Ordinal);
        }
    }
}
