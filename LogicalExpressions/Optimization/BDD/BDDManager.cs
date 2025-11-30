using System;
using System.Collections.Generic;
using System.Threading;

namespace LogicalExpressions.Optimization.Bdd
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

        // CA1805: Не инициализировать явно значением по умолчанию
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

    /// <summary>
    /// Менеджер для создания и управления узлами BDD.
    /// Обеспечивает каноничность представления (Shared BDD) через таблицу уникальности.
    /// </summary>
    public class BddManager
    {
        // Таблица уникальных узлов: (varIndex, low, high) -> BDDNode
        private readonly Dictionary<(int varIndex, BDDNode low, BDDNode high), BDDNode> _uniqueTable = new();
        
        // Таблица вычисленных операций: (op, id1, id2) -> BDDNode
        private readonly Dictionary<(string op, int id1, int id2), BDDNode> _computedTable = new();

        /// <summary>
        /// Глобальный узел "Ложь".
        /// </summary>
        public static BDDNode Zero => BDDNode.Zero;

        /// <summary>
        /// Глобальный узел "Истина".
        /// </summary>
        public static BDDNode One => BDDNode.One;

        /// <summary>
        /// Создает узел BDD для одной переменной.
        /// </summary>
        /// <param name="varIndex">Индекс переменной.</param>
        public BDDNode CreateVariable(int varIndex)
        {
            return GetNode(varIndex, Zero, One);
        }

        /// <summary>
        /// Получает или создает канонический узел BDD.
        /// </summary>
        public BDDNode GetNode(int varIndex, BDDNode low, BDDNode high)
        {
            if (low == high) return low;

            var key = (varIndex, low, high);
            if (_uniqueTable.TryGetValue(key, out var node))
            {
                return node;
            }

            node = BDDNode.Create(varIndex, low, high);
            _uniqueTable[key] = node;
            return node;
        }

        /// <summary>
        /// Логическое НЕ для BDD.
        /// </summary>
        public BDDNode Not(BDDNode u)
        {
            return Apply("not", u, null, (x, y) => !x);
        }

        /// <summary>
        /// Логическое И для BDD.
        /// </summary>
        public BDDNode And(BDDNode u, BDDNode v)
        {
            return Apply("and", u, v, (x, y) => x && y);
        }

        /// <summary>
        /// Логическое ИЛИ для BDD.
        /// </summary>
        public BDDNode Or(BDDNode u, BDDNode v)
        {
            return Apply("or", u, v, (x, y) => x || y);
        }
        
        /// <summary>
        /// Логическое Исключающее ИЛИ (XOR) для BDD.
        /// </summary>
        public BDDNode Xor(BDDNode u, BDDNode v)
        {
            return Apply("xor", u, v, (x, y) => x ^ y);
        }
        
        /// <summary>
        /// Логическая Импликация для BDD.
        /// </summary>
        public BDDNode Imply(BDDNode u, BDDNode v)
        {
             return Apply("imply", u, v, (x, y) => !x || y);
        }

        // Универсальный метод Apply для выполнения логических операций над BDD
        private BDDNode Apply(string op, BDDNode u, BDDNode? v, Func<bool, bool, bool> terminalOp)
        {
            if (v == null) // Унарная операция
            {
                if (u.IsTerminal) return terminalOp(u.Value, false) ? One : Zero;
                
                var key = (op, u.Id, 0);
                if (_computedTable.TryGetValue(key, out var cached)) return cached;

                var res = GetNode(u.VarIndex, Apply(op, u.Low!, null, terminalOp), Apply(op, u.High!, null, terminalOp));
                _computedTable[key] = res;
                return res;
            }

            // Бинарная операция
            if (u.IsTerminal && v.IsTerminal)
            {
                return terminalOp(u.Value, v.Value) ? One : Zero;
            }

            var keyBin = (op, u.Id, v.Id);
            if (_computedTable.TryGetValue(keyBin, out var cachedBin)) return cachedBin;

            int varIndex;
            BDDNode uLow, uHigh, vLow, vHigh;

            if (u.IsTerminal)
            {
                varIndex = v.VarIndex;
                uLow = uHigh = u;
                vLow = v.Low!;
                vHigh = v.High!;
            }
            else if (v.IsTerminal)
            {
                varIndex = u.VarIndex;
                uLow = u.Low!;
                uHigh = u.High!;
                vLow = vHigh = v;
            }
            else if (u.VarIndex == v.VarIndex)
            {
                varIndex = u.VarIndex;
                uLow = u.Low!;
                uHigh = u.High!;
                vLow = v.Low!;
                vHigh = v.High!;
            }
            else if (u.VarIndex < v.VarIndex)
            {
                varIndex = u.VarIndex;
                uLow = u.Low!;
                uHigh = u.High!;
                vLow = vHigh = v;
            }
            else // u.VarIndex > v.VarIndex
            {
                varIndex = v.VarIndex;
                uLow = uHigh = u;
                vLow = v.Low!;
                vHigh = v.High!;
            }

            var resBin = GetNode(varIndex, Apply(op, uLow, vLow, terminalOp), Apply(op, uHigh, vHigh, terminalOp));
            _computedTable[keyBin] = resBin;
            return resBin;
        }
    }
}
