using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Buffers;
using LogicalExpressions.Utils;

namespace LogicalExpressions.Parsing.Tokenization
{
    /// <summary>
    /// Токенизатор логических выражений.
    /// Разбивает входную строку на последовательность токенов.
    /// </summary>
    public class Tokenizer(OperatorRegistry registry)
    {
        private readonly OperatorRegistry _registry = registry;

        /// <summary>
        /// Выполняет токенизацию выражения.
        /// </summary>
        /// <param name="expression">Входное выражение.</param>
        /// <param name="enableSuggestions">Включить генерацию предложений для неизвестных токенов.</param>
        /// <returns>Список токенов.</returns>
        public List<Token> Tokenize(string expression, bool enableSuggestions = true)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(expression)) return tokens;

            if (_registry.Options.EnableUnicodeNormalization)
            {
                expression = expression.Normalize(NormalizationForm.FormKC);
            }

            int i = 0;
            ReadOnlySpan<char> span = expression.AsSpan();
            var operatorCandidates = _registry.GetOperatorCandidates();
            var unaryAliases = _registry.GetUnaryAliasesForNot();

            while (i < span.Length)
            {
                char ch = span[i];

                if (char.IsWhiteSpace(ch))
                {
                    i++;
                    continue;
                }

                if (ch == '(')
                {
                    tokens.Add(new Token(TokenType.LParen, "(", i));
                    i++;
                    continue;
                }
                
                if (ch == ')')
                {
                    tokens.Add(new Token(TokenType.RParen, ")", i));
                    i++;
                    continue;
                }
                
                if (ch == '0')
                {
                    tokens.Add(new Token(TokenType.Constant, "0", i));
                    i++;
                    continue;
                }
                
                if (ch == '1')
                {
                    tokens.Add(new Token(TokenType.Constant, "1", i));
                    i++;
                    continue;
                }

                if (ch == '<' && i + 3 <= span.Length && span.Slice(i, 3).SequenceEqual("<=>"))
                {
                    tokens.Add(new Token(TokenType.Operator, "<=>", i));
                    i += 3;
                    continue;
                }

                if (char.IsLetter(ch))
                {
                    int start = i;
                    i++;
                    while (i < span.Length && (char.IsLetterOrDigit(span[i]) || span[i] == '_'))
                        i++;

                    bool prefixedUnaryMatched = false;
                    foreach (var alias in unaryAliases)
                    {
                        int len = alias.Length;
                        if (start + len <= span.Length && span.Slice(start, len).Equals(alias.AsSpan(), StringComparison.OrdinalIgnoreCase))
                        {
                            tokens.Add(new Token(TokenType.Operator, "~", start));
                            i = start + len;
                            prefixedUnaryMatched = true;
                            break;
                        }
                    }

                    if (prefixedUnaryMatched)
                    {
                        continue;
                    }

                    string ident = new string(span.Slice(start, i - start));
                    
                    if (_registry.OperatorAliases.TryGetValue(ident, out var canonicalOp)
                        && _registry.OperatorPrecedence.ContainsKey(canonicalOp))
                    {
                        tokens.Add(new Token(TokenType.Operator, canonicalOp, start));
                    }
                    else if (_registry.ConstantAliases.TryGetValue(ident, out var canonicalConst))
                    {
                        tokens.Add(new Token(TokenType.Constant, canonicalConst, start));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Identifier, ident, start));
                    }
                    continue;
                }

                string? matched = null;
                string? canonical = null;
                
                foreach (var cand in operatorCandidates)
                {
                    int len = cand.Length;
                    if (i + len <= span.Length && span.Slice(i, len).Equals(cand.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        matched = cand;
                        canonical = _registry.OperatorAliases.TryGetValue(cand, out var canon) ? canon : cand;
                        break;
                    }
                }

                if (matched != null)
                {
                    tokens.Add(new Token(TokenType.Operator, canonical!, i));
                    i += matched.Length;
                    continue;
                }

                int errorStart = i;
                int end = i + 1;
                while (end < span.Length)
                {
                    char c = span[end];
                    if (char.IsWhiteSpace(c) || char.IsLetterOrDigit(c) || c == '(' || c == ')')
                        break;
                    end++;
                }
                string fragment = new string(span.Slice(errorStart, Math.Max(1, end - errorStart)));
                var suggestions = enableSuggestions ? SuggestOperatorAliases(fragment) : new List<string>();
                int? charCode = fragment.Length == 1 ? fragment[0] : (int?)null;
                throw new ParseException(
                    ParseErrorCode.UnknownToken,
                    $"Недопустимый символ/оператор '{fragment}'",
                    tokens.Count,
                    expression,
                    errorStart,
                    end,
                    charCode,
                    fragment,
                    tokenCode: null,
                    suggestions: suggestions
                );
            }

            return tokens;
        }

        private List<string> SuggestOperatorAliases(string fragment)
        {
            fragment = fragment.Trim();
            if (string.IsNullOrEmpty(fragment)) return new List<string>();

            var candidates = _registry.OperatorAliases.Keys
                .Concat(_registry.OperatorPrecedence.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var scored = new List<(string alias, int dist)>();
            
            foreach (var cand in candidates)
            {
                int d = LevenshteinDistance(fragment, cand);
                scored.Add((cand, d));
            }

            int threshold = _registry.Options.SuggestionMaxDistance;
            int maxItems = _registry.Options.SuggestionMaxItems;
            return scored
                .OrderBy(t => t.dist)
                .ThenBy(t => t.alias, StringComparer.OrdinalIgnoreCase)
                .Where(t => t.dist <= threshold)
                .Take(maxItems)
                .Select(t => t.alias)
                .ToList();
        }

        private static int LevenshteinDistance(ReadOnlySpan<char> s, ReadOnlySpan<char> t)
        {
             int n = s.Length;
             int m = t.Length;

             if (n == 0) return m;
             if (m == 0) return n;

             int[]? rentedArray = null;
             Span<int> v0 = (m + 1) <= 256 ? stackalloc int[m + 1] : (rentedArray = ArrayPool<int>.Shared.Rent(m + 1));
             
             try 
             {
                 for (int j = 0; j <= m; j++) v0[j] = j;

                 for (int i = 0; i < n; i++)
                 {
                     int current = i + 1;
                     int previousDiagonal = i; 

                     int left = current;
                     int diagonal = i;
                     
                     int prevV0_0 = v0[0];
                     v0[0] = i + 1;
                     int diagonalVal = prevV0_0;

                     for (int j = 0; j < m; j++)
                     {
                         int upper = v0[j + 1];
                         int cost = (char.ToLowerInvariant(s[i]) == char.ToLowerInvariant(t[j])) ? 0 : 1;
                         
                         int newVal = Math.Min(Math.Min(upper + 1, v0[j] + 1), diagonalVal + cost);
                         
                         diagonalVal = upper;
                         v0[j + 1] = newVal;
                     }
                 }
                 return v0[m];
             }
             finally
             {
                 if (rentedArray != null) ArrayPool<int>.Shared.Return(rentedArray);
             }
        }
    }
}
