using System;
using System.Collections.Generic;
using LogicalExpressions.Compilation.Visitors;

namespace LogicalExpressions.Core.Nodes
{
    /// <summary>
    /// Представляет узел логической константы (<see langword="true"/> или <see langword="false"/>).
    /// </summary>
    public sealed class ConstantNode(bool value) : LogicNode
    {
        /// <summary>
        /// Публичное значение константы для безопасного доступа без входов.
        /// </summary>
        public bool Value { get; } = value;

        public override void Accept(ILEVisitor visitor) => visitor.Visit(this);
        public override bool Evaluate(bool[] inputs) => Value;
        public override bool Evaluate(ReadOnlySpan<bool> inputs) => Value;
        public override void CollectVariables(HashSet<string> variables) { }
        
        public override string ToString() => Value ? "1" : "0";
        public override bool Equals(object? obj)
        {
            return obj is ConstantNode other && Value == other.Value;
        }
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
