using BenchmarkDotNet.Attributes;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Parsing;
using LogicalExpressions.Parsing.Strategies;

namespace LogicalExpressions.Benchmarks
{
    [MemoryDiagnoser]
    public class ParserStrategyBenchmarks
    {
        [Params(12, 20)]
        public int VarCount { get; set; }

        [Params(3, 4)]
        public int Depth { get; set; }

        private string _exprStr = string.Empty;

        [GlobalSetup]
        public void Setup()
        {
            var gen = BenchmarkGenerators.GenerateRandomExpressionGeneral(VarCount, maxDepth: Depth, seed: 888 + VarCount * 7 + Depth);
            _exprStr = gen.expr;
        }

        [Benchmark(Description = "Parse-ShuntingYard")]
        public LogicNode Parse_ShuntingYard()
        {
            ExpressionParser.SetParserStrategy(new ShuntingYardParserStrategy());
            return ExpressionParser.Parse(_exprStr);
        }

        [Benchmark(Description = "Parse-Pratt")]
        public LogicNode Parse_Pratt()
        {
            ExpressionParser.SetParserStrategy(new PrattParserStrategy());
            return ExpressionParser.Parse(_exprStr);
        }

        [Benchmark(Description = "Parse-WithOptions-ShuntingYard")]
        public LogicNode Parse_WithOptions_ShuntingYard()
        {
            var options = new LEParserOptions { Strategy = ParserStrategy.ShuntingYard };
            return ExpressionParser.Parse(_exprStr, options);
        }

        [Benchmark(Description = "Parse-WithOptions-Pratt")]
        public LogicNode Parse_WithOptions_Pratt()
        {
            var options = new LEParserOptions { Strategy = ParserStrategy.Pratt };
            return ExpressionParser.Parse(_exprStr, options);
        }
    }
}
