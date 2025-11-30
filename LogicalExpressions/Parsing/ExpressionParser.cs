using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Parsing.Strategies;
using LogicalExpressions.Parsing.Tokenization;
using LogicalExpressions.Utils;
using LogicalExpressions.Compilation.Visitors;

namespace LogicalExpressions.Parsing
{
    /// <summary>
    /// Представляет класс парсера логических выражений.
    /// </summary>
    public static class ExpressionParser
    {
        private static readonly OperatorRegistry _registry = new();
        private static readonly Tokenizer _tokenizer = new(_registry);
        private static readonly PostfixConverter _postfixConverter = new(_registry);
        private static readonly AstBuilder _astBuilder = new(_registry);
        private static IParserStrategy _strategy = new ShuntingYardParserStrategy();
        
        #region Кэш AST (потокобезопасный, ограниченный)
        private sealed class CacheEntry<T>
        {
            public T Value { get; }
            public long LastAccessTicks { get; private set; }
            public CacheEntry(T value)
            {
                Value = value;
                Touch();
            }
            public void Touch() => LastAccessTicks = Environment.TickCount64;
        }

        private static int _astMaxCacheSize = 1024;
        private static int _astEvictPercent = 10;
        private static bool _astEnableTtlEviction;
        private static long _astTtlMillis;

        private static long _astHits;
        private static long _astMisses;
        private static long _astEvictions;

        private static readonly ConcurrentDictionary<string, CacheEntry<LogicNode>> _astCache = new(StringComparer.Ordinal);
        private static readonly Lock _cacheLock = new();

        public static int CacheSize => _astCache.Count;

        private static void EnsureCapacity()
        {
            if (_astCache.Count < _astMaxCacheSize)
                return;
            lock (_cacheLock)
            {
                if (_astCache.Count < _astMaxCacheSize)
                    return;
                int toRemove = Math.Max(1, (_astMaxCacheSize * _astEvictPercent) / 100);
                var victims = _astCache
                    .OrderBy(kvp => kvp.Value.LastAccessTicks)
                    .Take(toRemove)
                    .Select(kvp => kvp.Key)
                    .ToList();
                foreach (var key in victims)
                {
                    if (_astCache.TryRemove(key, out _))
                        Interlocked.Increment(ref _astEvictions);
                }
            }
        }
        #endregion

        /// <summary>
        /// Преобразует строку в вычислимое логическое выражение.
        /// </summary>
        public static LogicNode Parse(string expression)
        {
            ArgumentNullException.ThrowIfNull(expression);

            var tokens = _tokenizer.Tokenize(expression);

            if (tokens.Count == 0)
                throw new ParseException(
                    ParseErrorCode.EmptyExpression,
                    "Пустое выражение",
                    0,
                    expression,
                    0);

            var cacheKey = $"PS={_strategy.GetType().Name}|UN={( _registry.Options.EnableUnicodeNormalization ? 1 : 0)}|{string.Join(" ", tokens)}";
            if (_astCache.TryGetValue(cacheKey, out var entry))
            {
                if (_astEnableTtlEviction && entry != null)
                {
                    long age = Environment.TickCount64 - entry.LastAccessTicks;
                    if (_astTtlMillis > 0 && age >= _astTtlMillis)
                    {
                        _astCache.TryRemove(cacheKey, out _);
                        Interlocked.Increment(ref _astEvictions);
                        Interlocked.Increment(ref _astMisses);
                    }
                    else
                    {
                        entry.Touch();
                        Interlocked.Increment(ref _astHits);
                        return entry.Value;
                    }
                }
                else if (entry != null)
                {
                    entry.Touch();
                    Interlocked.Increment(ref _astHits);
                    return entry.Value;
                }
            }
            Interlocked.Increment(ref _astMisses);

            ValidateTokenSequenceCore(expression, tokens);
            LogicNode ast;
            try
            {
                ast = _strategy.Parse(tokens, _registry);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Ошибка парсинга. Токены=[{string.Join(", ", tokens)}]",
                    ex);
            }

            var normalizer = new NormalizerVisitor();
            ast = normalizer.Normalize(ast);

            EnsureCapacity();
            _astCache[cacheKey] = new CacheEntry<LogicNode>(ast);
            return ast;
        }

        /// <summary>
        /// Перегрузка Parse с опциями.
        /// </summary>
        public static LogicNode Parse(string expression, LEParserOptions? options)
        {
            ArgumentNullException.ThrowIfNull(expression);

            bool enableHints = options?.EnableAliasSuggestions ?? true;
            var tokens = _tokenizer.Tokenize(expression, enableHints);

            if (tokens.Count == 0)
                throw new ParseException(
                    ParseErrorCode.EmptyExpression,
                    "Пустое выражение",
                    0,
                    expression,
                    0);

            var stratName = (options?.Strategy == ParserStrategy.Pratt) ? nameof(PrattParserStrategy) : nameof(ShuntingYardParserStrategy);
            var cacheKey = $"PS={stratName}|UN={( _registry.Options.EnableUnicodeNormalization ? 1 : 0)}|AS={(enableHints ? 1 : 0)}|{string.Join(" ", tokens)}";
            if (_astCache.TryGetValue(cacheKey, out var entry))
            {
                if (_astEnableTtlEviction && entry != null)
                {
                    long age = Environment.TickCount64 - entry.LastAccessTicks;
                    if (_astTtlMillis > 0 && age >= _astTtlMillis)
                    {
                        _astCache.TryRemove(cacheKey, out _);
                        Interlocked.Increment(ref _astEvictions);
                        Interlocked.Increment(ref _astMisses);
                    }
                    else
                    {
                        entry.Touch();
                        Interlocked.Increment(ref _astHits);
                        return entry.Value;
                    }
                }
                else if (entry != null)
                {
                    entry.Touch();
                    Interlocked.Increment(ref _astHits);
                    return entry.Value;
                }
            }
            Interlocked.Increment(ref _astMisses);

            ValidateTokenSequenceCore(expression, tokens);

            IParserStrategy strategyToUse = options?.Strategy switch
            {
                ParserStrategy.Pratt => new PrattParserStrategy(),
                _ => new ShuntingYardParserStrategy()
            };

            LogicNode ast;
            try
            {
                ast = strategyToUse.Parse(tokens, _registry);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Ошибка парсинга. Токены=[{string.Join(", ", tokens)}]",
                    ex);
            }

            var normalizer = new NormalizerVisitor();
            ast = normalizer.Normalize(ast);

            EnsureCapacity();
            _astCache[cacheKey] = new CacheEntry<LogicNode>(ast);
            return ast;
        }

        public static bool TryParse(string expression, out LogicNode? result, out ParseException? error)
        {
            result = null;
            error = null;

            try
            {
                var node = Parse(expression);
                result = node;
                return true;
            }
            catch (ParseException le)
            {
                error = le;
                return false;
            }
            catch (InvalidOperationException ioe)
            {
                error = new ParseException(ParseErrorCode.InvalidTokenSequence, ioe.Message, 0, ioe);
                return false;
            }
            catch (Exception ex)
            {
                error = new ParseException(ParseErrorCode.InvalidTokenSequence, ex.Message, 0, ex);
                return false;
            }
        }

        public static bool TryParse(string expression, LEParserOptions? options, out LogicNode? result, out ParseException? error)
        {
            result = null;
            error = null;
            try
            {
                var node = Parse(expression, options);
                result = node;
                return true;
            }
            catch (ParseException le)
            {
                error = le;
                return false;
            }
            catch (InvalidOperationException ioe)
            {
                error = new ParseException(ParseErrorCode.InvalidTokenSequence, ioe.Message, 0, ioe);
                return false;
            }
            catch (Exception ex)
            {
                error = new ParseException(ParseErrorCode.InvalidTokenSequence, ex.Message, 0, ex);
                return false;
            }
        }

        internal static List<string> Tokenize(string expression)
        {
            var tokens = _tokenizer.Tokenize(expression);
            return tokens.Select(t => t.Value).ToList();
        }

        private static void ValidateTokenSequenceCore(string expression, List<Token> tokens)
        {
            var categories = tokens.Select(t => GetTokenCategory(t)).ToList();
            int bracketBalance = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var current = categories[i];

                if (current == TokenCategory.OpenParen)
                {
                    bracketBalance++;
                    if (i > 0 && !IsValidBeforeOpenBracket(GetTokenCategory(tokens[i - 1])))
                        ThrowErrorDetailed(ParseErrorCode.InvalidTokenBeforeOpenParen,
                            $"Скобка '(' не может находиться после операнда \"{tokens[i - 1].Value}\"", i,
                            expression, token.Position, token.Position + 1, token.Value, current);
                }
                else if (current == TokenCategory.CloseParen)
                {
                    bracketBalance--;
                    if (bracketBalance < 0)
                        ThrowErrorDetailed(ParseErrorCode.UnmatchedClosingParenthesis, "Неспаренная закрывающая скобка", i,
                            expression, token.Position, token.Position + 1, token.Value, current);
                    if (i < tokens.Count - 1 && !IsValidAfterCloseBracket(GetTokenCategory(tokens[i + 1])))
                        ThrowErrorDetailed(ParseErrorCode.InvalidTokenAfterCloseParen,
                            $"Скобка ')' не может находиться перед операндом \"{tokens[i + 1].Value}\"", i,
                            expression, token.Position, token.Position + 1, token.Value, current);
                }

                if (current == TokenCategory.OperatorUnary)
                {
                    if (i == tokens.Count - 1 || !IsValidAfterUnaryOperator(GetTokenCategory(tokens[i + 1])))
                        ThrowErrorDetailed(ParseErrorCode.UnaryOperatorMissingOperand,
                            $"Унарный оператор \"{token.Value}\" требует операнд", i,
                            expression, token.Position, token.Position + token.Value.Length, token.Value, current);
                }

                if (current == TokenCategory.OperatorBinary)
                {
                    if (i == 0 || i == tokens.Count - 1)
                        ThrowErrorDetailed(ParseErrorCode.BinaryOperatorAtEnds,
                            $"Бинарный оператор '{token.Value}' не может быть в начале/конце", i,
                            expression, token.Position, token.Position + token.Value.Length, token.Value, current);

                    else if (!IsValidBinaryOperatorContext(GetTokenCategory(tokens[i - 1]), GetTokenCategory(tokens[i + 1])))
                        ThrowErrorDetailed(ParseErrorCode.InvalidBinaryOperatorContext,
                            $"Некорректное использование оператора '{token.Value}'", i,
                            expression, token.Position, token.Position + token.Value.Length, token.Value, current);
                }

                if (bracketBalance < 0)
                    ThrowErrorDetailed(ParseErrorCode.UnmatchedClosingParenthesis, "Неспаренная закрывающая скобка", i,
                        expression, token.Position, token.Position + 1, token.Value, current);
            }

            if (bracketBalance != 0)
            {
                int tokenPos = Math.Max(0, tokens.Count - 1);
                int charIndex = tokens.Count > 0 ? tokens[^1].Position : 0;
                ThrowErrorDetailed(ParseErrorCode.UnmatchedParentheses, "Неспаренные скобки", tokenPos,
                    expression, charIndex, charIndex + 1, tokens.Count > 0 ? tokens[^1].Value : string.Empty, TokenCategory.Unknown);
            }
        }

        private enum TokenCategory
        {
            Operand,
            OperatorUnary,
            OperatorBinary,
            OpenParen,
            CloseParen,
            Unknown
        }

        private static TokenCategory GetTokenCategory(Token token)
        {
            if (token.Type == TokenType.LParen) return TokenCategory.OpenParen;
            if (token.Type == TokenType.RParen) return TokenCategory.CloseParen;
            if (token.Type == TokenType.Operator)
            {
                if (_registry.UnaryOperators.ContainsKey(token.Value)) return TokenCategory.OperatorUnary;
                if (_registry.BinaryOperators.ContainsKey(token.Value)) return TokenCategory.OperatorBinary;
                return TokenCategory.Unknown;
            }
            if (token.Type == TokenType.Identifier || token.Type == TokenType.Constant) return TokenCategory.Operand;
            return TokenCategory.Unknown;
        }

        private static bool IsValidBeforeOpenBracket(TokenCategory prev) =>
            prev == TokenCategory.OperatorBinary || prev == TokenCategory.OpenParen || prev == TokenCategory.OperatorUnary;

        private static bool IsValidAfterCloseBracket(TokenCategory next) =>
            next == TokenCategory.OperatorBinary || next == TokenCategory.CloseParen;

        private static bool IsValidAfterUnaryOperator(TokenCategory next) =>
            next == TokenCategory.OpenParen || next == TokenCategory.Operand || next == TokenCategory.OperatorUnary;

        private static bool IsValidBinaryOperatorContext(TokenCategory left, TokenCategory right) =>
            (left == TokenCategory.Operand || left == TokenCategory.CloseParen) &&
            (right == TokenCategory.Operand || right == TokenCategory.OpenParen || right == TokenCategory.OperatorUnary);

        public static void ConfigureOperators(Action<OperatorRegistry> configure)
            => configure?.Invoke(_registry);

        public static void SetParserStrategy(IParserStrategy strategy)
            => _strategy = strategy ?? _strategy;

        public static void RegisterOperatorAlias(string alias, string canonical)
            => _registry.RegisterOperatorAlias(alias, canonical);

        public static void RegisterConstantAlias(string alias, string canonical)
            => _registry.RegisterConstantAlias(alias, canonical);

        public static void RegisterUnaryOperator(string symbol, int precedence, bool rightAssociative, Func<LogicNode, LogicNode> factory)
            => _registry.RegisterUnary(symbol, precedence, rightAssociative, factory);

        public static void RegisterBinaryOperator(string symbol, int precedence, bool rightAssociative, Func<LogicNode, LogicNode, LogicNode> factory)
            => _registry.RegisterBinary(symbol, precedence, rightAssociative, factory);

        public static void SetOperatorRegistryOptions(OperatorRegistryOptions options)
            => _registry.SetOptions(options);

        public static void SetUnicodeNormalization(bool enable)
        {
            var o = _registry.Options;
            var newOptions = new OperatorRegistryOptions
            {
                OperatorPrecedence = o.OperatorPrecedence,
                RightAssociative = o.RightAssociative,
                UnaryOperators = o.UnaryOperators,
                BinaryOperators = o.BinaryOperators,
                OperatorAliases = o.OperatorAliases,
                ConstantAliases = o.ConstantAliases,
                SuggestionMaxDistance = o.SuggestionMaxDistance,
                SuggestionMaxItems = o.SuggestionMaxItems,
                EnableSuggestions = o.EnableSuggestions,
                EnableUnicodeNormalization = enable
            };
            _registry.SetOptions(newOptions);
        }

        public static void ClearCache() => _astCache.Clear();
        public static void ClearAstCache() => _astCache.Clear();

        public static long AstCacheHits => Interlocked.Read(ref _astHits);
        public static long AstCacheMisses => Interlocked.Read(ref _astMisses);
        public static long AstCacheEvictions => Interlocked.Read(ref _astEvictions);

        public static void SetParserOptions(LEParserOptions options)
        {
            if (options == null) return;
            _astMaxCacheSize = Math.Max(1, options.AstMaxCacheSize);
            _astEvictPercent = Math.Clamp(options.AstEvictPercent, 1, 100);
            _astEnableTtlEviction = options.EnableAstTtlEviction;
            _astTtlMillis = options.AstTtl <= TimeSpan.Zero ? 0 : (long)options.AstTtl.TotalMilliseconds;

            LogicalExpressions.Core.LogicalExpression.ConfigureDelegateCache(
                Math.Max(1, options.DelegateMaxCacheSize),
                Math.Clamp(options.DelegateEvictPercent, 1, 100),
                options.EnableDelegateTtlEviction,
                options.DelegateTtl);
        }

        public static LogicNode Parse(string expression, ParserStrategy strategy)
            => Parse(expression, new LEParserOptions { Strategy = strategy });
        
        private static void ThrowErrorDetailed(ParseErrorCode code, string message, int position,
            string expression, int startIndex, int endIndex, string token, TokenCategory category)
        {
            string expected = message;
            switch (code)
            {
                case ParseErrorCode.InvalidTokenBeforeOpenParen:
                    expected += ": Ожидается один из: бинарный оператор, '(' или унарный оператор";
                    break;
                case ParseErrorCode.InvalidTokenAfterCloseParen:
                    expected += ": Ожидается один из: бинарный оператор или ')'";
                    break;
                case ParseErrorCode.UnaryOperatorMissingOperand:
                    expected += ": Ожидается: операнд, '(' или унарный оператор";
                    break;
                case ParseErrorCode.BinaryOperatorAtEnds:
                case ParseErrorCode.InvalidBinaryOperatorContext:
                    expected += ": Ожидается слева: операнд или ')', справа: операнд, '(' или унарный оператор";
                    break;
            }
            int? charCode = (endIndex - startIndex == 1 && startIndex >= 0 && startIndex < expression.Length)
                ? expression[startIndex]
                : (int?)null;
            string tokenCode = category.ToString();
            throw new ParseException(code, expected, position, expression, startIndex, endIndex, charCode, token, tokenCode, suggestions: null);
        }
    }
}
