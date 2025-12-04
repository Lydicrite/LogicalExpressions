using System;
using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Optimization.BDD;

namespace LogicalExpressions.Compilation.Visitors
{
    /// <summary>
    /// Посетитель для построения бинарной диаграммы решений (BDD) из AST.
    /// </summary>
    public class BDDBuilderVisitor(BDDManager manager, Dictionary<string, int> varMap) : ILEVisitor
    {
        private readonly BDDManager _manager = manager;
        private readonly Dictionary<string, int> _varMap = varMap;
        private readonly Stack<BDDNode> _stack = new();

        /// <summary>
        /// Результат построения BDD.
        /// </summary>
        public BDDNode Result => _stack.Count > 0 ? _stack.Peek() : BDDManager.Zero;

        /// <summary>
        /// Посещает узел логического выражения.
        /// </summary>
        public void Visit(LogicNode node)
        {
            switch (node)
            {
                case ConstantNode cn: VisitConstant(cn); break;
                case VariableNode vn: VisitVariable(vn); break;
                case UnaryNode un: VisitUnary(un); break;
                case BinaryNode bn: VisitBinary(bn); break;
                default: throw new NotSupportedException($"Тип узла {node.GetType()} не поддерживается");
            }
        }

        private void VisitConstant(ConstantNode node)
        {
            _stack.Push(node.Value ? BDDManager.One : BDDManager.Zero);
        }

        private void VisitVariable(VariableNode node)
        {
            if (_varMap.TryGetValue(node.Name, out var idx))
            {
                _stack.Push(_manager.CreateVariable(idx));
            }
            else
            {
                throw new InvalidOperationException($"Неизвестная переменная {node.Name} при конвертации в BDD");
            }
        }

        private void VisitUnary(UnaryNode node)
        {
            node.Operand.Accept(this);
            var op = _stack.Pop();
            if (node.Operator == "~")
            {
                _stack.Push(_manager.Not(op));
            }
            else
            {
                throw new NotSupportedException($"BDD builder не поддерживает унарный оператор {node.Operator}");
            }
        }

        private void VisitBinary(BinaryNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);
            var r = _stack.Pop();
            var l = _stack.Pop();

            switch (node.Operator)
            {
                case "&": _stack.Push(_manager.And(l, r)); break;
                case "|": _stack.Push(_manager.Or(l, r)); break;
                case "^": _stack.Push(_manager.Xor(l, r)); break;
                case "=>": _stack.Push(_manager.Imply(l, r)); break;
                case "<=>": 
                    _stack.Push(_manager.Not(_manager.Xor(l, r))); 
                    break;
                case "!&": _stack.Push(_manager.Not(_manager.And(l, r))); break;
                case "!|": _stack.Push(_manager.Not(_manager.Or(l, r))); break;
                default: throw new NotSupportedException($"BDD builder не поддерживает бинарный оператор {node.Operator}");
            }
        }
    }
}
