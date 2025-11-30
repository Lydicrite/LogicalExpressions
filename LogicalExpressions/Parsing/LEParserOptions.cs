using System;

namespace LogicalExpressions.Parsing
{
    /// <summary>
    /// Опции парсера: выбор стратегии и подсказки алиасов.
    /// </summary>
    public sealed class LEParserOptions
    {
        public ParserStrategy Strategy { get; init; } = ParserStrategy.ShuntingYard;
        /// <summary>
        /// Включить подсказки ближайших алиасов для неизвестных токенов.
        /// </summary>
        public bool EnableAliasSuggestions { get; init; } = true;

        // Настройка кэша AST
        /// <summary>
        /// Максимальный размер кэша AST. По умолчанию 1024.
        /// </summary>
        public int AstMaxCacheSize { get; init; } = 1024;
        /// <summary>
        /// Процент эвикции при переполнении кэша AST. По умолчанию 10.
        /// </summary>
        public int AstEvictPercent { get; init; } = 10;
        /// <summary>
        /// Включить TTL-эвикцию для AST (по времени неиспользования).
        /// </summary>
        public bool EnableAstTtlEviction { get; init; }
        /// <summary>
        /// TTL для AST (время неиспользования до эвикции). По умолчанию 0 (выключено).
        /// </summary>
        public TimeSpan AstTtl { get; init; } = TimeSpan.Zero;

        // Настройка кэша делегатов (LogicalExpression)
        /// <summary>
        /// Максимальный размер кэша делегатов. По умолчанию 2048.
        /// </summary>
        public int DelegateMaxCacheSize { get; init; } = 2048;
        /// <summary>
        /// Процент эвикции при переполнении кэша делегатов. По умолчанию 10.
        /// </summary>
        public int DelegateEvictPercent { get; init; } = 10;
        /// <summary>
        /// Включить TTL-эвикцию для делегатов (по времени неиспользования).
        /// </summary>
        public bool EnableDelegateTtlEviction { get; init; }
        /// <summary>
        /// TTL для делегатов (время неиспользования до эвикции). По умолчанию 0 (выключено).
        /// </summary>
        public TimeSpan DelegateTtl { get; init; } = TimeSpan.Zero;
    }

    public enum ParserStrategy
    {
        Pratt,
        ShuntingYard
    }
}
