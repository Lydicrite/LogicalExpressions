using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Parsing.Tokenization;
using LogicalExpressions.Utils;

namespace LogicalExpressions.Parsing
{
    public class AstBuilder(OperatorRegistry registry)
    {
        private readonly OperatorRegistry _registry = registry;

        public LogicNode BuildAST(List<Token> postfix)
        {
            var stack = new Stack<LogicNode>();

            foreach (var token in postfix)
            {
                if (token.Type == TokenType.Constant)
                {
                    stack.Push(new ConstantNode(token.Value == "1" || token.Value.Equals("true", System.StringComparison.OrdinalIgnoreCase)));
                }
                else if (token.Type == TokenType.Operator)
                {
                    if (_registry.UnaryOperators.TryGetValue(token.Value, out var unaryFactory))
                    {
                        if (stack.Count < 1)
                             throw new ParseException(ParseErrorCode.InvalidTokenSequence, $"Недостаточно операндов для унарного оператора '{token.Value}'", token.Position);
                        
                        stack.Push(unaryFactory(stack.Pop()));
                    }
                    else if (_registry.BinaryOperators.TryGetValue(token.Value, out var binaryFactory))
                    {
                        if (stack.Count < 2)
                            throw new ParseException(ParseErrorCode.InvalidTokenSequence, $"Недостаточно операндов для бинарного оператора '{token.Value}'", token.Position);
                        var right = stack.Pop();
                        var left = stack.Pop();
                        stack.Push(binaryFactory(left, right));
                    }
                    else
                    {
                         throw new ParseException(ParseErrorCode.UnknownToken, $"Неизвестный оператор '{token.Value}'", token.Position);
                    }
                }
                else // Identifier or Unknown
                {
                    stack.Push(new VariableNode(token.Value));
                }
            }

            if (stack.Count != 1)
                throw new ParseException(ParseErrorCode.InvalidTokenSequence, $"Некорректная постфиксная запись: ожидается один корень, фактически {stack.Count}", -1);
            return stack.Pop();
        }
    }
}
