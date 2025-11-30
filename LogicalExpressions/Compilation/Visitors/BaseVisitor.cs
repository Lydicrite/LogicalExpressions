using System;
using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Compilation.Visitors
{
    /// <summary>
    /// Интерфейс посетителя.
    /// </summary>
    public interface ILEVisitor
    {
        /// <summary>
        /// Метод посещения узла логического выражения.
        /// </summary>
        /// <param name="node">Узел, который нужно посетить.</param>
        void Visit(LogicNode node);
    }

    /// <summary>
    /// Базовый абстрактный посетитель с методами для посещения всех типов узлов.
    /// </summary>
    public abstract class BaseVisitor : ILEVisitor
    {
        public void Visit(LogicNode node)
        {
            switch (node)
            {
                case ConstantNode cn:
                    VisitConstant(cn);
                    break;
                case VariableNode vn:
                    VisitVariable(vn);
                    break;
                case UnaryNode un:
                    VisitUnary(un);
                    break;
                case BinaryNode bn:
                    VisitBinary(bn);
                    break;
                default:
                    throw new NotSupportedException($"Тип узла {node.GetType()} не поддерживается");
            }
        }
        
        /// <summary>
        /// Метод посещения узла-константы.
        /// </summary>
        /// <param name="node">Узел-константа, который необходимо посетить.</param>
        public abstract void VisitConstant(ConstantNode node);
        /// <summary>
        /// Метод посещения узла-переменной.
        /// </summary>
        /// <param name="node">Узел-переменная, который необходимо посетить.</param>
        public abstract void VisitVariable(VariableNode node);
        /// <summary>
        /// Метод посещения узла унарного оператора.
        /// </summary>
        /// <param name="node">Узел унарного оператора, который необходимо посетить.</param>
        public abstract void VisitUnary(UnaryNode node);
        /// <summary>
        /// Метод посещения узла бинарного оператора.
        /// </summary>
        /// <param name="node">Узел бинарного оператора, который необходимо посетить.</param>
        public abstract void VisitBinary(BinaryNode node);
    }
}
