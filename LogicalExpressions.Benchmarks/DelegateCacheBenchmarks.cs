using BenchmarkDotNet.Attributes;
using LogicalExpressions.Core;
using LogicalExpressions.Parsing;
using System.Collections.Generic;

namespace LogicalExpressions.Benchmarks
{
    [MemoryDiagnoser]
    public class DelegateCacheBenchmarks
    {
        [Params(12, 20)]
        public int VarCount { get; set; }

        [Params(3, 4)]
        public int Depth { get; set; }

        private LogicalExpression _expr = null!;

        [GlobalSetup]
        public void Setup()
        {
            var gen = BenchmarkGenerators.GenerateRandomExpressionGeneral(VarCount, maxDepth: Depth, seed: 999 + VarCount * 11 + Depth);
            var node = ExpressionParser.Parse(gen.expr);
            _expr = new LogicalExpression(node);
            _expr = _expr.WithVariableOrder(gen.vars);
            LogicalExpression.ClearDelegateCache();
            LogicalExpression.ConfigureDelegateCache(4096, 20, true, System.TimeSpan.FromSeconds(10));
        }

        [Benchmark(Description = "Compile-Cold")]
        public void Compile_Cold()
        {
            LogicalExpression.ClearDelegateCache();
            _expr.Compile();
        }

        [Benchmark(Description = "Compile-Warm")]
        public void Compile_Warm()
        {
            // кэш уже прогрет в Setup
            _expr.Compile();
        }

        [Benchmark(Description = "Evaluate-Cold")]
        public bool Evaluate_Cold()
        {
            LogicalExpression.ClearDelegateCache();
            var dict = new Dictionary<string, bool>();
            foreach (var v in _expr.Variables) dict[v] = true;
            return _expr.Evaluate(dict);
        }

        [Benchmark(Description = "Evaluate-Warm")]
        public bool Evaluate_Warm()
        {
            var dict = new Dictionary<string, bool>();
            foreach (var v in _expr.Variables) dict[v] = true;
            return _expr.Evaluate(dict);
        }
    }
}
