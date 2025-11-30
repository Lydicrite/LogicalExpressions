using System;
using System.Collections.Generic;
using System.Linq;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Utils;

namespace LogicalExpressions.Compilation.Visitors
{
    /// <summary>
    /// Построитель канонической строковой формы выражения
    /// с предсказуемым порядком и скобками.
    /// </summary>
    public sealed class CanonicalStringVisitor
    {
        // Набор коммутативных операторов, для которых выполняется сортировка/флаттенинг
        private static readonly HashSet<string> Commutative = new(StringComparer.OrdinalIgnoreCase)
        {
            "&", "|", "^", "<=>", "!&", "!|"
        };

        public static string Build(LogicNode node)
        {
            var sb = new PooledValueStringBuilder(256);
            AppendNode(node, ref sb);
            return sb.ToStringAndDispose();
        }
        private static void FlattenSameOperator(LogicNode node, string op, List<LogicNode> acc)
        {
            if (node is BinaryNode bn && bn.Operator == op)
            {
                FlattenSameOperator(bn.Left, op, acc);
                FlattenSameOperator(bn.Right, op, acc);
            }
            else
            {
                acc.Add(node);
            }
        }

        private static bool NeedsParentheses(LogicNode node) => node is UnaryNode || node is BinaryNode;

        private static void AppendNode(LogicNode node, ref PooledValueStringBuilder sb)
        {
            switch (node)
            {
                case ConstantNode c:
                    sb.Append(c.Value ? "1" : "0");
                    return;
                case VariableNode v:
                    sb.Append(v.Name);
                    return;
                case UnaryNode u:
                    // Для сложных операндов добавляем скобки
                    if (NeedsParentheses(u.Operand))
                    {
                        sb.Append("~(");
                        AppendNode(u.Operand, ref sb);
                        sb.Append(')');
                    }
                    else
                    {
                        sb.Append('~');
                        AppendNode(u.Operand, ref sb);
                    }
                    return;
                case BinaryNode b:
                    if (Commutative.Contains(b.Operator))
                    {
                        var items = new List<LogicNode>();
                        FlattenSameOperator(b, b.Operator, items);
                        var parts = new List<string>(items.Count);
                        foreach (var it in items)
                        {
                            var partBuilder = new PooledValueStringBuilder(64);
                            AppendNode(it, ref partBuilder);
                            parts.Add(partBuilder.ToStringAndDispose());
                        }
                        parts.Sort(StringComparer.Ordinal);
                        sb.Append('(');
                        for (int i = 0; i < parts.Count; i++)
                        {
                            if (i > 0)
                            {
                                sb.Append(' ');
                                sb.Append(b.Operator);
                                sb.Append(' ');
                            }
                            sb.Append(parts[i]);
                        }
                        sb.Append(')');
                    }
                    else
                    {
                        sb.Append('(');
                        AppendNode(b.Left, ref sb);
                        sb.Append(' ');
                        sb.Append(b.Operator);
                        sb.Append(' ');
                        AppendNode(b.Right, ref sb);
                        sb.Append(')');
                    }
                    return;
                default:
                    // Should not happen if we cover all types
                    sb.Append(node.ToString());
                    return;
            }
        }
    }
}
