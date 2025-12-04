using System;
using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Optimization.BDD;

namespace LogicalExpressions.Optimization.VariableOrdering
{
    /// <summary>
    /// Интерфейс стратегии оптимизации порядка переменных.
    /// </summary>
    public interface IVariableOrderingStrategy
    {
        /// <summary>
        /// Определяет оптимальный порядок переменных для заданного логического выражения.
        /// </summary>
        /// <param name="root">Корневой узел выражения.</param>
        /// <param name="currentVariables">Текущий список переменных.</param>
        /// <returns>Новый упорядоченный список переменных.</returns>
        IEnumerable<string> OptimizeOrder(LogicNode root, IReadOnlyList<string> currentVariables);
    }
}
