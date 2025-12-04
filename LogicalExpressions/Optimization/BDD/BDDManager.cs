using System;
using System.Collections.Generic;
using System.Threading;

namespace LogicalExpressions.Optimization.BDD
{
    /// <summary>
    /// Менеджер для создания и управления узлами BDD.
    /// Обеспечивает каноничность представления (Shared BDD) через таблицу уникальности.
    /// </summary>
    public class BDDManager
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
        /// Вычисляет количество узлов в поддереве (размер BDD).
        /// </summary>
        public static long GetNodeCount(BDDNode root)
        {
            if (root.IsTerminal) return 1;
            var visited = new HashSet<int>();
            var stack = new Stack<BDDNode>();
            stack.Push(root);
            long count = 0;
            
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node.IsTerminal) continue;
                if (!visited.Add(node.Id)) continue;
                
                count++;
                if (node.Low != null) stack.Push(node.Low);
                if (node.High != null) stack.Push(node.High);
            }
            return count;
        }

        /// <summary>
        /// Меняет местами переменные на уровнях <paramref name="level"/> и <paramref name="level"/> + 1.
        /// </summary>
        /// <param name="root">Корневой узел BDD.</param>
        /// <param name="level">Индекс уровня (переменной) для обмена с следующим.</param>
        /// <returns>Новый корневой узел с измененным порядком переменных.</returns>
        public BDDNode Swap(BDDNode root, int level)
        {
            var cache = new Dictionary<BDDNode, BDDNode>();
            return SwapRecursive(root, level, cache);
        }

        private BDDNode SwapRecursive(BDDNode node, int level, Dictionary<BDDNode, BDDNode> cache)
        {
            // Если узел терминальный или его индекс больше следующего уровня, он не меняется
            if (node.IsTerminal || node.VarIndex > level + 1)
            {
                return node;
            }

            if (cache.TryGetValue(node, out var cached))
            {
                return cached;
            }

            BDDNode result;

            // Если индекс узла равен level + 1, он "всплывает" на уровень level
            if (node.VarIndex == level + 1)
            {
                // Узел зависит от x_{i+1}, но не от x_i.
                // В новом порядке x_{i+1} становится на уровень i.
                // Индекс меняется на level, дети остаются те же (так как они > level + 1)
                result = GetNode(level, node.Low!, node.High!);
            }
            // Если индекс узла равен level, выполняем обмен
            else if (node.VarIndex == level)
            {
                var f0 = node.Low!;
                var f1 = node.High!;

                // Получаем кофакторы относительно переменной на уровне level + 1
                GetCofactors(f0, level + 1, out var f00, out var f01);
                GetCofactors(f1, level + 1, out var f10, out var f11);

                // Строим новые узлы
                // Новый уровень level (бывший x_{i+1})
                // Low: x_i=0 -> f00 (x_{i+1}=0), f10 (x_{i+1}=1) => Зависит от x_i (теперь level + 1)
                
                // Новая структура:
                // Level i (проверяет x_{i+1}):
                //   Low (x_{i+1}=0): Level i+1 (проверяет x_i) -> Low: f00, High: f10
                //   High (x_{i+1}=1): Level i+1 (проверяет x_i) -> Low: f01, High: f11

                var newLow = GetNode(level + 1, f00, f10);
                var newHigh = GetNode(level + 1, f01, f11);
                
                result = GetNode(level, newLow, newHigh);
            }
            // Если индекс меньше level, рекурсивно спускаемся
            else
            {
                var newLow = SwapRecursive(node.Low!, level, cache);
                var newHigh = SwapRecursive(node.High!, level, cache);
                result = GetNode(node.VarIndex, newLow, newHigh);
            }

            cache[node] = result;
            return result;
        }

        private static void GetCofactors(BDDNode node, int level, out BDDNode low, out BDDNode high)
        {
            if (node.VarIndex == level && !node.IsTerminal)
            {
                low = node.Low!;
                high = node.High!;
            }
            else
            {
                // Если узел не зависит от переменной на этом уровне (VarIndex > level или Terminal),
                // то он одинаков для обеих ветвей этой переменной.
                low = node;
                high = node;
            }
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
