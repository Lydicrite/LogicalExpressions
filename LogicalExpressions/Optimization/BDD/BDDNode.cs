using System;
using System.Collections.Generic;
using System.Threading;

namespace LogicalExpressions.Optimization.BDD
{
    /// <summary>
    /// Узел бинарной диаграммы решений (BDD).
    /// Является неизменяемым объектом, представляющим булеву функцию.
    /// </summary>
    public class BDDNode : IEquatable<BDDNode>
    {
        /// <summary>
        /// Индекс переменной в заданном порядке.
        /// Значение -1 указывает на терминальный узел (константу).
        /// </summary>
        public int VarIndex { get; }

        /// <summary>
        /// Дочерний узел для случая, когда переменная равна false.
        /// </summary>
        public BDDNode? Low { get; }

        /// <summary>
        /// Дочерний узел для случая, когда переменная равна true.
        /// </summary>
        public BDDNode? High { get; }

        /// <summary>
        /// Значение терминального узла (истина или ложь).
        /// Актуально только если <see cref="IsTerminal"/> равно true.
        /// </summary>
        public bool Value { get; }

        /// <summary>
        /// Уникальный идентификатор узла.
        /// </summary>
        public int Id { get; }

        private static int _idCounter;

        private BDDNode(int varIndex, BDDNode? low, BDDNode? high, bool value)
        {
            VarIndex = varIndex;
            Low = low;
            High = high;
            Value = value;
            Id = Interlocked.Increment(ref _idCounter);
        }

        /// <summary>
        /// Терминальный узел "Истина" (1).
        /// </summary>
        public static readonly BDDNode One = new BDDNode(-1, null, null, true);

        /// <summary>
        /// Терминальный узел "Ложь" (0).
        /// </summary>
        public static readonly BDDNode Zero = new BDDNode(-1, null, null, false);

        /// <summary>
        /// Создает новый нетерминальный узел BDD.
        /// Выполняет базовую редукцию: если low == high, возвращает low.
        /// </summary>
        public static BDDNode Create(int varIndex, BDDNode low, BDDNode high)
        {
            if (low == high) return low;
            return new BDDNode(varIndex, low, high, false);
        }

        /// <summary>
        /// Возвращает true, если узел является терминальным (константой).
        /// </summary>
        public bool IsTerminal => VarIndex == -1;

        public override bool Equals(object? obj) => Equals(obj as BDDNode);
        
        public bool Equals(BDDNode? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (IsTerminal && other.IsTerminal) return Value == other.Value;
            return VarIndex == other.VarIndex && Low == other.Low && High == other.High;
        }

        public override int GetHashCode()
        {
            if (IsTerminal) return Value.GetHashCode();
            return HashCode.Combine(VarIndex, Low, High);
        }
    }
}
