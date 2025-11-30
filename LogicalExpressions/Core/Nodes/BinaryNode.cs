using System;
using System.Collections.Generic;
using LogicalExpressions.Compilation.Visitors;

namespace LogicalExpressions.Core.Nodes
{
    public sealed class BinaryNode : LogicNode
    {
        /// <summary>
        /// Левый операнд этого оператора.
        /// </summary>
        public LogicNode Left { get; }
        /// <summary>
        /// Правый операнд этого оператора.
        /// </summary>
        public LogicNode Right { get; }
        /// <summary>
        /// Строковое представление операнда.
        /// </summary>
        public string Operator { get; }

        public BinaryNode(string op, LogicNode left, LogicNode right)
        {
            Operator = op ?? throw new ArgumentNullException(nameof(op));
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        public override void Accept(ILEVisitor visitor) => visitor.Visit(this);
        public override bool Evaluate(bool[] inputs)
        {
            switch (Operator)
            {
                case "&":
                    return Left.Evaluate(inputs) && Right.Evaluate(inputs);
                case "|":
                    return Left.Evaluate(inputs) || Right.Evaluate(inputs);
                case "^":
                    return Left.Evaluate(inputs) ^ Right.Evaluate(inputs);
                case "=>":
                    return !Left.Evaluate(inputs) || Right.Evaluate(inputs);
                case "<=>":
                    return Left.Evaluate(inputs) == Right.Evaluate(inputs);
                case "!&":
                    return !(Left.Evaluate(inputs) && Right.Evaluate(inputs));
                case "!|":
                    return !(Left.Evaluate(inputs) || Right.Evaluate(inputs));
                default:
                    throw new NotSupportedException($"Бинарный оператор '{Operator}' не поддерживается!");
            }
        }
        public override bool Evaluate(ReadOnlySpan<bool> inputs)
        {
            switch (Operator)
            {
                case "&":
                    return Left.Evaluate(inputs) && Right.Evaluate(inputs);
                case "|":
                    return Left.Evaluate(inputs) || Right.Evaluate(inputs);
                case "^":
                    return Left.Evaluate(inputs) ^ Right.Evaluate(inputs);
                case "=>":
                    return !Left.Evaluate(inputs) || Right.Evaluate(inputs);
                case "<=>":
                    return Left.Evaluate(inputs) == Right.Evaluate(inputs);
                case "!&":
                    return !(Left.Evaluate(inputs) && Right.Evaluate(inputs));
                case "!|":
                    return !(Left.Evaluate(inputs) || Right.Evaluate(inputs));
                default:
                    throw new NotSupportedException($"Бинарный оператор '{Operator}' не поддерживается!");
            }
        }
        public override void CollectVariables(HashSet<string> variables)
        {
            Left.CollectVariables(variables);
            Right.CollectVariables(variables);
        }

        public override string ToString()
        {
            var leftStr = Left.ToString();
            var rightStr = Right.ToString();
            return string.Concat("(", leftStr, " ", Operator, " ", rightStr, ")");
        }
        public override bool Equals(object? obj)
        {
            if (obj is BinaryNode other)
                return Operator == other.Operator &&
                       Left.Equals(other.Left) &&
                       Right.Equals(other.Right);
            return false;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Operator, Left, Right);
        }
    }
}
