using System;
using System.Linq.Expressions;

namespace LogicalExpressions.Utils
{
    /// <summary>
    /// Вспомогательный класс для построения узлов <see cref="Expression"/> по строковым операторам.
    /// Содержит единое сопоставление операторов логики с соответствующими фабриками узлов выражений.
    /// </summary>
    public static class ExpressionOperatorHelper
    {
        /// <summary>
        /// Строит унарное выражение по оператору.
        /// </summary>
        /// <param name="op">Строковый унарный оператор (например, <c>"~"</c>).</param>
        /// <param name="operand">Операнд унарного оператора.</param>
        /// <returns>Построенное выражение.</returns>
        /// <exception cref="NotSupportedException">Если оператор не поддерживается.</exception>
        public static Expression BuildUnary(string op, Expression operand)
        {
            return op switch
            {
                "~" => Expression.Not(operand),
                _ => throw new NotSupportedException($"Оператор '{op}' не поддерживается")
            };
        }

        /// <summary>
        /// Строит бинарное выражение по оператору. По умолчанию использует короткое замыкание
        /// для <c>&amp;</c> и <c>|</c> (через <see cref="Expression.AndAlso(Expression, Expression)"/> и <see cref="Expression.OrElse(Expression, Expression)"/>).
        /// Эквивалентность <c>&lt;=&gt;</c> разворачивается в базовые операции
        /// <c>(~A &amp; ~B) | (A &amp; B)</c> для единообразия с нормализатором.
        /// </summary>
        /// <param name="op">Строковый бинарный оператор (например, <c>"&amp;"</c>, <c>"|"</c>, <c>"^"</c>, <c>"=&gt;"</c>, <c>"&lt;=&gt;"</c>, <c>"!&amp;"</c>, <c>"!|"</c>).</param>
        /// <param name="left">Левый операнд.</param>
        /// <param name="right">Правый операнд.</param>
        /// <returns>Построенное выражение.</returns>
        /// <exception cref="NotSupportedException">Если оператор не поддерживается.</exception>
        public static Expression BuildBinary(string op, Expression left, Expression right)
        {
            return op switch
            {
                "&" => Expression.AndAlso(left, right),
                "|" => Expression.OrElse(left, right),
                "^" => Expression.ExclusiveOr(left, right),
                "=>" => Expression.OrElse(Expression.Not(left), right),
                // Эквивалентность: A <=> B  ==  (~A & ~B) | (A & B)
                "<=>" => Expression.OrElse(
                    Expression.AndAlso(Expression.Not(left), Expression.Not(right)),
                    Expression.AndAlso(left, right)
                ),
                "!&" => Expression.Not(Expression.AndAlso(left, right)),
                "!|" => Expression.Not(Expression.OrElse(left, right)),
                _ => throw new NotSupportedException($"Оператор '{op}' не поддерживается")
            };
        }
    }
}
