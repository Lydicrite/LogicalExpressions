using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Parsing.Tokenization;

namespace LogicalExpressions.Parsing.Strategies
{
    /// <summary>
    /// Интерфейс стратегии парсинга.
    /// </summary>
    public interface IParserStrategy
    {
        /// <summary>
        /// Выполняет парсинг списка токенов в AST.
        /// </summary>
        /// <param name="tokens">Список токенов.</param>
        /// <param name="registry">Реестр операторов.</param>
        /// <returns>Корневой узел AST.</returns>
        LogicNode Parse(List<Token> tokens, OperatorRegistry registry);
    }
}
