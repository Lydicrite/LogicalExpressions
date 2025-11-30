using System.Collections.Generic;
using System.Linq;
using LogicalExpressions.Parsing.Tokenization;

namespace LogicalExpressions.Parsing
{
    public class PostfixConverter(OperatorRegistry registry)
    {
        private readonly OperatorRegistry _registry = registry;

        public List<Token> ConvertToPostfix(List<Token> tokens)
        {
            var output = new List<Token>(tokens.Count);
            var stack = new Stack<Token>();

            foreach (var token in tokens)
            {
                if (token.Type == TokenType.LParen)
                {
                    stack.Push(token);
                }
                else if (token.Type == TokenType.RParen)
                {
                    while (stack.Count > 0 && stack.Peek().Type != TokenType.LParen)
                        output.Add(stack.Pop());
                    
                    if (stack.Count > 0) stack.Pop(); // Pop '('
                }
                else if (token.Type == TokenType.Operator)
                {
                    if (_registry.OperatorPrecedence.TryGetValue(token.Value, out var precedence))
                    {
                        var isRightAssoc = _registry.RightAssociative.Contains(token.Value);
                        while (stack.Count > 0 && stack.Peek().Type != TokenType.LParen)
                        {
                            var top = stack.Peek();
                            if (top.Type == TokenType.Operator && _registry.OperatorPrecedence.TryGetValue(top.Value, out var topPrecedence))
                            {
                                if (topPrecedence > precedence || (topPrecedence == precedence && !isRightAssoc))
                                {
                                    output.Add(stack.Pop());
                                    continue;
                                }
                            }
                            break;
                        }
                        stack.Push(token);
                    }
                    else
                    {
                        output.Add(token);
                    }
                }
                else
                {
                    output.Add(token);
                }
            }

            while (stack.Count > 0)
                output.Add(stack.Pop());

            return output;
        }
    }
}
