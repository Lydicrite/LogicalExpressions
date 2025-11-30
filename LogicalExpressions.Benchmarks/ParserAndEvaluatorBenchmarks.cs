using BenchmarkDotNet.Attributes;
using LogicalExpressions.Core;
using LogicalExpressions.Parsing;
using System;

namespace LogicalExpressions.Benchmarks
{
    [MemoryDiagnoser]
    public class ParserAndEvaluatorBenchmarks
    {
        private readonly string _simple = "a & b | c";
        private readonly string _medium = "(a & b) | (~c ^ d)";
        private readonly string _complex = "((a => b) & (c | d)) <=> (!e | (f ^ g))";

        private LogicalExpression _exprSimple = null!;
        private LogicalExpression _exprComplex = null!;

        [GlobalSetup]
        public void Setup()
        {
            var simpleNode = ExpressionParser.Parse(_simple);
            _exprSimple = new LogicalExpression(simpleNode);
            _exprSimple = _exprSimple.WithVariableOrder(new[] { "a", "b", "c" });

            var complexNode = ExpressionParser.Parse(_complex);
            _exprComplex = new LogicalExpression(complexNode);
            _exprComplex = _exprComplex.WithVariableOrder(new[] { "a", "b", "c", "d", "e", "f", "g" });
        }

        [Benchmark]
        public void Parse_Simple() => ExpressionParser.Parse(_simple);

        [Benchmark]
        public void Parse_Medium() => ExpressionParser.Parse(_medium);

        [Benchmark]
        public void Parse_Complex() => ExpressionParser.Parse(_complex);

        [Benchmark]
        public bool Evaluate_Simple_True() => _exprSimple.Evaluate(new[] { true, true, false });

        [Benchmark]
        public bool Evaluate_Complex_Mixed() => _exprComplex.Evaluate(new[] { true, false, true, false, true, false, true });

        private bool[][] _batchInputs = null!;

        [GlobalSetup(Target = nameof(EvaluateBatch_Complex))]
        public void SetupBatch()
        {
            Setup();
            int batchSize = 1000;
            _batchInputs = new bool[batchSize][];
            var rnd = new Random(42);
            for (int i = 0; i < batchSize; i++)
            {
                _batchInputs[i] = new bool[7]; // 7 vars in complex
                for (int j = 0; j < 7; j++)
                    _batchInputs[i][j] = rnd.Next(2) == 0;
            }
        }

        [Benchmark]
        public bool EvaluateBatch_Complex() 
        {
            // Поскольку EvaluateBatch больше не является публичным API,
            // эмулируем пакетную обработку через цикл, но с использованием скомпилированного делегата (Evaluate сам скомпилирует)
            bool last = false;
            for(int i = 0; i < _batchInputs.Length; i++)
            {
                last = _exprComplex.Evaluate(_batchInputs[i]);
            }
            return last;
        }
    }
}
