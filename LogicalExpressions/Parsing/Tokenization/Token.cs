using System;

namespace LogicalExpressions.Parsing.Tokenization
{
    public enum TokenType
    {
        Unknown,
        Identifier, // Variables or unclassified
        Constant,   // 0, 1, true, false aliases
        Operator,   // &, |, ~, =>, etc.
        LParen,     // (
        RParen      // )
    }

    public readonly struct Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int Position { get; }

        public Token(TokenType type, string value, int position)
        {
            Type = type;
            Value = value;
            Position = position;
        }

        public override string ToString() => Value;
    }
}
