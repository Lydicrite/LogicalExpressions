using BenchmarkDotNet.Attributes;
using LogicalExpressions.Core;
using LogicalExpressions.Parsing;

namespace LogicalExpressions.Benchmarks
{
    [MemoryDiagnoser]
    public class NormalizationBenchmarks
    {
        [Params(8, 12, 16)]
        public int VarCount { get; set; }

        [Params(2, 3, 4)]
        public int Depth { get; set; }

        private LogicalExpression _expr = null!;

        [GlobalSetup]
        public void Setup()
        {
            var gen = BenchmarkGenerators.GenerateRandomExpressionGeneral(VarCount, maxDepth: Depth, seed: 555 + VarCount * 10 + Depth);
            var node = ExpressionParser.Parse(gen.expr);
            _expr = new LogicalExpression(node);
            _expr = _expr.WithVariableOrder(gen.vars);
        }

        [Benchmark(Description = "ToDNF")]
        public LogicalExpression ToDNF() => _expr.ToDnf();

        [Benchmark(Description = "ToCNF")]
        public LogicalExpression ToCNF() => _expr.ToCnf();

        [Benchmark(Description = "Normalize")]
        public LogicalExpression Normalize() => _expr.Normalize();

        [Benchmark(Description = "Minimize")]
        public LogicalExpression Minimize_BDD() => _expr.Minimize();
    }
}
