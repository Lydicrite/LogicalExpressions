using System;
using System.Collections.Immutable;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Parsing
{
    /// <summary>
    /// Конфигурация реестра операторов: приоритеты, ассоциативность, фабрики и алиасы.
    /// Также содержит параметры подсказок для токенизатора.
    /// </summary>
    public sealed class OperatorRegistryOptions
    {
        public ImmutableDictionary<string, int> OperatorPrecedence { get; init; } = ImmutableDictionary<string, int>.Empty;
        public ImmutableHashSet<string> RightAssociative { get; init; } = ImmutableHashSet<string>.Empty;
        public ImmutableDictionary<string, Func<LogicNode, LogicNode>> UnaryOperators { get; init; } = ImmutableDictionary<string, Func<LogicNode, LogicNode>>.Empty;
        public ImmutableDictionary<string, Func<LogicNode, LogicNode, LogicNode>> BinaryOperators { get; init; } = ImmutableDictionary<string, Func<LogicNode, LogicNode, LogicNode>>.Empty;
        public ImmutableDictionary<string, string> OperatorAliases { get; init; } = ImmutableDictionary<string, string>.Empty;
        public ImmutableDictionary<string, string> ConstantAliases { get; init; } = ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Максимально допустимая дистанция Левенштейна для подсказок операторов.
        /// </summary>
        public int SuggestionMaxDistance { get; init; } = 2;

        /// <summary>
        /// Максимальное число подсказок.
        /// </summary>
        public int SuggestionMaxItems { get; init; } = 3;

        /// <summary>
        /// Включить подсказки ближайших алиасов при неизвестных токенах.
        /// </summary>
        public bool EnableSuggestions { get; init; } = true;

        /// <summary>
        /// Включить нормализацию Unicode (NFKC) при токенизации.
        /// Отключение может повысить производительность на уже нормализованных входах.
        /// </summary>
        public bool EnableUnicodeNormalization { get; init; } = true;

        private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

        public static OperatorRegistryOptions Default { get; } = BuildDefault();

        private static OperatorRegistryOptions BuildDefault()
        {
            var precedence = ImmutableDictionary.Create<string, int>(Comparer)
                .Add("~", 5).Add("&", 4).Add("!&", 4).Add("^", 3)
                .Add("|", 2).Add("!|", 2).Add("=>", 1).Add("<=>", 0);

            var rightAssoc = ImmutableHashSet.Create<string>(Comparer, "~", "=>");

            var unary = ImmutableDictionary.Create<string, Func<LogicNode, LogicNode>>(Comparer)
                .Add("~", operand => new UnaryNode("~", operand));

            var binary = ImmutableDictionary.Create<string, Func<LogicNode, LogicNode, LogicNode>>(Comparer)
                .Add("&", (l, r) => new BinaryNode("&", l, r))
                .Add("|", (l, r) => new BinaryNode("|", l, r))
                .Add("^", (l, r) => new BinaryNode("^", l, r))
                .Add("=>", (l, r) => new BinaryNode("=>", l, r))
                .Add("<=>", (l, r) => new BinaryNode("<=>", l, r))
                .Add("!&", (l, r) => new BinaryNode("!&", l, r))
                .Add("!|", (l, r) => new BinaryNode("!|", l, r));

            var aliases = ImmutableDictionary.Create<string, string>(Comparer)
                // Бинарные операторы
                .Add("&", "&").Add("AND", "&").Add("˄", "&").Add("∧", "&").Add("&&", "&").Add("и", "&")
                .Add("|", "|").Add("OR", "|").Add("˅", "|").Add("∨", "|").Add("||", "|").Add("или", "|")
                .Add("^", "^").Add("XOR", "^").Add("⊕", "^")
                .Add("=>", "=>").Add("IMPLIES", "=>").Add("→", "=>").Add("->", "=>")
                .Add("<=>", "<=>").Add("IFF", "<=>").Add("XNOR", "<=>").Add("≡", "<=>").Add("⇔", "<=>").Add("↔", "<=>")
                .Add("!&", "!&").Add("NAND", "!&").Add("/", "!&").Add("⊼", "!&").Add("↑", "!&")
                .Add("!|", "!|").Add("NOR", "!|").Add("↓", "!|").Add("⊽", "!|")
                // Унарные операторы
                .Add("!", "~").Add("NOT", "~").Add("~", "~").Add("¬", "~").Add("не", "~");

            var constAliases = ImmutableDictionary.Create<string, string>(Comparer)
                .Add("0", "0").Add("false", "0").Add("⊥", "0").Add("нет", "0")
                .Add("1", "1").Add("true", "1").Add("⊤", "1").Add("да", "1");
            // ВНИМАНИЕ: Убраны короткие алиасы 'F' и 'T', чтобы не конфликтовать с переменными

            return new OperatorRegistryOptions
            {
                OperatorPrecedence = precedence,
                RightAssociative = rightAssoc,
                UnaryOperators = unary,
                BinaryOperators = binary,
                OperatorAliases = aliases,
                ConstantAliases = constAliases,
                SuggestionMaxDistance = 2,
                SuggestionMaxItems = 3,
                EnableSuggestions = true,
                EnableUnicodeNormalization = true
            };
        }
    }
}
