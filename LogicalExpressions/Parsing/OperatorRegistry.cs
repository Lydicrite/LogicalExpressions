using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LogicalExpressions.Core.Nodes;

namespace LogicalExpressions.Parsing
{
    public class OperatorRegistry
    {
        private ImmutableDictionary<string, int> _operatorPrecedence;
        private ImmutableHashSet<string> _rightAssociative;
        private ImmutableDictionary<string, Func<LogicNode, LogicNode>> _unaryOperators;
        private ImmutableDictionary<string, Func<LogicNode, LogicNode, LogicNode>> _binaryOperators;
        private ImmutableDictionary<string, string> _operatorAliases;
        private ImmutableDictionary<string, string> _constantAliases;

        // Кэш списка кандидатов операторов (обновляется при изменении опций)
        private List<string>? _operatorCandidates;
        private bool _operatorCandidatesValid;
        private List<string>? _unaryNotAliases;
        private List<string>? _impliesAliases;

        public OperatorRegistryOptions Options { get; private set; }

        public OperatorRegistry() : this(OperatorRegistryOptions.Default)
        {
        }

        public OperatorRegistry(OperatorRegistryOptions options)
        {
            Options = options ?? OperatorRegistryOptions.Default;

            _operatorPrecedence = Options.OperatorPrecedence;
            _rightAssociative = Options.RightAssociative;
            _unaryOperators = Options.UnaryOperators;
            _binaryOperators = Options.BinaryOperators;
            _operatorAliases = Options.OperatorAliases;
            _constantAliases = Options.ConstantAliases;
        }

        public void SetOptions(OperatorRegistryOptions options)
        {
            Options = options ?? OperatorRegistryOptions.Default;
            _operatorPrecedence = Options.OperatorPrecedence;
            _rightAssociative = Options.RightAssociative;
            _unaryOperators = Options.UnaryOperators;
            _binaryOperators = Options.BinaryOperators;
            _operatorAliases = Options.OperatorAliases;
            _constantAliases = Options.ConstantAliases;
            _operatorCandidatesValid = false;
        }

        public IReadOnlyDictionary<string, int> OperatorPrecedence => _operatorPrecedence;
        public IReadOnlySet<string> RightAssociative => _rightAssociative;
        public IReadOnlyDictionary<string, Func<LogicNode, LogicNode>> UnaryOperators => _unaryOperators;
        public IReadOnlyDictionary<string, Func<LogicNode, LogicNode, LogicNode>> BinaryOperators => _binaryOperators;
        public IReadOnlyDictionary<string, string> OperatorAliases => _operatorAliases;
        public IReadOnlyDictionary<string, string> ConstantAliases => _constantAliases;

        public void RegisterUnary(string symbol, int precedence, bool rightAssociative, Func<LogicNode, LogicNode> factory)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            _unaryOperators = _unaryOperators.SetItem(symbol, factory).WithComparers(comparer);
            _operatorPrecedence = _operatorPrecedence.SetItem(symbol, precedence).WithComparers(comparer);
            _rightAssociative = rightAssociative
                ? _rightAssociative.Add(symbol).WithComparer(comparer)
                : _rightAssociative.Remove(symbol).WithComparer(comparer);
            _operatorCandidatesValid = false;
        }

        public void RegisterBinary(string symbol, int precedence, bool rightAssociative, Func<LogicNode, LogicNode, LogicNode> factory)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            _binaryOperators = _binaryOperators.SetItem(symbol, factory).WithComparers(comparer);
            _operatorPrecedence = _operatorPrecedence.SetItem(symbol, precedence).WithComparers(comparer);
            _rightAssociative = rightAssociative
                ? _rightAssociative.Add(symbol).WithComparer(comparer)
                : _rightAssociative.Remove(symbol).WithComparer(comparer);
            _operatorCandidatesValid = false;
        }

        public void RegisterOperatorAlias(string alias, string canonical)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            _operatorAliases = _operatorAliases.SetItem(alias, canonical).WithComparers(comparer);
            _operatorCandidatesValid = false;
        }

        /// <summary>
        /// Регистрирует алиас константы.
        /// </summary>
        public void RegisterConstantAlias(string alias, string canonical)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            _constantAliases = _constantAliases.SetItem(alias, canonical).WithComparers(comparer);
        }

        /// <summary>
        /// Возвращает кэшированный список кандидатов операторов (алиасы + канонические символы),
        /// отсортированный по длине убыв.
        /// </summary>
        public IReadOnlyList<string> GetOperatorCandidates()
        {
            if (_operatorCandidatesValid && _operatorCandidates != null)
                return _operatorCandidates;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in _operatorPrecedence.Keys) set.Add(k);
            foreach (var k in _operatorAliases.Keys) set.Add(k);
            _operatorCandidates = set.OrderByDescending(k => k.Length).ToList();
            _operatorCandidatesValid = true;
            return _operatorCandidates;
        }

        /// <summary>
        /// Возвращает список алиасов для унарного оператора НЕ.
        /// </summary>
        public IReadOnlyList<string> GetUnaryAliasesForNot()
        {
            if (_unaryNotAliases != null) return _unaryNotAliases;
            var comparer = StringComparer.OrdinalIgnoreCase;
            _unaryNotAliases = _operatorAliases
                .Where(kvp => string.Equals(kvp.Value, "~", StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .Distinct(comparer)
                .OrderByDescending(s => s.Length)
                .ToList();
            return _unaryNotAliases;
        }

        /// <summary>
        /// Возвращает список алиасов для оператора импликации.
        /// </summary>
        public IReadOnlyList<string> GetImpliesAliases()
        {
            if (_impliesAliases != null) return _impliesAliases;
            var comparer = StringComparer.OrdinalIgnoreCase;
            _impliesAliases = _operatorAliases
                .Where(kvp => string.Equals(kvp.Value, "=>", StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .Append("=>")
                .Distinct(comparer)
                .OrderByDescending(s => s.Length)
                .ToList();
            return _impliesAliases;
        }
    }
}
