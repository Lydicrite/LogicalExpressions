using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Parsing.Tokenization;

namespace LogicalExpressions.Parsing.Strategies
{
    /// <summary>
    /// Стратегия парсинга на основе алгоритма сортировочной станции (Shunting-yard).
    /// </summary>
    public class ShuntingYardParserStrategy : IParserStrategy
    {
        /// <inheritdoc />
        public LogicNode Parse(List<Token> tokens, OperatorRegistry registry)
        {
            var converter = new PostfixConverter(registry);
            var builder = new AstBuilder(registry);
            var postfix = converter.ConvertToPostfix(tokens);
            return builder.BuildAST(postfix);
        }
    }
}
