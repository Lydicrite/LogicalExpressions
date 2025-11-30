using System;
using System.Collections.Generic;
using LogicalExpressions.Compilation.Visitors;

namespace LogicalExpressions.Core.Nodes
{
    /// <summary>
    /// Представляет узел унарного оператора в логическом выражении.
    /// </summary>
    public sealed class UnaryNode : LogicNode
    {
        /// <summary>
        /// Операнд этого оператора.
        /// </summary>
        public LogicNode Operand { get; }
        /// <summary>
        /// Строковое представление операнда.
        /// </summary>
        public string Operator { get; }

        public UnaryNode(string op, LogicNode operand)
        {
            Operator = op ?? throw new ArgumentNullException(nameof(op));
            Operand = operand ?? throw new ArgumentNullException(nameof(operand));
        }

        public override void Accept(ILEVisitor visitor) => visitor.Visit(this);
        public override bool Evaluate(bool[] inputs)
        {
            switch (Operator)
            {
                case "~":
                    return !Operand.Evaluate(inputs);
                default:
                    throw new NotSupportedException($"Унарный оператор '{Operator}' не поддерживается!");
            }
        }
        public override bool Evaluate(ReadOnlySpan<bool> inputs)
        {
            switch (Operator)
            {
                case "~":
                    return !Operand.Evaluate(inputs);
                default:
                    throw new NotSupportedException($"Унарный оператор '{Operator}' не поддерживается!");
            }
        }

        public override string ToString()
        {
            // Минимизируем аллокации при конкатенации
            var operandStr = Operand.ToString();
            return string.Concat(Operator, operandStr);
        }
        public override bool Equals(object? obj)
        {
            if (obj is UnaryNode other)
                return Operator == other.Operator && Operand.Equals(other.Operand);
            return false;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Operator, Operand);
        }

        public override void CollectVariables(HashSet<string> variables) => Operand.CollectVariables(variables);
    }
}
