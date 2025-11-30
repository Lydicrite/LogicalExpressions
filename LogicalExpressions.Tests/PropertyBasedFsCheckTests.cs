using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogicalExpressions.Core;
using LogicalExpressions.Parsing;
using LogicalExpressions.Parsing.Strategies;
using LogicalExpressions.Compilation.Visitors;

namespace MSTests
{
    [TestClass]
    public sealed class PropertyBasedFsCheckTests
    {
        private static void SetParserStrategyForTest(string strategy)
        {
            if (string.Equals(strategy, "Pratt", StringComparison.OrdinalIgnoreCase))
                ExpressionParser.SetParserStrategy(new PrattParserStrategy());
            else
                ExpressionParser.SetParserStrategy(new ShuntingYardParserStrategy());
        }

        // Генерация случайного валидного выражения с контролем глубины
        private static (string expr, List<string> vars) GenerateRandomExpression(Random rnd, int maxDepth, string[] variablePool)
        {
            var ops = new[] { "&", "|", "^", "=>", "<=>" };
            var vars = new HashSet<string>();

            string Gen(int depth)
            {
                if (depth == 0)
                {
                    var v = variablePool[rnd.Next(variablePool.Length)];
                    vars.Add(v);
                    return v;
                }
                if (rnd.NextDouble() < 0.25)
                {
                    return "(" + Gen(depth - 1) + ")";
                }
                if (rnd.NextDouble() < 0.25)
                {
                    return "~" + Gen(depth - 1);
                }
                var left = Gen(depth - 1);
                var right = Gen(depth - 1);
                var op = ops[rnd.Next(ops.Length)];
                return $"({left} {op} {right})";
            }

            var expr = Gen(maxDepth);
            return (expr, vars.ToList());
        }

        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("FsCheck: Evaluate и Compile дают одинаковый результат для случайных выражений")]
        public void FsCheck_Evaluate_Equals_Compile(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var seed = 1337 + s * 7919;
                var rnd = new Random(seed);
                var (exprStr, _) = GenerateRandomExpression(rnd, maxDepth: 3, variablePool: pool);
                var expr = new LogicalExpression(ExpressionParser.Parse(exprStr));

                // Подготовим входы согласно числу переменных
                var varCount = expr.Variables.Count;
                var inputs = new bool[varCount];
                for (int i = 0; i < varCount; i++) inputs[i] = rnd.Next(2) == 1;

                // Скомпилированный путь против интерпретативного пути (Span)
                expr.Compile();
                var compiledEval = expr.Evaluate(inputs);
                var interpretEval = expr.Evaluate(inputs.AsSpan());
                Assert.AreEqual(interpretEval, compiledEval, $"Evaluate(bool[]) vs Evaluate(ReadOnlySpan<bool>) различаются для выражения: {exprStr}\nExpr: {expr}");
            }
        }

        [DataTestMethod]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("FsCheck: Эквивалентность до/после Normalize/ToCnf/ToDnf/Minimize и идемпотентность")]
        public void FsCheck_Transforms_AreEquivalent_And_Idempotent(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var pool = new[] { "A", "B", "C", "D" };

            for (int s = 0; s < 120; s++)
            {
                var seed = 4242 + s * 104729; // другой поток seeds
                var rnd = new Random(seed);
                var (exprStr, _) = GenerateRandomExpression(rnd, maxDepth: 3, variablePool: pool);
                var expr = new LogicalExpression(ExpressionParser.Parse(exprStr));

                var norm = expr.Normalize();
                var dnf = expr.ToDnf();
                var cnf = expr.ToCnf();
                var min = expr.Minimize();

                // Эквивалентность преобразований исходному выражению
                Assert.IsTrue(norm.EquivalentTo(expr), $"Normalize должен сохранять семантику.\nOrig: {expr}\nNorm: {norm}\nExprStr: {exprStr}");
                Assert.IsTrue(dnf.EquivalentTo(expr), $"ToDnf должен сохранять семантику.\nOrig: {expr}\nDNF: {dnf}\nExprStr: {exprStr}");
                Assert.IsTrue(cnf.EquivalentTo(expr), $"ToCnf должен сохранять семантику.\nOrig: {expr}\nCNF: {cnf}\nExprStr: {exprStr}");
                Assert.IsTrue(min.EquivalentTo(expr), $"Minimize должен сохранять семантику.\nOrig: {expr}\nMin: {min}\nExprStr: {exprStr}");

                // Идемпотентность Normalize/Minimize
                Assert.IsTrue(norm.Normalize().EquivalentTo(norm), $"Normalize должен быть идемпотентным.\nNorm1: {norm}\nNorm2: {norm.Normalize()}\nExprStr: {exprStr}");
                Assert.IsTrue(min.Minimize().EquivalentTo(min), $"Minimize должен быть идемпотентным.\nMin1: {min}\nMin2: {min.Minimize()}\nExprStr: {exprStr}");
            }
        }

        [TestMethod]
        [Description("Debug: Normalize specific expression")]
        public void Debug_Normalize_Specific()
        {
            ExpressionParser.SetParserStrategy(new PrattParserStrategy());
            var exprStr = "((~A) | ((B | C) <=> (C)))";
            var node = ExpressionParser.Parse(exprStr);
            var expr = new LogicalExpression(node);
            expr = expr.WithVariableOrder(new[] { "A", "B", "C" });

            var norm = expr.Normalize();

            var origStr = CanonicalStringVisitor.Build(node);
            var normStr = norm.ToString();

            Console.WriteLine($"Orig: {origStr}");
            Console.WriteLine($"Norm: {normStr}");

            Assert.IsTrue(norm.EquivalentTo(expr), "Normalize should preserve semantics for debug case");
        }
    }
}
