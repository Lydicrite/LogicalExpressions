using System;
using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Parsing.Tokenization;
using LogicalExpressions.Utils;

namespace LogicalExpressions.Parsing.Strategies
{
    /// <summary>
    /// Стратегия парсинга на основе алгоритма Пратта (Top-down operator precedence).
    /// </summary>
    public class PrattParserStrategy : IParserStrategy
    {
        /// <inheritdoc />
        public LogicNode Parse(List<Token> tokens, OperatorRegistry registry)
        {
            int pos = 0;
            var expr = ParseExpression(ref pos, tokens, registry, 0);
            if (pos != tokens.Count)
            {
                var extra = pos < tokens.Count ? tokens[pos].Value : "<end>";
                throw new ParseException(ParseErrorCode.InvalidTokenSequence, $"Лишние токены после разбора: '{extra}'", pos < tokens.Count ? tokens[pos].Position : -1);
            }
            return expr;
        }

        private static LogicNode ParseExpression(ref int pos, IReadOnlyList<Token> tokens, OperatorRegistry registry, int minBp)
        {
            var left = ParseNud(ref pos, tokens, registry);

            while (pos < tokens.Count)
            {
                var token = tokens[pos];
                if (token.Type != TokenType.Operator || !registry.BinaryOperators.ContainsKey(token.Value))
                    break;

                var bp = registry.OperatorPrecedence[token.Value];
                var rightAssoc = registry.RightAssociative.Contains(token.Value);
                var lbp = bp;
                var rbp = rightAssoc ? bp : bp + 1;

                if (lbp < minBp)
                    break;

                pos++; // consume operator
                var startPos = pos;
                var right = ParseExpression(ref pos, tokens, registry, rbp);
                if (pos == startPos)
                {
                    right = ParseNud(ref pos, tokens, registry);
                }
                left = registry.BinaryOperators[token.Value](left, right);
            }

            return left;
        }

        private static LogicNode ParseNud(ref int pos, IReadOnlyList<Token> tokens, OperatorRegistry registry)
        {
            if (pos >= tokens.Count)
                throw new ParseException(ParseErrorCode.InvalidTokenSequence, "Ожидался операнд", pos > 0 ? tokens[pos-1].Position : 0);

            var token = tokens[pos++];

            if (token.Type == TokenType.LParen)
            {
                var expr = ParseExpression(ref pos, tokens, registry, 0);
                if (pos >= tokens.Count || tokens[pos].Type != TokenType.RParen)
                    throw new ParseException(ParseErrorCode.UnmatchedParentheses, "Ожидалась закрывающая скобка", pos < tokens.Count ? tokens[pos].Position : -1);
                pos++; // consume ')'
                return expr;
            }

            if (token.Type == TokenType.Operator)
            {
                if (registry.UnaryOperators.TryGetValue(token.Value, out var factory))
                {
                    var bp = registry.OperatorPrecedence[token.Value];
                    var right = ParseExpression(ref pos, tokens, registry, bp);
                    return factory(right);
                }
                throw new ParseException(ParseErrorCode.InvalidTokenSequence, $"Неожиданный оператор в позиции префикса: '{token.Value}'", token.Position);
            }

            if (token.Type == TokenType.Constant)
            {
                return new ConstantNode(token.Value == "1" || token.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
            }

            if (token.Type == TokenType.Identifier)
            {
                return new VariableNode(token.Value);
            }

            throw new ParseException(ParseErrorCode.UnknownToken, $"Неожиданный токен '{token.Value}'", token.Position);
        }
    }
}
