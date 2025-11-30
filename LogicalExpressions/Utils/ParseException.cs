using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LogicalExpressions.Utils
{
    /// <summary>
    /// Коды ошибок парсинга логических выражений.
    /// </summary>
    public enum ParseErrorCode
    {
        EmptyExpression,
        InvalidTokenBeforeOpenParen,
        InvalidTokenAfterCloseParen,
        UnaryOperatorMissingOperand,
        BinaryOperatorAtEnds,
        InvalidBinaryOperatorContext,
        UnmatchedClosingParenthesis,
        UnmatchedParentheses,
        UnknownToken,
        InvalidTokenSequence
    }

    /// <summary>
    /// Представляет исключения, возникающее при работе парсера логических выражений.
    /// </summary>
    [Serializable]
    public sealed class ParseException : Exception
    {
        /// <summary>
        /// Позиция в списке токенов, где произошла ошибка.
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// Позиция символа в исходном выражении (индекс в строке).
        /// </summary>
        public int CharIndex { get; }

        /// <summary>
        /// Номер строки (начиная с 1), где произошла ошибка.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Номер столбца (начиная с 1), где произошла ошибка.
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Исходное выражение (для диагностики).
        /// </summary>
        public string? Expression { get; }

        /// <summary>
        /// Строка с фрагментом выражения (линия), где произошла ошибка.
        /// </summary>
        public string? Snippet { get; }

        /// <summary>
        /// Визуальная подсветка позиции ошибки (строка с ^).
        /// </summary>
        public string? Highlight { get; }

        /// <summary>
        /// Код ошибки парсинга.
        /// </summary>
        public ParseErrorCode ErrorCode { get; }

        /// <summary>
        /// Диапазон символов [StartIndex, EndIndex) в исходном выражении, где произошла ошибка.
        /// </summary>
        public int StartIndex { get; }
        public int EndIndex { get; }

        /// <summary>
        /// Исходный проблемный токен/фрагмент.
        /// </summary>
        public string? Token { get; }

        /// <summary>
        /// Категория/код токена (например, UnaryOperator, BinaryOperator, Operand).
        /// </summary>
        public string? TokenCode { get; }

        /// <summary>
        /// Код символа Unicode (если применимо для одиночного символа).
        /// </summary>
        public int? CharCode { get; }

        /// <summary>
        /// Список подсказок ближайших алиасов.
        /// </summary>
        public IReadOnlyList<string>? Suggestions { get; }

        /// <summary>
        /// Создает исключение парсинга с кодом ошибки и позицией.
        /// </summary>
        public ParseException(ParseErrorCode code, string message, int pos)
            : base($"{message} (Позиция: {pos}, Код: {code})")
        {
            ErrorCode = code;
            Position = pos;
            CharIndex = pos; // Default fallback
            Line = 1;
            Column = pos + 1;
        }

        /// <summary>
        /// Создает исключение парсинга с кодом ошибки и внутренней причиной.
        /// </summary>
        public ParseException(ParseErrorCode code, string message, int pos, Exception inner)
            : base($"{message} (Позиция: {pos}, Код: {code})", inner)
        {
            ErrorCode = code;
            Position = pos;
            CharIndex = pos; // Default fallback
            Line = 1;
            Column = pos + 1;
        }

        /// <summary>
        /// Создает исключение с подробной информацией о месте ошибки в исходном выражении.
        /// </summary>
        public ParseException(
            ParseErrorCode code,
            string message,
            int pos,
            string expression,
            int startIndex,
            int endIndex = -1,
            int? charCode = null,
            string? token = null,
            string? tokenCode = null,
            IReadOnlyList<string>? suggestions = null)
            : base(FormatMessage(message, code, expression, startIndex))
        {
            ErrorCode = code;
            Position = pos;
            Expression = expression;
            StartIndex = startIndex;
            EndIndex = endIndex == -1 ? startIndex + 1 : endIndex;
            CharIndex = startIndex;
            Token = token;
            TokenCode = tokenCode;
            CharCode = charCode;
            Suggestions = suggestions;

            // Calculate line/column/snippet
            if (expression != null && startIndex >= 0 && startIndex <= expression.Length)
            {
                (Line, Column, Snippet, Highlight) = GetErrorLocation(expression, startIndex);
            }
        }

        private static string FormatMessage(string message, ParseErrorCode code, string expression, int index)
        {
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"{message} (Код: {code})");

            if (expression != null && index >= 0 && index <= expression.Length)
            {
                var (_, col, snippet, highlight) = GetErrorLocation(expression, index);
                sb.AppendLine();
                sb.AppendLine(snippet);
                sb.AppendLine(highlight);
            }

            return sb.ToString();
        }

        private static (int line, int column, string snippet, string highlight) GetErrorLocation(string expression, int index)
        {
            int line = 1;
            int lastLineStart = 0;
            for (int i = 0; i < index && i < expression.Length; i++)
            {
                if (expression[i] == '\n')
                {
                    line++;
                    lastLineStart = i + 1;
                }
            }
            int column = index - lastLineStart + 1;

            // Get snippet line
            int lineEnd = expression.IndexOf('\n', lastLineStart);
            if (lineEnd == -1) lineEnd = expression.Length;
            
            // Trim newlines
            if (lineEnd > lastLineStart && expression[lineEnd - 1] == '\r') lineEnd--;

            string snippetLine = expression.Substring(lastLineStart, lineEnd - lastLineStart);
            
            // Build highlight string
            // Replace tabs with spaces to match alignment if necessary, simplified here
            var sb = new StringBuilder();
            for (int i = 0; i < column - 1; i++)
            {
                sb.Append(' '); // Simplification: assume monospace and no tabs/wide chars
            }
            sb.Append('^');

            return (line, column, snippetLine, sb.ToString());
        }
    }
}
