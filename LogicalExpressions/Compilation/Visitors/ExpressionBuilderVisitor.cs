using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Utils;

namespace LogicalExpressions.Compilation.Visitors
{
    /// <summary>
    /// Посетитель, строящий абстрактное синтаксическое дерево, используя классы <see cref="Expression"/> и <seealso cref="ParameterExpression"/>.
    /// Сопоставление операторов делегируется <see cref="LogicalExpressions.Utils.ExpressionOperatorHelper"/>,
    /// что исключает дублирование логики с узлами AST.
    /// </summary>
    public sealed class ExpressionBuilderVisitor : BaseVisitor
    {
        /// <summary>
        /// Стэк, содержащий выражения и используемый для их обработки.
        /// </summary>
        private readonly Stack<Expression> _expressionStack = new();
        /// <summary>
        /// Объект, содержащий входные параметры выражения.
        /// </summary>
        private readonly ParameterExpression _param;

        /// <summary>
        /// Создаёт новый посетитель, строящий абстрактное синтаксическое дерево.
        /// </summary>
        /// <param name="param">Параметры для дерева выражеиня.</param>
        public ExpressionBuilderVisitor(ParameterExpression param) => _param = param;

        public override void VisitConstant(ConstantNode node)
        {
            _expressionStack.Push(Expression.Constant(node.Value));
        }

        public override void VisitVariable(VariableNode node)
        {
            _expressionStack.Push(Expression.ArrayIndex(_param, Expression.Constant(node.Index)));
        }

        public override void VisitUnary(UnaryNode node)
        {
            node.Operand.Accept(this);
            var expr = _expressionStack.Pop();
            _expressionStack.Push(ExpressionOperatorHelper.BuildUnary(node.Operator, expr));
        }

        public override void VisitBinary(BinaryNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            var right = _expressionStack.Pop();
            var left = _expressionStack.Pop();

            _expressionStack.Push(ExpressionOperatorHelper.BuildBinary(node.Operator, left, right));
        }

        public Expression GetResult() => _expressionStack.Pop();
    }
}
