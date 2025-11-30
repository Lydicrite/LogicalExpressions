using System;

namespace LogicalExpressions.Compilation
{
    /// <summary>
    /// Опции компиляции для построения дерева выражений <see cref="System.Linq.Expressions.Expression"/>.
    /// Позволяют управлять политикой короткого замыкания для операторов AND/OR.
    /// </summary>
    public sealed class CompilationOptions
    {
        /// <summary>
        /// Управляет использованием короткого замыкания при компиляции.
        /// true: используется <c>Expression.AndAlso</c>/<c>Expression.OrElse</c>.
        /// false: применяется строгое булево вычисление без короткого замыкания через <c>Expression.And</c>/<c>Expression.Or</c>.
        /// По умолчанию: <c>true</c>.
        /// </summary>
        public bool UseShortCircuiting { get; set; } = true;
    }
}
