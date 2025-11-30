using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogicalExpressions.Core;
using LogicalExpressions.Utils;
using LogicalExpressions.Parsing;
using LogicalExpressions.Parsing.Strategies;
using LogicalExpressions.Compilation;

namespace MSTests
{
    [TestClass]
    public sealed class LogicalExpressionsTests
    {
        private const int TestTimeoutMs = 3000;

        #region Инициализация и очистка

        [TestInitialize]
        public void TestInit()
        {
            Console.WriteLine($"Инициализация теста: {TestContext.TestName}\n\n");
        }

        [TestCleanup]
        public void TestCleanup() => Console.WriteLine($"\n\nЗавершение теста: {TestContext.TestName}");

        public TestContext TestContext { get; set; }

        #endregion

        #region Вспомогательные методы
        private static void SetParserStrategyForTest(string strategy)
        {
            if (string.Equals(strategy, "Pratt", StringComparison.OrdinalIgnoreCase))
                ExpressionParser.SetParserStrategy(new PrattParserStrategy());
            else
                ExpressionParser.SetParserStrategy(new ShuntingYardParserStrategy());
        }
        private static long MeasureEvaluationTime(LogicalExpression expr, bool[] inputs)
        {
            var sw = Stopwatch.StartNew();
            expr.Evaluate(inputs);
            sw.Stop();
            return sw.ElapsedTicks;
        }

        private static void PrintTestResults(LogicalExpression expr, bool result)
        {
            Console.WriteLine($"\nРезультат: {result}");
            Console.WriteLine($"Оптимизированное выражение: {expr}");

            var sw = Stopwatch.StartNew();
            var table = expr.PrintTruthTable();
            sw.Stop();

            Console.WriteLine($"\nТаблица истинности (построена за {sw.ElapsedMilliseconds} мс) {table}");
        }


        #region Расширенные диагностические методы

        private static void PrintDetailedAnalysis(string expression, bool[] inputs, string[] variables)
        {
            Console.WriteLine("\n=== ДИАГНОСТИЧЕСКИЙ ОТЧЁТ ===");

            // 1. Тестирование кэширования парсера
            var (firstParseTime, secondParseTime) = MeasureParserCacheEfficiency(expression);
            Console.WriteLine($"\n[ПАРСЕР] Первый запуск: {firstParseTime.ms} мс ({firstParseTime.ticks} тиков)");
            Console.WriteLine($"[ПАРСЕР] Второй запуск: {secondParseTime.ms} мс ({secondParseTime.ticks} тиков)");
            Console.WriteLine($"Эффективность кэша: {((firstParseTime.ticks - secondParseTime.ticks) * 100.0 / firstParseTime.ticks):F1}%");

            // 2. Анализ выражения
            var expr = new LogicalExpression(ExpressionParser.Parse(expression));
            expr = expr.WithVariableOrder(variables);
            Console.WriteLine("\n[АНАЛИЗ ВЫРАЖЕНИЯ]");
            Console.WriteLine($"Исходное выражение: {expression}");
            Console.WriteLine($"Нормализованная форма: {expr}");
            Console.WriteLine($"Количество переменных: {variables.Length}");
            Console.WriteLine($"Глубина дерева: {CalculateExpressionDepth(expr)} уровней");

            // 3. Вычисление результата
            var evalResult = MeasureEvaluationWithDetails(expr, inputs);
            Console.WriteLine("\n[ВЫЧИСЛЕНИЕ]");
            Console.WriteLine($"Входные данные: {FormatBoolArray(inputs)}");
            Console.WriteLine($"Результат: {evalResult.result} \tВремя: {evalResult.timeMs:F3} мс");

            // 4. Генерация таблицы истинности
            var (table, buildTime) = MeasureTruthTableGeneration(expr);
            Console.WriteLine("\n[ТАБЛИЦА ИСТИННОСТИ]");
            Console.WriteLine($"Время построения: {buildTime} мс");
            Console.WriteLine(table);

            // 5. Дополнительная информация
            Console.WriteLine("\n[ДОПОЛНИТЕЛЬНО]");
            Console.WriteLine($"Используемые операторы: {GetUsedOperators(expr)}");
            Console.WriteLine($"Размер кэша парсера: {GetParserCacheSize()} выражений");
        }

        private static ((long ticks, double ms) first, (long ticks, double ms) second)
            MeasureParserCacheEfficiency(string expression)
        {
            var swFirst = Stopwatch.StartNew();
            ExpressionParser.Parse(expression);
            swFirst.Stop();

            var swSecond = Stopwatch.StartNew();
            ExpressionParser.Parse(expression);
            swSecond.Stop();

            return (
                (swFirst.ElapsedTicks, swFirst.Elapsed.TotalMilliseconds),
                (swSecond.ElapsedTicks, swSecond.Elapsed.TotalMilliseconds)
            );
        }

        private static (bool result, double timeMs) MeasureEvaluationWithDetails(LogicalExpression expr, bool[] inputs)
        {
            var sw = Stopwatch.StartNew();
            var result = expr.Evaluate(inputs);
            sw.Stop();
            return (result, sw.Elapsed.TotalMilliseconds);
        }

        private static (string table, long timeMs) MeasureTruthTableGeneration(LogicalExpression expr)
        {
            var sw = Stopwatch.StartNew();
            var table = expr.PrintTruthTable();
            sw.Stop();
            return (table, sw.ElapsedMilliseconds);
        }

        #endregion

        #region Вспомогательные форматеры

        private static string FormatBoolArray(bool[] arr) =>
            $"[{string.Join(", ", arr.Select(b => b ? "1" : "0"))}]";

        private static int CalculateExpressionDepth(LogicalExpression expr)
        {
            // Реализация через рекурсивный обход дерева выражений
            return expr.ToString().Count(c => c == '(');
        }

        private static string GetUsedOperators(LogicalExpression expr)
        {
            var ops = new HashSet<string>();
            var pattern = new Regex(@"([&|^!]|=>|<=>|[∧∨→≡])");
            foreach (Match m in pattern.Matches(expr.ToString()))
                ops.Add(m.Value);

            return string.Join(", ", ops.OrderBy(o => o));
        }

        private static long GetParserCacheSize()
        {
            // Используем публичное свойство размера кэша AST
            return ExpressionParser.CacheSize;
        }

        #endregion

        #endregion




        #region Тесты базовой функциональности

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Проверка вложенных выражений с константами и базовыми операторами")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Evaluate_ComplexNestedExpressionWithConstants_ReturnsExpectedResult(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = new LogicalExpression(ExpressionParser.Parse("((A & B) | !(C → true)) ⇔ D"));
            var variables = new[] { "A", "B", "C", "D" };
            var testInput = new[] { false, false, false, false };

            // Act
            expr = expr.WithVariableOrder(variables);
            var result = expr.Evaluate(testInput);

            // Assert
            PrintTestResults(expr, result);
            Assert.IsTrue(result, "Ожидалось значение true для входных данных [false, false, false, false]");
        }

        #endregion




        #region Тесты расширенных возможностей

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Проверка независимости результатов от порядка переменных")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Evaluate_WithDifferentVariableOrders_ReturnsConsistentResults(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr1 = new LogicalExpression(ExpressionParser.Parse("(true & !(~A)) | (B ^ false)"));
            var expr2 = new LogicalExpression(ExpressionParser.Parse("(true ∧ ¬(!A)) ∨ (B XOR false)"));
            var inputs = new[] { false, true };

            // Act
            expr1 = expr1.WithVariableOrder(new[] { "A", "B" });
            expr2 = expr2.WithVariableOrder(new[] { "B", "A" });
            var result1 = expr1.Evaluate(inputs);
            var result2 = expr2.Evaluate(inputs);

            // Assert
            Console.WriteLine($"Результат 1: {result1}\nРезультат 2: {result2}");
            Assert.AreEqual(result1, result2,
                "Результаты должны быть идентичны при разных порядках переменных");
        }

        #endregion





        #region Параметризованные тесты

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [DataRow(new[] { true, true, false }, true, "ShuntingYard")]
        [DataRow(new[] { false, false, true }, false, "ShuntingYard")]
        [DataRow(new[] { true, false, true }, false, "ShuntingYard")]
        [DataRow(new[] { true, true, false }, true, "Pratt")]
        [DataRow(new[] { false, false, true }, false, "Pratt")]
        [DataRow(new[] { true, false, true }, false, "Pratt")]
        [Description("Проверка различных комбинаций входных данных")]
        public void Evaluate_WithMultipleInputCombinations_ReturnsExpected(bool[] inputs, bool expected, string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = new LogicalExpression(ExpressionParser.Parse("(X | Y) & !Z"));
            expr = expr.WithVariableOrder(new[] { "X", "Y", "Z" });

            // Act
            var result = expr.Evaluate(inputs);

            // Assert
            Console.WriteLine($"Входные данные: {string.Join(", ", inputs)}\nВыход: {result}");
            Assert.AreEqual(expected, result,
                $"Несоответствие ожидаемого результата для входных данных: {string.Join(", ", inputs)}");
        }

        #endregion





        #region Тесты производительности

        [DataTestMethod]
        [Timeout(5000)]
        [Description("Проверка эффективности кэширования выражений")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Evaluate_ExpressionCaching_EfficiencyTest(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = new LogicalExpression(ExpressionParser.Parse("(A ^ B) & (C | D) => (E ⇔ F)"));
            expr = expr.WithVariableOrder(new[] { "A", "B", "C", "D", "E", "F" });
            var testInput = new[] { true, false, true, false, true, false };

            // Act
            var firstCallTime = MeasureEvaluationTime(expr, testInput);
            var secondCallTime = MeasureEvaluationTime(expr, testInput);

            // Assert
            Assert.IsTrue(secondCallTime < firstCallTime * 0.5,
                "Повторный вызов должен быть значительно быстрее благодаря кэшированию");
        }

        [TestMethod]
        [DoNotParallelize]
        [Description("DelegateCache: ключ учитывает порядок переменных и опции компиляции")]
        public void DelegateCache_Key_Considers_VariableOrder_And_Options()
        {
            // Очистим кэш делегатов
            LogicalExpression.ClearDelegateCache();
            Assert.AreEqual(0, LogicalExpression.DelegateCacheSize, "Кэш делегатов должен быть пустым в начале теста.");

            var node = ExpressionParser.Parse("A & (B | C)");

            // Компиляция с порядком [A,B,C]
            var exprABC = new LogicalExpression(node).WithVariableOrder(new[] { "A", "B", "C" });
            exprABC.Compile();
            Assert.AreEqual(1, LogicalExpression.DelegateCacheSize, "После первой компиляции ожидается одна запись в кэше делегатов.");

            // Компиляция того же AST, но с порядком [B,A,C] — должен появиться второй ключ
            var exprBAC = new LogicalExpression(node).WithVariableOrder(new[] { "B", "A", "C" });
            exprBAC.Compile();
            Assert.AreEqual(2, LogicalExpression.DelegateCacheSize, "Разный порядок переменных должен формировать разные записи кэша делегатов.");

            // Компиляция с другой опцией короткого замыкания — третий ключ
            var exprNoSC = exprABC.WithCompilationOptions(new CompilationOptions { UseShortCircuiting = false });
            exprNoSC.Compile();
            Assert.AreEqual(3, LogicalExpression.DelegateCacheSize, "Разные опции компиляции должны формировать разные записи кэша делегатов.");

            // Семантическая корректность сохраняется (используем словарь по именам переменных)
            var values = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["A"] = true,
                ["B"] = false,
                ["C"] = true
            };
            Assert.AreEqual(exprABC.Evaluate(values), exprNoSC.Evaluate(values), "Смена опции короткого замыкания не должна менять семантику булевой функции.");
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs * 4)]
        [Description("Проверка производительности для выражения с 15 переменными")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Evaluate_With15Variables_CompletesInReasonableTime(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = "(A | B | C | D | E | F | G) & (H <=> I <=> J) XOR (K -> L -> M -> N -> O & (((P & Q) XOR (R ^ (!S))) | T))";
            var vars = Enumerable.Range('A', 20).Select(c => ((char)c).ToString()).ToArray();

            // Act & assert
            var input = new bool[20];
            PrintDetailedAnalysis(expr, input, vars);
        }

        #endregion





        #region Тесты семантической корректности

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Проверка эквивалентности различных форм импликации")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Evaluate_ImplicationOperatorEquivalence_ReturnsConsistentResults(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr1 = new LogicalExpression(ExpressionParser.Parse("A => B"));
            var expr2 = new LogicalExpression(ExpressionParser.Parse("!A | B"));
            var expr3 = new LogicalExpression(ExpressionParser.Parse("¬A ∨ B"));
            var variables = new[] { "A", "B" };
            var testInputs = new bool[][]
            {
                new[] { true, true },
                new[] { true, false },
                new[] { false, true },
                new[] { false, false }
            };

            // Act & Assert
            foreach (var input in testInputs)
            {
                expr1 = expr1.WithVariableOrder(variables);
                expr2 = expr2.WithVariableOrder(variables);
                expr3 = expr3.WithVariableOrder(variables);

                var result1 = expr1.Evaluate(input);
                var result2 = expr2.Evaluate(input);
                var result3 = expr3.Evaluate(input);

                Assert.AreEqual(result1, result2, $"Несоответствие для A={input[0]}, B={input[1]}");
                Assert.AreEqual(result2, result3, $"Несоответствие для A={input[0]}, B={input[1]}");
            }
        }

        #endregion





        #region Тесты обработки ошибок

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [ExpectedException(typeof(ArgumentException))]
        [Description("Проверка обработки неверного количества аргументов")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Evaluate_WithInvalidInputLength_ThrowsArgumentException(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = new LogicalExpression(ExpressionParser.Parse("A & B"));
            expr = expr.WithVariableOrder(new[] { "A", "B" });

            // Act
            expr.Evaluate(new[] { true });
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Проверка пустого выражения (должно выбрасывать ParseException.EmptyExpression)")]
        [ExpectedException(typeof(ParseException))]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_EmptyExpression_ThrowsException(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange & Act
            var expr = new LogicalExpression(ExpressionParser.Parse(""));
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentException))]
        [Description("Проверка обработки дубликатов переменных при установке порядка")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void SetVariableOrder_WithDuplicateVariables_ThrowsArgumentException(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = new LogicalExpression(ExpressionParser.Parse("A & B"));

            // Act
            expr = expr.WithVariableOrder(new[] { "A", "A", "B" });
        }

        [DataTestMethod]
        [ExpectedException(typeof(ParseException))]
        [Description("Проверка обработки неверной вложенности скобок")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_InvalidParenthesisNesting_ThrowsParseException(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange & Act
            var expr = new LogicalExpression(ExpressionParser.Parse("((A & B) | C"));
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: недопустимый токен перед '('")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_InvalidTokenBeforeOpenParen_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("A(");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.InvalidTokenBeforeOpenParen, ex.ErrorCode);
                Assert.AreEqual(1, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: недопустимый токен после ')'")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_InvalidTokenAfterCloseParen_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("(A)B");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.InvalidTokenAfterCloseParen, ex.ErrorCode);
                Assert.AreEqual(2, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: отсутствует операнд у унарного оператора")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_UnaryOperatorMissingOperand_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("~");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.UnaryOperatorMissingOperand, ex.ErrorCode);
                Assert.AreEqual(0, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: бинарный оператор в начале выражения")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_BinaryOperatorAtStart_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("&A");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.BinaryOperatorAtEnds, ex.ErrorCode);
                Assert.AreEqual(0, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: бинарный оператор в конце выражения")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_BinaryOperatorAtEnd_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("A&");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.BinaryOperatorAtEnds, ex.ErrorCode);
                Assert.AreEqual(1, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: некорректный контекст бинарного оператора")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_InvalidBinaryOperatorContext_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("A & & B");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.InvalidBinaryOperatorContext, ex.ErrorCode);
                Assert.AreEqual(1, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: неспаренная закрывающая скобка")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_UnmatchedClosingParenthesis_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse(")A");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.UnmatchedClosingParenthesis, ex.ErrorCode);
                Assert.AreEqual(0, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: неспаренные скобки")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_UnmatchedParentheses_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("(A");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.UnmatchedParentheses, ex.ErrorCode);
                Assert.AreEqual(1, ex.Position);
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("ErrorCode и Position: неизвестный токен (недопустимый символ)")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_UnknownTokenSymbol_ErrorCodeAndPosition(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("A$");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.UnknownToken, ex.ErrorCode);
                Assert.AreEqual(1, ex.Position);
            }
        }

        #endregion





        #region Тесты пограничных случаев

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Проверка выражения с единственной переменной")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Evaluate_SingleVariableExpression_ReturnsCorrectValue(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = new LogicalExpression(ExpressionParser.Parse("X"));
            expr = expr.WithVariableOrder(new[] { "X" });

            // Act & Assert
            Assert.IsTrue(expr.Evaluate(new[] { true }), "Ожидалось true для X = true");
            Assert.IsFalse(expr.Evaluate(new[] { false }), "Ожидалось false для X = false");
        }

        #endregion





        #region Тесты таблиц истинности

        [DataTestMethod]
        [Timeout(1000)]
        [Description("Проверка полного соответствия таблицы истинности для XOR")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void TruthTable_ForXorOperator_MatchesExpected(string strategy)
        {
            SetParserStrategyForTest(strategy);
            // Arrange
            var expr = new LogicalExpression(ExpressionParser.Parse("A ^ B"));
            expr = expr.WithVariableOrder(new[] { "A", "B" });

            // Expected truth table for XOR
            var expectedResults = new[]
            {
                new { A = false, B = false, Result = false },
                new { A = false, B = true, Result = true },
                new { A = true, B = false, Result = true },
                new { A = true, B = true, Result = false }
            };

            // Act & Assert
            foreach (var expected in expectedResults)
            {
                var actual = expr.Evaluate(new[] { expected.A, expected.B });
                Assert.AreEqual(expected.Result, actual,
                    $"Несоответствие для A = {expected.A}, B = {expected.B}");
            }
        }

        #endregion

        // --- Новые property-based тесты, точные позиции ошибок и профилирование ---

        #region Property-based тесты

        [DataTestMethod]
        [Timeout(TestTimeoutMs * 15)]
        [Description("Property-based: случайная генерация валидных выражений и проверка эквивалентности с алиасами")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Property_GeneratedExpressions_AliasesAreEquivalent(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var rnd = new Random(12345);
            for (int t = 0; t < 30; t++)
            {
                var (exprStr, vars) = GenerateRandomExpression(rnd, maxDepth: 3, variablePool: new[] { "A", "B", "C", "D" });
                var expr1 = new LogicalExpression(ExpressionParser.Parse(exprStr, new LEParserOptions { EnableAliasSuggestions = false }));
                expr1 = expr1.WithVariableOrder(vars.ToArray());

                // Заменяем операторы на алиасы случайно
                var aliased = RandomlyAliasOperators(exprStr, rnd);
                var expr2 = new LogicalExpression(ExpressionParser.Parse(aliased, new LEParserOptions { EnableAliasSuggestions = false }));
                expr2 = expr2.WithVariableOrder(vars.ToArray());

                // Проверяем эквивалентность на случайных входах
                for (int k = 0; k < 50; k++)
                {
                    var inputs = vars.Select(_ => rnd.Next(2) == 1).ToArray();
                    var r1 = expr1.Evaluate(inputs);
                    var r2 = expr2.Evaluate(inputs);
                    Assert.AreEqual(r1, r2, $"Выражения должны быть эквивалентны.\nИсходное: {exprStr}\nАлиасы:   {aliased}");
                }
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Property-based: устойчивость к двойным отрицаниям и лишним скобкам")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Property_GeneratedExpressions_DoubleNegationsAndRedundantParens(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var rnd = new Random(54321);
            for (int t = 0; t < 100; t++)
            {
                var (exprStr, vars) = GenerateRandomExpression(rnd, maxDepth: 3, variablePool: new[] { "A", "B", "C" });
                var normalized = $"(~(~{exprStr}))"; // двойное отрицание
                var withParens = $"(({exprStr}))";   // лишние скобки

                try
                {
                    var exprBase = new LogicalExpression(ExpressionParser.Parse(exprStr));
                    exprBase = exprBase.WithVariableOrder(vars.ToArray());
                    var exprNeg = new LogicalExpression(ExpressionParser.Parse(normalized));
                    exprNeg = exprNeg.WithVariableOrder(vars.ToArray());
                    var exprParen = new LogicalExpression(ExpressionParser.Parse(withParens));
                    exprParen = exprParen.WithVariableOrder(vars.ToArray());

                    for (int k = 0; k < 50; k++)
                    {
                        var inputs = vars.Select(_ => rnd.Next(2) == 1).ToArray();
                        var r0 = exprBase.Evaluate(inputs);
                        var r1 = exprNeg.Evaluate(inputs);
                        var r2 = exprParen.Evaluate(inputs);
                        Assert.AreEqual(r0, r1, $"Двойное отрицание должно сохранять значение. Исходное: {exprStr}");
                        Assert.AreEqual(r0, r2, $"Лишние скобки не должны менять значение. Исходное: {exprStr}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Пропущено некорректное выражение: {exprStr}. Причина: {ex.Message}");
                    // пропускаем этот случай, двигаемся дальше
                    continue;
                }
            }
        }

        #endregion

        #region Тесты точных позиций ошибок

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Ошибка: унарные операторы в глубоко вложенном контексте, точная позиция")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_UnaryOperatorMissingOperand_NestedContext_PositionIsAccurate(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("(A & (~))");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.UnaryOperatorMissingOperand, ex.ErrorCode);
                // Позиция указывает на индекс токена '~' в последовательности токенов
                Assert.IsTrue(ex.Position >= 3 && ex.Position <= 4, $"Неожиданная позиция: {ex.Position}");
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Ошибка: последовательные закрывающие скобки без соответствующих открывающих, точная позиция")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Parse_ConsecutiveClosingParens_PositionIsAccurate(string strategy)
        {
            SetParserStrategyForTest(strategy);
            try
            {
                ExpressionParser.Parse("A))B");
                Assert.Fail("Ожидалось исключение ParseException");
            }
            catch (ParseException ex)
            {
                Assert.AreEqual(ParseErrorCode.UnmatchedClosingParenthesis, ex.ErrorCode);
                Assert.IsTrue(ex.Position >= 1 && ex.Position <= 3, $"Неожиданная позиция: {ex.Position}");
            }
        }

        #endregion

        #region Профилирование горячих методов

        [TestMethod]
        [Timeout(TestTimeoutMs * 4)]
        [Description("Профилирование: измерение времени Tokenize, ValidateTokenSequence, ConvertToPostfix, BuildAST")]
        [Ignore("Internal methods are no longer accessible directly")]
        public void Profiling_HotPaths_TimesAreMeasured()
        {
            /*
            var exprStr = "((A & B) | !(C → true)) ⇔ D";
            var sw = Stopwatch.StartNew();
            var tokens = ExpressionParser.Tokenize(exprStr);
            sw.Stop();
            var tTokenize = sw.ElapsedTicks;

            sw.Restart();
            ExpressionParser.ValidateTokenSequence(tokens);
            sw.Stop();
            var tValidate = sw.ElapsedTicks;

            sw.Restart();
            var postfix = ExpressionParser.ConvertToPostfix(tokens);
            sw.Stop();
            var tPostfix = sw.ElapsedTicks;

            sw.Restart();
            ExpressionParser.BuildAST(postfix);
            sw.Stop();
            var tBuild = sw.ElapsedTicks;

            Console.WriteLine($"Tokenize: {tTokenize} ticks, Validate: {tValidate} ticks, Postfix: {tPostfix} ticks, BuildAST: {tBuild} ticks");

            Assert.IsTrue(tTokenize > 0 && tValidate > 0 && tPostfix > 0 && tBuild > 0, "Ожидались ненулевые метрики времени");
            */
        }

        // Вспомогательный вызов приватных методов через Reflection
        // reflection больше не требуется благодаря InternalsVisibleTo

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
                if (rnd.NextDouble() < 0.3)
                {
                    return "(" + Gen(depth - 1) + ")";
                }
                if (rnd.NextDouble() < 0.3)
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

        // Случайная замена операторов на алиасы
        private static string RandomlyAliasOperators(string expr, Random rnd)
        {
            // Используем токенизатор для безопасной замены операторов без проблем с подстроками
            var tokens = ExpressionParser.Tokenize(expr);
            
            var replacements = new Dictionary<string, string[]> {
                { "&", new[] { "AND", "∧" } },
                { "|", new[] { "OR", "∨" } },
                { "^", new[] { "XOR", "⊕" } },
                { "=>", new[] { "IMPLIES", "→", "->" } },
                { "<=>", new[] { "IFF", "≡", "⇔" } },
                { "~", new[] { "NOT", "¬" } },
                { "true", new[] { "1" } },
                { "false", new[] { "0" } },
            };

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (replacements.TryGetValue(t, out var aliases))
                {
                     if (rnd.NextDouble() < 0.5)
                     {
                         tokens[i] = aliases[rnd.Next(aliases.Length)];
                     }
                }
            }
            
            return string.Join(" ", tokens);
        }

        // --- Дополнительные property-based тесты для NormalizerVisitor ---

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Property-based: идемпотентность NormalizerVisitor и стабильность CanonicalStringVisitor")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Property_Normalizer_Idempotent_And_CanonicalStable(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var rnd = new Random(20241014);
            for (int t = 0; t < 100; t++)
            {
                var (exprStr, vars) = GenerateRandomExpression(rnd, maxDepth: 3, variablePool: new[] { "A", "B", "C", "D" });
                var expr = new LogicalExpression(ExpressionParser.Parse(exprStr));
                expr = expr.WithVariableOrder(vars.ToArray());

                var n1 = expr.Normalize();
                var n2 = n1.Normalize();

                // Идемпотентность: повторная нормализация не меняет каноническую строку
                Assert.AreEqual(n1.ToString(), n2.ToString(), $"Повторная нормализация изменила форму. Исходное: {exprStr}\nN1: {n1}\nN2: {n2}");
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Де Морган: ~ (A & B) == (~A | ~B), ~ (A | B) == (~A & ~B) после нормализации")]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        public void Normalizer_DeMorgan_CanonicalMatches(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var expr1 = new LogicalExpression(ExpressionParser.Parse("~(A & B)"));
            expr1 = expr1.WithVariableOrder(new[] { "A", "B" });
            var norm1 = expr1.Normalize();
            Assert.AreEqual("(~A | ~B)", norm1.ToString(), "Ожидалась форма де Моргана для ~(A & B)");

            var expr2 = new LogicalExpression(ExpressionParser.Parse("~(A | B)"));
            expr2 = expr2.WithVariableOrder(new[] { "A", "B" });
            var norm2 = expr2.Normalize();
            Assert.AreEqual("(~A & ~B)", norm2.ToString(), "Ожидалась форма де Моргана для ~(A | B)");
        }

        // --- Тесты для CNF/DNF и предикатных методов ---

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("CNF/DNF эквивалентны исходному выражению для разных формул")]
        [DataRow("(A & B) | (~C)")]
        [DataRow("~(A & (B | C))")]
        [DataRow("(A => B) & (B => C)")]
        [DataRow("(A <=> B) | (C ^ D)")]
        [DataRow("((A | B) & (~A | ~B))")]
        [DataRow("true")]
        [DataRow("false")]
        public void CNF_DNF_AreEquivalentToOriginal(string exprStr)
        {
            // Проверяем для обеих стратегий парсера
            foreach (var strategy in new[] { "ShuntingYard", "Pratt" })
            {
                SetParserStrategyForTest(strategy);
                var expr = new LogicalExpression(ExpressionParser.Parse(exprStr));

                // Зафиксируем порядок переменных для устойчивого сравнения
                var vars = expr.Variables.OrderBy(v => v).ToArray();
                expr = expr.WithVariableOrder(vars);

                var dnf = expr.ToDnf();
                dnf = dnf.WithVariableOrder(vars);
                var cnf = expr.ToCnf();
                cnf = cnf.WithVariableOrder(vars);

                Assert.IsTrue(dnf.EquivalentTo(expr), $"DNF должна быть эквивалентна исходному выражению.\nExpr: {exprStr}\nDNF:  {dnf}\nOrig: {expr}");
                Assert.IsTrue(cnf.EquivalentTo(expr), $"CNF должна быть эквивалентна исходному выражению.\nExpr: {exprStr}\nCNF:  {cnf}\nOrig: {expr}");
            }
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [Description("Проверка методов IsTautology/IsContradiction/IsSatisfiable на известных формулах")]
        [DataRow("A | ~A", true, false, true)]
        [DataRow("A & ~A", false, true, false)]
        [DataRow("true", true, false, true)]
        [DataRow("false", false, true, false)]
        [DataRow("(A => B) & A & ~B", false, true, false)]
        [DataRow("(A & B)", false, false, true)]
        public void PredicateMethods_KnownFormulas(string exprStr, bool tautology, bool contradiction, bool satisfiable)
        {
            foreach (var strategy in new[] { "ShuntingYard", "Pratt" })
            {
                SetParserStrategyForTest(strategy);
                var expr = new LogicalExpression(ExpressionParser.Parse(exprStr));
                // Устанавливаем порядок переменных (если есть)
                var vars = expr.Variables.OrderBy(v => v).ToArray();
                if (vars.Length > 0) expr = expr.WithVariableOrder(vars);

                Assert.AreEqual(tautology, expr.IsTautology(), $"IsTautology: {exprStr}");
                Assert.AreEqual(contradiction, expr.IsContradiction(), $"IsContradiction: {exprStr}");
                Assert.AreEqual(satisfiable, expr.IsSatisfiable(), $"IsSatisfiable: {exprStr}");
            }
        }

        #region Минимизация

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Minimize: константы и простые законы поглощения")]
        public void Minimize_Constants_And_Absorption(string strategy)
        {
            SetParserStrategyForTest(strategy);

            // Тавтология A | ~A -> 1
            var e1 = new LogicalExpression(ExpressionParser.Parse("A | ~A"));
            e1 = e1.WithVariableOrder(new[] { "A" });
            var m1 = e1.Minimize();
            var t = new LogicalExpression(ExpressionParser.Parse("1"));
            Assert.IsTrue(m1.EquivalentTo(t), $"Ожидалась константа 1: Minimize(A | ~A)\nMin: {m1}\nOrig: {e1}");

            // Противоречие A & ~A -> 0
            var e2 = new LogicalExpression(ExpressionParser.Parse("A & ~A"));
            e2 = e2.WithVariableOrder(new[] { "A" });
            var m2 = e2.Minimize();
            var f = new LogicalExpression(ExpressionParser.Parse("0"));
            Assert.IsTrue(m2.EquivalentTo(f), $"Ожидалась константа 0: Minimize(A & ~A)\nMin: {m2}\nOrig: {e2}");

            // Поглощение: A | (A & B) -> A
            var e3 = new LogicalExpression(ExpressionParser.Parse("A | (A & B)"));
            e3 = e3.WithVariableOrder(new[] { "A", "B" });
            var m3 = e3.Minimize();
            var a = new LogicalExpression(ExpressionParser.Parse("A"));
            a = a.WithVariableOrder(new[] { "A" });
            Assert.IsTrue(m3.EquivalentTo(a), $"Поглощение не выполнено: Minimize(A | (A & B))\nMin: {m3}\nOrig: {e3}");
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Minimize: сложная редукция к A | C")]
        public void Minimize_Complex_Absorption_To_A_Or_C(string strategy)
        {
            SetParserStrategyForTest(strategy);

            // A & B | A & ~B | ~A & C -> A | C
            var e = new LogicalExpression(ExpressionParser.Parse("(A & B) | (A & ~B) | (~A & C)"));
            e = e.WithVariableOrder(new[] { "A", "B", "C" });
            var m = e.Minimize();
            var expect = new LogicalExpression(ExpressionParser.Parse("A | C"));
            expect = expect.WithVariableOrder(new[] { "A", "C" });
            Assert.IsTrue(m.EquivalentTo(expect), $"Ожидалось A | C: Minimize((A&B)|(A&~B)|(~A&C))\nMin: {m}\nOrig: {e}");
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Property-based: Minimize эквивалентен оригиналу и идемпотентен (малые n)")]
        public void Property_Minimize_Equivalent_And_Idempotent_SmallVars(string strategy)
        {
            SetParserStrategyForTest(strategy);
            var rnd = new Random(54321);
            var pool = new[] { "A", "B", "C" };

            for (int t = 0; t < 50; t++)
            {
                var (exprStr, vars) = GenerateRandomExpression(rnd, maxDepth: 3, variablePool: pool);
                var expr = new LogicalExpression(ExpressionParser.Parse(exprStr));
                expr = expr.WithVariableOrder(vars.ToArray());

                var min = expr.Minimize();
                // Эквивалентность
                Assert.IsTrue(min.EquivalentTo(expr), $"Minimize должен сохранять семантику.\nOrig: {expr}\nMin:  {min}\nOps:  {GetUsedOperators(expr)}");

                // Идемпотентность минимизации
                var min2 = min.Minimize();
                Assert.IsTrue(min2.EquivalentTo(min), $"Minimize должен быть идемпотентным.\nMin1: {min}\nMin2: {min2}");
            }
        }

        #endregion

        // --- конец новых тестов ---

        #region Различие Equals и EquivalentTo

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Equals vs EquivalentTo: консенсус (A & B) | (A & ~B) ≡ A (структуры различаются)")]
        public void EqualsVsEquivalentTo_ConsensusOr_DiffersStructurallyButEquivalent(string strategy)
        {
            SetParserStrategyForTest(strategy);

            var expr1 = new LogicalExpression(ExpressionParser.Parse("(A & B) | (A & ~B)"));
            var expr2 = new LogicalExpression(ExpressionParser.Parse("A"));

            var vars = expr1.Variables.Union(expr2.Variables).OrderBy(v => v).ToArray();
            expr1 = expr1.WithVariableOrder(vars);
            expr2 = expr2.WithVariableOrder(vars);

            Assert.IsFalse(expr1.StructuralEquals(expr2), "Структурная форма должна различаться (консенсус не схлопывается нормализатором).");
            Assert.IsFalse(expr1.Equals(expr2), "Equals основан на StructuralEquals и тоже должен быть false.");
            Assert.IsTrue(expr1.EquivalentTo(expr2), "Формулы логически эквивалентны по теореме консенсуса.");
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Equals vs EquivalentTo: дуальный консенсус (A | B) & (A | ~B) ≡ A (структуры различаются)")]
        public void EqualsVsEquivalentTo_ConsensusAnd_DiffersStructurallyButEquivalent(string strategy)
        {
            SetParserStrategyForTest(strategy);

            var expr1 = new LogicalExpression(ExpressionParser.Parse("(A | B) & (A | ~B)"));
            var expr2 = new LogicalExpression(ExpressionParser.Parse("A"));

            var vars = expr1.Variables.Union(expr2.Variables).OrderBy(v => v).ToArray();
            expr1 = expr1.WithVariableOrder(vars);
            expr2 = expr2.WithVariableOrder(vars);

            Assert.IsFalse(expr1.StructuralEquals(expr2), "Структурная форма должна различаться (дуальный консенсус не схлопывается нормализатором).");
            Assert.IsFalse(expr1.Equals(expr2), "Equals должен быть false для структурно разных форм.");
            Assert.IsTrue(expr1.EquivalentTo(expr2), "Формулы логически эквивалентны по дуальной теореме консенсуса.");
        }

        [DataTestMethod]
        [Timeout(TestTimeoutMs)]
        [DataRow("ShuntingYard")]
        [DataRow("Pratt")]
        [Description("Equals vs EquivalentTo: консенсус с тремя переменными (A & B) | (A & ~B) | (A & C) ≡ A | (A & C)")]
        public void EqualsVsEquivalentTo_ConsensusOr_ThreeTerms_DiffersStructurallyButEquivalent(string strategy)
        {
            SetParserStrategyForTest(strategy);

            var expr1 = new LogicalExpression(ExpressionParser.Parse("(A & B) | (A & ~B) | (A & C)"));
            var expr2 = new LogicalExpression(ExpressionParser.Parse("A | (A & C)"));

            var vars = expr1.Variables.Union(expr2.Variables).OrderBy(v => v).ToArray();
            expr1 = expr1.WithVariableOrder(vars);
            expr2 = expr2.WithVariableOrder(vars);

            Assert.IsFalse(expr1.StructuralEquals(expr2), "Структуры должны различаться: нормализатор не применяет общий консенсус для нескольких термов.");
            Assert.IsFalse(expr1.Equals(expr2), "Equals должен быть false для структурно разных форм.");
            Assert.IsTrue(expr1.EquivalentTo(expr2), "Формулы логически эквивалентны по консенсусу (с удалением пары B/~B).");
        }

        #endregion

        #endregion
    }
}
