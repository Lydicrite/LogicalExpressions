using BenchmarkDotNet.Attributes;
using LogicalExpressions.Core;
using LogicalExpressions.Parsing;

namespace LogicalExpressions.Benchmarks
{
    [MemoryDiagnoser]
    public class MinimizationBenchmarks
    {
        public enum SopShape { Sparse, Dense, Clustered }

        [Params(8, 12, 16, 20)]
        public int VarCount { get; set; }

        [Params(SopShape.Sparse, SopShape.Dense, SopShape.Clustered)]
        public SopShape Shape { get; set; }

        private LogicalExpression _expr = null!;

        [GlobalSetup]
        public void Setup()
        {
            string exprStr; string[] vars;
            switch (Shape)
            {
                case SopShape.Sparse:
                    (exprStr, vars) = BenchmarkGenerators.GenerateRandomSop(VarCount, termCount: VarCount, density: 0.35, seed: 100 + VarCount);
                    break;
                case SopShape.Dense:
                    (exprStr, vars) = BenchmarkGenerators.GenerateRandomSop(VarCount, termCount: VarCount * 2, density: 0.65, seed: 200 + VarCount);
                    break;
                default:
                    (exprStr, vars) = BenchmarkGenerators.GenerateClusteredSop(VarCount, extraCount: 3, seed: 300 + VarCount);
                    break;
            }
            var node = ExpressionParser.Parse(exprStr);
            _expr = new LogicalExpression(node);
            _expr = _expr.WithVariableOrder(vars);
        }

        [Benchmark(Description = "Minimize")]
        public LogicalExpression Minimize_BDD() => _expr.Minimize();
    }
}
