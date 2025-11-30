using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Compilation.Visitors
{
    /// <summary>
    /// Посетитель, применяющий к узлам выражения законы алгебры логики для их упрощения и нормализации.
    /// </summary>
    public sealed class NormalizerVisitor : BaseVisitor
    {
        /// <summary>
        /// Стэк, содержащий узлы выражения и используемый для их обработки.
        /// </summary>
        private readonly Stack<LogicNode> _stack = new Stack<LogicNode>();

        /// <summary>
        /// Метод, применяющий законы алгебры логики для узла.
        /// </summary>
        /// <param name="node">Узел, для которого нужно применить метод.</param>
        /// <returns></returns>
        public LogicNode Normalize(LogicNode node)
        {
            node.Accept(this);
            return _stack.Pop();
        }

        public override void VisitConstant(ConstantNode node) => _stack.Push(node);

        public override void VisitVariable(VariableNode node) => _stack.Push(node);

        public override void VisitUnary(UnaryNode node)
        {
            node.Operand.Accept(this);
            var operand = _stack.Pop();

            // Оптимизация: ~(const) → !const
            if (operand is ConstantNode cn)
            {
                _stack.Push(new ConstantNode(!cn.Value));
                return;
            }

            // Устранение двойного отрицания
            if (node.Operator == "~" && operand is UnaryNode inner && inner.Operator == "~")
            {
                _stack.Push(inner.Operand);
                return;
            }

            // Де Морган для бинарных операций
            if (node.Operator == "~" && operand is BinaryNode binary)
            {
                var left = new UnaryNode("~", binary.Left);
                var right = new UnaryNode("~", binary.Right);

                switch (binary.Operator)
                {
                    case "&":
                        _stack.Push(new BinaryNode("|", left, right));
                        return;
                    case "|":
                        _stack.Push(new BinaryNode("&", left, right));
                        return;
                }
            }

            _stack.Push(new UnaryNode(node.Operator, operand));
        }

        public override void VisitBinary(BinaryNode node)
        {
            node.Left.Accept(this);
            node.Right.Accept(this);

            var right = _stack.Pop();
            var left = _stack.Pop();

            // Упрощение константных выражений
            {
                if (left is ConstantNode cl && right is ConstantNode cr)
                {
                    bool lv = cl.Value;
                    bool rv = cr.Value;

                    switch (node.Operator)
                    {
                        case "&": _stack.Push(new ConstantNode(lv && rv)); return;
                        case "|": _stack.Push(new ConstantNode(lv || rv)); return;
                        case "^": _stack.Push(new ConstantNode(lv ^ rv)); return;
                        case "=>": _stack.Push(new ConstantNode(!lv || rv)); return;
                        case "<=>": _stack.Push(new ConstantNode(lv == rv)); return;
                        case "!&": _stack.Push(new ConstantNode(!(lv && rv))); return;
                        case "!|": _stack.Push(new ConstantNode(!(lv || rv))); return;
                    }
                }
                
                // Identity laws (A & 1 = A, A | 0 = A) and Annihilator laws (A & 0 = 0, A | 1 = 1)
                if (node.Operator == "&")
                {
                    if (left is ConstantNode c1)
                        _stack.Push(c1.Value ? right : new ConstantNode(false));
                    else if (right is ConstantNode c2)
                        _stack.Push(c2.Value ? left : new ConstantNode(false));
                    else
                        _stack.Push(new BinaryNode("&", left, right));
                    return;
                }
                
                if (node.Operator == "|")
                {
                    if (left is ConstantNode c3)
                        _stack.Push(c3.Value ? new ConstantNode(true) : right);
                    else if (right is ConstantNode c4)
                        _stack.Push(c4.Value ? new ConstantNode(true) : left);
                    else
                        _stack.Push(new BinaryNode("|", left, right));
                    return;
                }
            }
            
            _stack.Push(new BinaryNode(node.Operator, left, right));
        }
    }
}
