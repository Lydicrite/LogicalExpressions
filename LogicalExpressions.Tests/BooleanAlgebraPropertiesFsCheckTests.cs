using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogicalExpressions.Core;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Parsing;
using LogicalExpressions.Parsing.Strategies;
using LogicalExpressions.Compilation.Visitors;

namespace MSTests
{
    [TestClass]
    public sealed class BooleanAlgebraPropertiesFsCheckTests
    {
        private static void SetParserStrategyForTest(string strategy)
        {
            if (string.Equals(strategy, "Pratt", StringComparison.OrdinalIgnoreCase))
                ExpressionParser.SetParserStrategy(new PrattParserStrategy());
            else
                ExpressionParser.SetParserStrategy(new ShuntingYardParserStrategy());
        }

        private static LogicalExpression Reindex(LogicalExpression expr, LogicalExpression other)
        {
            var order = expr.Variables.Union(other.Variables).OrderBy(x => x);
            return expr.WithVariableOrder(order);
        }

        private static LogicNode BuildUnaryChain(int n, LogicNode inner)
        {
            var node = inner;
            for (int i = 0; i < n; i++) node = new UnaryNode("~", node);
            return node;
        }

        private static LogicNode GenerateRandomNode(Random rnd, int maxDepth, string[] variablePool)
        {
            LogicNode Gen(int depth)
            {
                if (depth == 0)
                {
                    if (rnd.NextDouble() < 0.3)
                        return new ConstantNode(rnd.Next(2) == 1);
                    var v = variablePool[rnd.Next(variablePool.Length)];
                    return new VariableNode(v);
                }

                // Вероятность унарной цепочки
                if (rnd.NextDouble() < 0.25)
                {
                    var inner = Gen(depth - 1);
                    var len = 1 + rnd.Next(Math.Min(3, depth + 1));
                    return BuildUnaryChain(len, inner);
                }

                var left = Gen(depth - 1);
                var right = Gen(depth - 1);
                var ops = new[] { "&", "|", "^", "=>", "<=>" };
                var op = ops[rnd.Next(ops.Length)];
                return new BinaryNode(op, left, right);
            }
            return Gen(maxDepth);
        }

        // Коммутативность для &, |, ^
        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property: Коммутативность бинарных операторов &, |, ^ (рандом LogicNode)")]
        public void Property_Commutativity_And_Or_Xor(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var rnd = new Random(20240501 + s * 7919);
                var a = GenerateRandomNode(rnd, 3, pool);
                var b = GenerateRandomNode(rnd, 3, pool);

                bool Check(string op)
                {
                    var left = new LogicalExpression(new BinaryNode(op, a, b));
                    var right = new LogicalExpression(new BinaryNode(op, b, a));
                    left = Reindex(left, right);
                    right = Reindex(right, left);
                    return left.EquivalentTo(right);
                }

                Assert.IsTrue(Check("&"));
                Assert.IsTrue(Check("|"));
                Assert.IsTrue(Check("^"));
            }
        }

        // Ассоциативность для &, |, ^
        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property: Ассоциативность бинарных операторов &, |, ^ (рандом LogicNode)")]
        public void Property_Associativity_And_Or_Xor(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var rnd = new Random(20240502 + s * 7919);
                var a = GenerateRandomNode(rnd, 3, pool);
                var b = GenerateRandomNode(rnd, 3, pool);
                var c = GenerateRandomNode(rnd, 3, pool);

                bool Check(string op)
                {
                    var left = new LogicalExpression(new BinaryNode(op, a, new BinaryNode(op, b, c)));
                    var right = new LogicalExpression(new BinaryNode(op, new BinaryNode(op, a, b), c));
                    left = Reindex(left, right);
                    right = Reindex(right, left);
                    return left.EquivalentTo(right);
                }

                Assert.IsTrue(Check("&"));
                Assert.IsTrue(Check("|"));
                Assert.IsTrue(Check("^"));
            }
        }

        // Дистрибутивность: a & (b | c) == (a & b) | (a & c), и a | (b & c) == (a | b) & (a | c)
        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property: Закон дистрибутивности для &, | (рандом LogicNode)")]
        public void Property_Distributivity(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var rnd = new Random(20240503 + s * 7919);
                var a = GenerateRandomNode(rnd, 3, pool);
                var b = GenerateRandomNode(rnd, 3, pool);
                var c = GenerateRandomNode(rnd, 3, pool);

                var left1 = new LogicalExpression(new BinaryNode("&", a, new BinaryNode("|", b, c)));
                var right1 = new LogicalExpression(new BinaryNode("|", new BinaryNode("&", a, b), new BinaryNode("&", a, c)));

                var left2 = new LogicalExpression(new BinaryNode("|", a, new BinaryNode("&", b, c)));
                var right2 = new LogicalExpression(new BinaryNode("&", new BinaryNode("|", a, b), new BinaryNode("|", a, c)));

                left1 = Reindex(left1, right1); right1 = Reindex(right1, left1);
                left2 = Reindex(left2, right2); right2 = Reindex(right2, left2);

                Assert.IsTrue(left1.EquivalentTo(right1));
                Assert.IsTrue(left2.EquivalentTo(right2));
            }
        }

        // Законы де Моргана: ~(a & b) == ~a | ~b; ~(a | b) == ~a & ~b
        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property: Законы де Моргана (рандом LogicNode)")]
        public void Property_DeMorgan(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var rnd = new Random(20240504 + s * 7919);
                var a = GenerateRandomNode(rnd, 3, pool);
                var b = GenerateRandomNode(rnd, 3, pool);

                var left1 = new LogicalExpression(new UnaryNode("~", new BinaryNode("&", a, b)));
                var right1 = new LogicalExpression(new BinaryNode("|", new UnaryNode("~", a), new UnaryNode("~", b)));

                var left2 = new LogicalExpression(new UnaryNode("~", new BinaryNode("|", a, b)));
                var right2 = new LogicalExpression(new BinaryNode("&", new UnaryNode("~", a), new UnaryNode("~", b)));

                left1 = Reindex(left1, right1); right1 = Reindex(right1, left1);
                left2 = Reindex(left2, right2); right2 = Reindex(right2, left2);

                Assert.IsTrue(left1.EquivalentTo(right1));
                Assert.IsTrue(left2.EquivalentTo(right2));
            }
        }

        // Поглощение: a | (a & b) == a; a & (a | b) == a
        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property: Закон поглощения для &, | (рандом LogicNode)")]
        public void Property_Absorption(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var rnd = new Random(20240505 + s * 7919);
                var a = GenerateRandomNode(rnd, 3, pool);
                var b = GenerateRandomNode(rnd, 3, pool);

                var left1 = new LogicalExpression(new BinaryNode("|", a, new BinaryNode("&", a, b)));
                var right1 = new LogicalExpression(a);

                var left2 = new LogicalExpression(new BinaryNode("&", a, new BinaryNode("|", a, b)));
                var right2 = new LogicalExpression(a);

                left1 = Reindex(left1, right1); right1 = Reindex(right1, left1);
                left2 = Reindex(left2, right2); right2 = Reindex(right2, left2);

                Assert.IsTrue(left1.EquivalentTo(right1));
                Assert.IsTrue(left2.EquivalentTo(right2));
            }
        }

        // Идемпотентность: a & a == a; a | a == a; a ^ a == 0
        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property: Идемпотентность для &, | и поведение XOR (рандом LogicNode)")]
        public void Property_Idempotency_And_Xor(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var rnd = new Random(20240506 + s * 7919);
                var a = GenerateRandomNode(rnd, 3, pool);

                var andLeft = new LogicalExpression(new BinaryNode("&", a, a));
                var andRight = new LogicalExpression(a);

                var orLeft = new LogicalExpression(new BinaryNode("|", a, a));
                var orRight = new LogicalExpression(a);

                var xorLeft = new LogicalExpression(new BinaryNode("^", a, a));
                var xorRight = new LogicalExpression(new ConstantNode(false));

                andLeft = Reindex(andLeft, andRight); andRight = Reindex(andRight, andLeft);
                orLeft = Reindex(orLeft, orRight); orRight = Reindex(orRight, orLeft);
                xorLeft = Reindex(xorLeft, xorRight); xorRight = Reindex(xorRight, xorLeft);

                Assert.IsTrue(andLeft.EquivalentTo(andRight));
                Assert.IsTrue(orLeft.EquivalentTo(orRight));
                Assert.IsTrue(xorLeft.EquivalentTo(xorRight));
            }
        }

        // Round-trip: Canonical(ToString(Parse(expr))) == Canonical(expr)
        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property: Инвариант round-trip Parse → ToString семантически эквивалентен исходному (RAND LogicNode)")]
        public void Property_Canonical_RoundTrip(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            LogicNode GenerateWithoutImpEqXor(Random rnd, int maxDepth, string[] variablePool)
            {
                LogicNode Gen(int depth)
                {
                    if (depth == 0)
                    {
                        if (rnd.NextDouble() < 0.3)
                            return new ConstantNode(rnd.Next(2) == 1);
                        var v = variablePool[rnd.Next(variablePool.Length)];
                        return new VariableNode(v);
                    }

                    if (rnd.NextDouble() < 0.25)
                    {
                        var inner = Gen(depth - 1);
                        var len = 1 + rnd.Next(Math.Min(3, depth + 1));
                        return BuildUnaryChain(len, inner);
                    }

                    var left = Gen(depth - 1);
                    var right = Gen(depth - 1);
                    var ops = new[] { "&", "|" };
                    var op = ops[rnd.Next(ops.Length)];
                    return new BinaryNode(op, left, right);
                }
                return Gen(maxDepth);
            }

            for (int s = 0; s < 120; s++)
            {
                var rnd = new Random(20240507 + s * 7919);
                var node = GenerateWithoutImpEqXor(rnd, 3, pool);

                var expr1 = new LogicalExpression(node);
                var round = expr1.ToString();
                var node2 = ExpressionParser.Parse(round);
                var expr2 = new LogicalExpression(node2);

                expr1 = Reindex(expr1, expr2);
                expr2 = Reindex(expr2, expr1);

                Assert.IsTrue(expr1.EquivalentTo(expr2), "Parse(ToString(expr)) должен быть семантически эквивалентен исходному");
            }
        }
    }
}
