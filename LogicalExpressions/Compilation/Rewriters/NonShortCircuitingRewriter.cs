using System;
using System.Linq.Expressions;

namespace LogicalExpressions.Compilation.Rewriters
{
    /// <summary>
    /// Переписывает дерево выражений, заменяя <see cref="ExpressionType.AndAlso"/> на
    /// <see cref="ExpressionType.And"/> и <see cref="ExpressionType.OrElse"/> на
    /// <see cref="ExpressionType.Or"/> для строгого булева вычисления без короткого замыкания.
    /// </summary>
    internal sealed class NonShortCircuitingRewriter : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            return node.NodeType switch
            {
                ExpressionType.AndAlso => Expression.MakeBinary(ExpressionType.And, left, right),
                ExpressionType.OrElse => Expression.MakeBinary(ExpressionType.Or, left, right),
                _ => node.Update(left, node.Conversion, right)
            };
        }
    }
}
