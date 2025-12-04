using System;
using System.Collections.Generic;
using System.Linq;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Optimization.VariableOrdering.Strategies.StaticStrategies
{
    /// <summary>
    /// Стратегия, упорядочивающая переменные по частоте их появления в выражении (от самых частых к редким).
    /// </summary>
    public class FrequencyOrderingStrategy : IVariableOrderingStrategy
    {
        public IEnumerable<string> OptimizeOrder(LogicNode root, IReadOnlyList<string> currentVariables)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var v in currentVariables) counts[v] = 0;

            void Visit(LogicNode node)
            {
                if (node is VariableNode vn)
                {
                    if (counts.TryGetValue(vn.Name, out int val))
                        counts[vn.Name] = val + 1;
                }
                else if (node is UnaryNode un)
                {
                    Visit(un.Operand);
                }
                else if (node is BinaryNode bn)
                {
                    Visit(bn.Left);
                    Visit(bn.Right);
                }
            }
            Visit(root);

            return currentVariables
                .OrderByDescending(v => counts[v])
                .ThenBy(v => v, StringComparer.Ordinal);
        }
    }
}
