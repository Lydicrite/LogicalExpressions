using System;
using System.Collections.Generic;
using LogicalExpressions.Compilation.Visitors;

namespace LogicalExpressions.Core.Nodes
{
    /// <summary>
    /// Представляет узел переменной в логическом выражении.
    /// </summary>
    public sealed class VariableNode : LogicNode
    {
        /// <summary>
        /// Индекс переменной.
        /// </summary>
        public int Index { get; }
        /// <summary>
        /// Имя переменной.
        /// </summary>
        public string Name { get; }
        
        public VariableNode(string name, int index = -1)
        {
            Name = name;
            Index = index;
        }

        /// <summary>
        /// Возвращает новый узел переменной с новым индексом и прежним именем.
        /// </summary>
        /// <param name="newIndex">Индекс нового узла.</param>
        /// <returns>Новый узел переменной с новым индексом и прежним именем.</returns>
        public VariableNode WithIndex(int newIndex)
        {
            return new VariableNode(Name, newIndex);
        }

        public override void Accept(ILEVisitor visitor) => visitor.Visit(this);
        public override bool Evaluate(bool[] inputs) => inputs[Index];
        public override bool Evaluate(ReadOnlySpan<bool> inputs) => inputs[Index];
        public override void CollectVariables(HashSet<string> variables) => variables.Add(Name);

        public override string ToString() => Name;
        public override bool Equals(object? obj)
        {
            if (obj is VariableNode other)
                return Name == other.Name && Index == other.Index;
            return false;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Index);
        }
    }
}
