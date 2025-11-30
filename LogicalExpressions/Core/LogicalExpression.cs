using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using LogicalExpressions.Core.Nodes;
using LogicalExpressions.Compilation;
using LogicalExpressions.Compilation.Visitors;
using LogicalExpressions.Optimization.Bdd;
using LogicalExpressions.Utils;

namespace LogicalExpressions.Core
{
    [NotThreadSafe]
    public sealed class LogicalExpression : IEquatable<LogicalExpression>
    {
        private readonly LogicNode _root;
        private readonly ImmutableArray<string> _variables;
        private readonly CompilationOptions _compilationOptions;
        
        // Cache for compiled delegate
        private Func<bool[], bool>? _compiledDelegate;
        
        // Delegate cache (static)
        private static readonly ConcurrentDictionary<string, Func<bool[], bool>> _delegateCache = new(StringComparer.Ordinal);
        private static int _delegateMaxCacheSize = 2048;
        private static int _delegateEvictPercent = 10;

        /// <summary>
        /// Создает новое логическое выражение из заданного корневого узла.
        /// Переменные автоматически собираются и сортируются по алфавиту.
        /// </summary>
        public LogicalExpression(LogicNode root)
        {
            ArgumentNullException.ThrowIfNull(root);
            _root = root;
            var vars = new HashSet<string>(StringComparer.Ordinal);
            _root.CollectVariables(vars);
            _variables = vars.OrderBy(v => v, StringComparer.Ordinal).ToImmutableArray();
            _compilationOptions = new CompilationOptions();
            
            // Index the variables in the AST
            var indices = new Dictionary<string, int>();
            for (int i = 0; i < _variables.Length; i++)
            {
                indices[_variables[i]] = i;
            }
            var indexer = new VariableIndexerVisitor(indices);
            _root = indexer.ProcessNode(_root);
        }

        private LogicalExpression(LogicNode root, ImmutableArray<string> variables, CompilationOptions options)
        {
            _root = root;
            _variables = variables;
            _compilationOptions = options;
            
            // Ensure indices are correct if variables changed (though usually we pass matching root)
            var indices = new Dictionary<string, int>();
            for (int i = 0; i < _variables.Length; i++)
            {
                indices[_variables[i]] = i;
            }
            var indexer = new VariableIndexerVisitor(indices);
            _root = indexer.ProcessNode(_root);
        }

        /// <summary>
        /// Список переменных выражения.
        /// </summary>
        public IReadOnlyList<string> Variables => _variables;

        /// <summary>
        /// Создает новое выражение с заданным порядком переменных.
        /// Позволяет переупорядочить переменные или добавить новые (суперсет).
        /// </summary>
        public LogicalExpression WithVariableOrder(IEnumerable<string> variables)
        {
            var newVars = variables.ToImmutableArray();
            
            if (newVars.Distinct(StringComparer.Ordinal).Count() != newVars.Length)
                 throw new ArgumentException("Обнаружены дубликаты переменных");

            var providedVars = new HashSet<string>(newVars, StringComparer.Ordinal);
            if (!_variables.All(v => providedVars.Contains(v)))
                 throw new ArgumentException("Предоставленные переменные не содержат всех переменных выражения");

            return new LogicalExpression(_root, newVars, _compilationOptions);
        }

        /// <summary>
        /// Создает новое выражение с заданными опциями компиляции.
        /// </summary>
        public LogicalExpression WithCompilationOptions(CompilationOptions options)
        {
            return new LogicalExpression(_root, _variables, options);
        }

        /// <summary>
        /// Вычисляет значение выражения для заданного набора входных значений.
        /// </summary>
        public bool Evaluate(bool[] inputs)
        {
            if (inputs.Length != _variables.Length)
                throw new ArgumentException($"Ожидалось {_variables.Length} входов, получено {inputs.Length}");

            if (_compiledDelegate != null)
                return _compiledDelegate(inputs);

            // Lazy compilation to satisfy performance tests expecting caching
            Compile();
            return _compiledDelegate!(inputs);
        }

        /// <summary>
        /// Вычисляет значение выражения, используя <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        public bool Evaluate(ReadOnlySpan<bool> inputs)
        {
             if (inputs.Length != _variables.Length)
                throw new ArgumentException($"Ожидалось {_variables.Length} входов, получено {inputs.Length}");
            
            return _root.Evaluate(inputs);
        }

        /// <summary>
        /// Вычисляет значение выражения, используя словарь значений переменных.
        /// </summary>
        public bool Evaluate(IDictionary<string, bool> inputs)
        {
            ArgumentNullException.ThrowIfNull(inputs);
            var bools = new bool[_variables.Length];
            for (int i = 0; i < _variables.Length; i++)
            {
                if (!inputs.TryGetValue(_variables[i], out bool val))
                    throw new ArgumentException($"Отсутствует значение для переменной '{_variables[i]}'");
                bools[i] = val;
            }
            return Evaluate(bools);
        }

        /// <summary>
        /// Компилирует выражение в делегат для быстрого вычисления.
        /// </summary>
        public void Compile()
        {
            if (_compiledDelegate != null) return;

            string cacheKey = GetDelegateCacheKey();
            if (_delegateCache.TryGetValue(cacheKey, out var del))
            {
                _compiledDelegate = del;
                return;
            }

            var param = Expression.Parameter(typeof(bool[]), "inputs");
            var visitor = new ExpressionBuilderVisitor(param);
            _root.Accept(visitor);
            var body = visitor.GetResult();
            
            var lambda = Expression.Lambda<Func<bool[], bool>>(body, param);
            _compiledDelegate = lambda.Compile();
            
            EnsureDelegateCacheCapacity();
            _delegateCache[cacheKey] = _compiledDelegate;
        }
        
        private string GetDelegateCacheKey()
        {
            var sb = new StringBuilder();
            sb.Append("SC=").Append(_compilationOptions.UseShortCircuiting ? "1" : "0").Append('|');
            sb.Append(CanonicalStringVisitor.Build(_root));
            sb.Append('|');
            foreach(var v in _variables)
            {
                sb.Append(v).Append(',');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Возвращает нормализованную версию выражения.
        /// </summary>
        public LogicalExpression Normalize()
        {
            var visitor = new NormalizerVisitor();
            var newRoot = visitor.Normalize(_root);
            return new LogicalExpression(newRoot, _variables, _compilationOptions);
        }
        
        /// <summary>
        /// Возвращает минимизированную версию выражения.
        /// Использует BDD для канонизации и упрощения.
        /// </summary>
        public LogicalExpression Minimize()
        {
            var bdd = BuildBdd();
            var converter = new BDDConverter(_variables.ToArray());
            var simplifiedNode = converter.Convert(bdd);
            
            // Дополнительно прогоняем через Normalizer для устранения лишних скобок и констант
            var visitor = new NormalizerVisitor();
            simplifiedNode = visitor.Normalize(simplifiedNode);

            return new LogicalExpression(simplifiedNode, _variables, _compilationOptions);
        }
        
        /// <summary>
        /// Преобразует выражение в конъюнктивную нормальную форму (КНФ).
        /// </summary>
        public LogicalExpression ToCnf() => Normalize(); // Placeholder

        /// <summary>
        /// Преобразует выражение в дизъюнктивную нормальную форму (ДНФ).
        /// </summary>
        public LogicalExpression ToDnf() => Normalize(); // Placeholder

        /// <summary>
        /// Проверяет, является ли выражение тавтологией (всегда истинным).
        /// </summary>
        public bool IsTautology()
        {
            var bdd = BuildBdd();
            return bdd == BddManager.One;
        }

        /// <summary>
        /// Проверяет, является ли выражение противоречием (всегда ложным).
        /// </summary>
        public bool IsContradiction()
        {
             var bdd = BuildBdd();
             return bdd == BddManager.Zero;
        }

        /// <summary>
        /// Проверяет, является ли выражение выполнимым (существует хотя бы один набор значений, при котором оно истинно).
        /// </summary>
        public bool IsSatisfiable()
        {
             var bdd = BuildBdd();
             return bdd != BddManager.Zero;
        }

        private BDDNode BuildBdd()
        {
            var manager = new BddManager();
            var varMap = new Dictionary<string, int>();
            for(int i=0; i<_variables.Length; i++) varMap[_variables[i]] = i;
            
            var visitor = new BddBuilderVisitor(manager, varMap);
            _root.Accept(visitor);
            return visitor.Result;
        }

        public bool StructuralEquals(LogicalExpression other)
        {
            if (other is null) return false;
            return _root.Equals(other._root);
        }

        public bool EquivalentTo(LogicalExpression other)
        {
             if (other is null) return false;
             
             // Use BDD for equivalence check
             // Ensure variable mapping is consistent
             var allVars = new HashSet<string>(_variables);
             allVars.UnionWith(other._variables);
             var sortedVars = allVars.OrderBy(v => v, StringComparer.Ordinal).ToList();
             var varMap = sortedVars.Select((v, i) => (v, i)).ToDictionary(x => x.v, x => x.i);
             
             var manager = new BddManager();
             
             var v1 = new BddBuilderVisitor(manager, varMap);
             _root.Accept(v1);
             
             var v2 = new BddBuilderVisitor(manager, varMap);
             other._root.Accept(v2);
             
             return v1.Result == v2.Result;
        }

        public override bool Equals(object? obj) => Equals(obj as LogicalExpression);

        public bool Equals(LogicalExpression? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return StructuralEquals(other);
        }

        public override int GetHashCode()
        {
            return _root.GetHashCode();
        }

        public override string ToString()
        {
            return CanonicalStringVisitor.Build(_root);
        }
        
        /// <summary>
        /// Строит и возвращает строковое представление таблицы истинности выражения.
        /// </summary>
        public string PrintTruthTable()
        {
            var sb = new StringBuilder();
            // Header
            foreach(var v in _variables) sb.Append(v).Append('\t');
            sb.AppendLine("Result");
            
            int count = 1 << _variables.Length;
            bool[] inputs = new bool[_variables.Length];
            for(int i=0; i<count; i++)
            {
                for(int j=0; j<_variables.Length; j++)
                {
                    bool val = (i & (1 << (_variables.Length - 1 - j))) != 0;
                    inputs[j] = val;
                    sb.Append(val ? "1" : "0").Append('\t');
                }
                sb.Append(Evaluate(inputs) ? "1" : "0").AppendLine();
            }
            return sb.ToString();
        }

        // Static Cache Management
        public static void ConfigureDelegateCache(int maxSize, int evictPercent, bool enableTtl, TimeSpan ttl)
        {
            _delegateMaxCacheSize = maxSize;
            _delegateEvictPercent = evictPercent;
        }
        
        public static void ClearDelegateCache() => _delegateCache.Clear();
        public static int DelegateCacheSize => _delegateCache.Count;

        private static void EnsureDelegateCacheCapacity()
        {
             if (_delegateCache.Count >= _delegateMaxCacheSize)
             {
                 _delegateCache.Clear(); // Simple eviction
             }
        }
    }
}
