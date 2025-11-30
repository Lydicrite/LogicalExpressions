using System;
using System.Collections.Generic;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Optimization.Bdd
{
    /// <summary>
    /// Конвертер для преобразования BDD обратно в AST (LogicNode).
    /// </summary>
    public class BDDConverter
    {
        private readonly string[] _variables;
        private readonly Dictionary<BDDNode, LogicNode> _cache = new();

        public BDDConverter(string[] variables)
        {
            _variables = variables;
        }

        /// <summary>
        /// Преобразует узел BDD в логическое выражение (AST).
        /// </summary>
        public LogicNode Convert(BDDNode node)
        {
            _cache.Clear();
            return ConvertInternal(node);
        }

        private LogicNode ConvertInternal(BDDNode node)
        {
            if (_cache.TryGetValue(node, out var cached))
            {
                return cached;
            }

            if (node.IsTerminal)
            {
                return new ConstantNode(node.Value);
            }

            var varName = _variables[node.VarIndex];
            var varNode = new VariableNode(varName, node.VarIndex);
            
            var highExpr = ConvertInternal(node.High!); // Если var = 1
            var lowExpr = ConvertInternal(node.Low!);   // Если var = 0

            // Оптимизации (Shannon expansion: (var & high) | (~var & low))
            
            // 1. Если high == 1, то (var & 1) | ... -> var | ...
            // 2. Если high == 0, то (var & 0) | ... -> 0 | ... -> ...
            // 3. Если low == 1, то ... | (~var & 1) -> ... | ~var
            // 4. Если low == 0, то ... | (~var & 0) -> ... | 0 -> ...

            LogicNode? term1 = null;
            if (highExpr is ConstantNode ch)
            {
                if (ch.Value) term1 = varNode; // var & 1 -> var
                else term1 = null;             // var & 0 -> 0 (skip)
            }
            else
            {
                term1 = new BinaryNode("&", varNode, highExpr);
            }

            LogicNode? term2 = null;
            var notVar = new UnaryNode("~", varNode);
            if (lowExpr is ConstantNode cl)
            {
                if (cl.Value) term2 = notVar; // ~var & 1 -> ~var
                else term2 = null;            // ~var & 0 -> 0 (skip)
            }
            else
            {
                term2 = new BinaryNode("&", notVar, lowExpr);
            }

            LogicNode result;
            if (term1 == null && term2 == null)
            {
                // Оба 0 -> 0 (но BDD Zero должен быть терминальным, сюда не должны попасть теоретически,
                // если только не было редукции)
                result = new ConstantNode(false); 
            }
            else if (term1 == null)
            {
                result = term2!;
            }
            else if (term2 == null)
            {
                result = term1!;
            }
            else
            {
                result = new BinaryNode("|", term1, term2);
            }

            _cache[node] = result;
            return result;
        }
    }
}
