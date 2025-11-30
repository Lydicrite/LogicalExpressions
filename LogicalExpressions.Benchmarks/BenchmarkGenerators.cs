using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicalExpressions.Benchmarks
{
    internal static class BenchmarkGenerators
    {
        public static string[] MakeVars(int varCount)
            => Enumerable.Range(0, varCount).Select(i => $"x{i}").ToArray();

        // Генерация случайного терма (произведение) заданной плотности литералов
        public static string GenerateSopTerm(Random rnd, string[] vars, double density)
        {
            var chosen = new List<string>();
            foreach (var v in vars)
            {
                if (rnd.NextDouble() < density)
                {
                    var lit = rnd.Next(2) == 1 ? $"~{v}" : v;
                    chosen.Add(lit);
                }
            }
            if (chosen.Count == 0)
            {
                // гарантируем хотя бы один литерал
                var v = vars[rnd.Next(vars.Length)];
                chosen.Add(rnd.Next(2) == 1 ? $"~{v}" : v);
            }
            return chosen.Count == 1 ? chosen[0] : $"({string.Join(" & ", chosen)})";
        }

        // Случайная СДНФ из termCount термов и плотности литералов
        public static (string expr, string[] vars) GenerateRandomSop(int varCount, int termCount, double density, int seed = 123)
        {
            var rnd = new Random(seed);
            var vars = MakeVars(varCount);
            var terms = new List<string>(termCount);
            var uniq = new HashSet<string>();
            while (terms.Count < termCount)
            {
                var t = GenerateSopTerm(rnd, vars, density);
                if (uniq.Add(t)) terms.Add(t);
            }
            var expr = terms.Count == 1 ? terms[0] : $"({string.Join(" | ", terms)})";
            return (expr, vars);
        }

        // Кластеризованная СДНФ: общие литералы + небольшое разнообразие, хорошо схлопывается Espresso
        public static (string expr, string[] vars) GenerateClusteredSop(int varCount, int extraCount = 3, int seed = 321)
        {
            var rnd = new Random(seed);
            var vars = MakeVars(varCount);
            int commonCount = Math.Max(1, varCount / 2);
            var commonVars = vars.Take(commonCount).ToArray();
            var extraVars = vars.Skip(commonCount).Take(Math.Max(1, extraCount)).ToArray();

            var commonLits = commonVars.Select(v => rnd.Next(2) == 1 ? $"~{v}" : v).ToArray();
            var terms = new List<string>();

            int patterns = 1 << extraVars.Length;
            for (int mask = 0; mask < patterns; mask++)
            {
                var lits = new List<string>(commonLits);
                for (int i = 0; i < extraVars.Length; i++)
                {
                    bool bit = ((mask >> i) & 1) == 1;
                    lits.Add(bit ? extraVars[i] : $"~{extraVars[i]}");
                }
                terms.Add($"({string.Join(" & ", lits)})");
            }

            var expr = $"({string.Join(" | ", terms)})";
            return (expr, vars);
        }

        // Общая генерация произвольного выражения с заданной глубиной
        public static (string expr, string[] vars) GenerateRandomExpressionGeneral(int varCount, int maxDepth, int seed = 777)
        {
            var rnd = new Random(seed);
            var vars = MakeVars(varCount);
            string Gen(int depth)
            {
                if (depth <= 0)
                {
                    var v = vars[rnd.Next(vars.Length)];
                    return rnd.Next(3) == 0 ? $"~{v}" : v;
                }
                // иногда унарный, чаще бинарный
                if (rnd.Next(4) == 0)
                {
                    return $"~({Gen(depth - 1)})";
                }
                var ops = new[] {"&", "|", "^", "=>", "<=>"};
                var op = ops[rnd.Next(ops.Length)];
                var left = Gen(depth - 1);
                var right = Gen(depth - 1);
                return $"({left} {op} {right})";
            }

            var expr = Gen(maxDepth);
            return (expr, vars);
        }
    }
}