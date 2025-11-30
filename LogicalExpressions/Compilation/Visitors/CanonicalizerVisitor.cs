using System;
using System.Collections.Generic;
using System.Linq;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Compilation.Visitors
{
    /// <summary>
    /// Канонизатор АСТ: для коммутативных операторов (&amp;, |, ^, &lt;=&gt;)
    /// выполняет флаттенинг, стабильную сортировку операндов и удаление дубликатов.
    /// Для ^ и &lt;=&gt; дубликаты устраняются попарно (по модулю 2),
    /// что соответствует свойствам этих операций.
    /// </summary>
    public sealed class CanonicalizerVisitor : BaseVisitor
    {
        private readonly Stack<LogicNode> _stack = new();

        private static readonly HashSet<string> Commutative = new(StringComparer.Ordinal)
        {
            "&", "|", "^", "<=>"
        };

        /// <summary>
        /// Построить канонизированное дерево для заданного узла.
        /// </summary>
        public LogicNode Canonicalize(LogicNode node)
        {
            node.Accept(this);
            return _stack.Pop();
        }

        public override void VisitConstant(ConstantNode node)
        {
            _stack.Push(new ConstantNode(node.Value));
        }

        public override void VisitVariable(VariableNode node)
        {
            // Сохраняем индекс переменной, чтобы не нарушать индексирование при компиляции/оценке
            _stack.Push(new VariableNode(node.Name, node.Index));
        }

        public override void VisitUnary(UnaryNode node)
        {
            node.Operand.Accept(this);
            var op = _stack.Pop();
            _stack.Push(new UnaryNode(node.Operator, op));
        }

        public override void VisitBinary(BinaryNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            var right = _stack.Pop();
            var left = _stack.Pop();

            if (!Commutative.Contains(node.Operator))
            {
                _stack.Push(new BinaryNode(node.Operator, left, right));
                return;
            }

            // Сбор операндов одного и того же оператора
            var items = new List<LogicNode>();
            void Flatten(LogicNode n)
            {
                if (n is BinaryNode b && b.Operator == node.Operator)
                {
                    Flatten(b.Left);
                    Flatten(b.Right);
                }
                else { items.Add(n); }
            }
            Flatten(left);
            Flatten(right);

            // Удаление дубликатов/пар согласно оператору
            IEnumerable<LogicNode> uniqueOrdered;

            // Ключ для сравнения — каноническая строка
            string KeyOf(LogicNode n) => CanonicalStringVisitor.Build(n);

            if (node.Operator == "^" || node.Operator == "<=>")
            {
                // Попарное устранение одинаковых литералов/подвыражений (mod 2)
                var mod2 = items
                    .GroupBy(KeyOf)
                    .Select(g => new { node = g.First(), count = g.Count() })
                    .Where(x => (x.count % 2) == 1)
                    .Select(x => x.node)
                    .ToList();
                items = mod2;
            }
            else // & и |
            {
                items = items
                    .GroupBy(KeyOf)
                    .Select(g => g.First())
                    .ToList();
            }

            uniqueOrdered = items.OrderBy(KeyOf, StringComparer.Ordinal);

            var resultList = uniqueOrdered.ToList();
            if (resultList.Count == 0)
            {
                // ^ с пустым списком (все сократились) -> 0
                // <=> с пустым списком -> 1
                // & с пустым списком -> 1 (нейтральный)
                // | с пустым списком -> 0 (нейтральный)
                if (node.Operator == "^" || node.Operator == "|")
                    _stack.Push(new ConstantNode(false));
                else
                    _stack.Push(new ConstantNode(true));
                return;
            }

            // Rebuild tree
            var acc = resultList[0];
            for (int i = 1; i < resultList.Count; i++)
            {
                acc = new BinaryNode(node.Operator, acc, resultList[i]);
            }
            _stack.Push(acc);
        }
    }
}
