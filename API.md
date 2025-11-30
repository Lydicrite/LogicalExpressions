# API Reference

Полный справочник публичного API библиотеки **LogicalExpressions**.

## Пространство имен `LogicalExpressions.Core`

### Класс `LogicalExpression`
Основной класс для работы с логическими выражениями. Представляет собой неизменяемый (immutable) объект, инкапсулирующий AST (абстрактное синтаксическое дерево) выражения.

#### Конструкторы
- `LogicalExpression(LogicNode root)`: Создает новое выражение из корневого узла AST. Переменные извлекаются автоматически и сортируются в алфавитном порядке.

#### Свойства
- `IReadOnlyList<string> Variables`: Возвращает список имен переменных, используемых в выражении, в текущем порядке.
- `static int DelegateCacheSize`: Возвращает текущее количество элементов в кэше скомпилированных делегатов.

#### Методы
- `bool Evaluate(bool[] inputs)`: Вычисляет значение выражения для заданного массива входных значений. Порядок значений должен соответствовать порядку переменных в `Variables`.
- `bool Evaluate(ReadOnlySpan<bool> inputs)`: Аналог `Evaluate` для `ReadOnlySpan<bool>`, позволяющий избежать аллокаций.
- `bool Evaluate(IDictionary<string, bool> inputs)`: Вычисляет значение, используя словарь значений переменных (безопасный способ, не зависящий от порядка).
- `void Compile()`: Принудительно компилирует выражение в делегат и кэширует его. Обычно вызывается автоматически (лениво) при первом вызове `Evaluate`.
- `LogicalExpression WithVariableOrder(IEnumerable<string> variables)`: Создает новый экземпляр выражения с заданным порядком переменных. Позволяет переупорядочить переменные или добавить новые (расширение контекста).
- `LogicalExpression WithCompilationOptions(CompilationOptions options)`: Создает новый экземпляр с измененными опциями компиляции (например, отключение short-circuiting).
- `LogicalExpression Normalize()`: Возвращает новый экземпляр выражения, приведенный к нормализованной форме (удаление двойных отрицаний, применение законов де Моргана, упрощение констант).
- `LogicalExpression Minimize()`: Возвращает минимизированную версию выражения, используя BDD (Binary Decision Diagrams).
- `LogicalExpression ToCnf()`: Преобразует выражение в конъюнктивную нормальную форму (КНФ).
- `LogicalExpression ToDnf()`: Преобразует выражение в дизъюнктивную нормальную форму (ДНФ).
- `bool IsTautology()`: Возвращает `true`, если выражение истинно при любых значениях переменных.
- `bool IsContradiction()`: Возвращает `true`, если выражение ложно при любых значениях переменных.
- `bool IsSatisfiable()`: Возвращает `true`, если существует хотя бы один набор значений переменных, при котором выражение истинно.
- `string PrintTruthTable()`: Генерирует строковое представление таблицы истинности для выражения.
- `bool StructuralEquals(LogicalExpression other)`: Проверяет структурное равенство AST двух выражений.
- `bool EquivalentTo(LogicalExpression other)`: Проверяет семантическую эквивалентность двух выражений (используя BDD), даже если их структура различается.

#### Статические методы
- `static void ConfigureDelegateCache(int maxSize, int evictPercent, bool enableTtl, TimeSpan ttl)`: Настраивает параметры кэширования скомпилированных делегатов.
- `static void ClearDelegateCache()`: Очищает кэш делегатов.

---

## Пространство имен `LogicalExpressions.Core.Nodes`

### Абстрактный класс `LogicNode`
Базовый класс для всех узлов AST.

#### Методы
- `abstract bool Evaluate(bool[] inputs)`: Вычисляет значение узла.
- `abstract bool Evaluate(ReadOnlySpan<bool> inputs)`: Вычисляет значение узла (Span).
- `abstract void Accept(ILEVisitor visitor)`: Принимает посетителя (Visitor pattern).
- `abstract void CollectVariables(HashSet<string> variables)`: Рекурсивно собирает имена переменных.

### Класс `ConstantNode`
Представляет логическую константу (`true` или `false`).
- Свойство `bool Value`: Значение константы.

### Класс `VariableNode`
Представляет переменную.
- Свойство `string Name`: Имя переменной.
- Свойство `int Index`: Индекс переменной во входном массиве значений.

### Класс `UnaryNode`
Представляет унарную операцию (например, NOT).
- Свойство `string Operator`: Символ оператора (например, `"~"`).
- Свойство `LogicNode Operand`: Операнд.

### Класс `BinaryNode`
Представляет бинарную операцию (AND, OR, XOR и т.д.).
- Свойство `string Operator`: Символ оператора.
- Свойство `LogicNode Left`: Левый операнд.
- Свойство `LogicNode Right`: Правый операнд.

---

## Пространство имен `LogicalExpressions.Parsing`

### Статический класс `ExpressionParser`
Фасад для парсинга строковых выражений.

#### Свойства
- `static long AstCacheHits`: Количество попаданий в кэш AST.
- `static long AstCacheMisses`: Количество промахов кэша AST.
- `static int CacheSize`: Текущий размер кэша AST.

#### Методы
- `static LogicNode Parse(string expression)`: Парсит строку с настройками по умолчанию. Выбрасывает исключения при ошибках.
- `static LogicNode Parse(string expression, LEParserOptions? options)`: Парсит строку с заданными опциями.
- `static LogicNode Parse(string expression, ParserStrategy strategy)`: Парсит строку с указанной стратегией.
- `static bool TryParse(string expression, out LogicNode? result, out ParseException? error)`: Безопасный парсинг без выброса исключений.
- `static void ConfigureOperators(Action<OperatorRegistry> configure)`: Позволяет настроить реестр операторов (добавить свои операторы).
- `static void RegisterOperatorAlias(string alias, string canonical)`: Регистрирует текстовый алиас для оператора (например, "И" для "&").
- `static void SetParserStrategy(IParserStrategy strategy)`: Устанавливает глобальную стратегию парсинга.
- `static void SetParserOptions(LEParserOptions options)`: Применяет глобальные настройки парсера и кэшей.
- `static void ClearCache()`: Очищает кэш AST.

### Класс `LEParserOptions`
Настройки парсера.
- `ParserStrategy Strategy`: Выбор алгоритма (`ShuntingYard` или `Pratt`).
- `bool EnableAliasSuggestions`: Включить подсказки при опечатках (default: true).
- `int AstMaxCacheSize`: Размер кэша AST.
- `int DelegateMaxCacheSize`: Размер кэша делегатов.
- `TimeSpan AstTtl`: Время жизни записи в кэше AST.

### Перечисление `ParserStrategy`
- `ShuntingYard`: Алгоритм сортировочной станции.
- `Pratt`: Top-Down Operator Precedence парсер.

---

## Пространство имен `LogicalExpressions.Parsing.Strategies`

### Интерфейс `IParserStrategy`
- `LogicNode Parse(List<Token> tokens, OperatorRegistry registry)`: Метод парсинга токенов в AST.

### Класс `ShuntingYardParserStrategy`
Реализация алгоритма Дейкстры.

### Класс `PrattParserStrategy`
Реализация парсера Пратта.

---

## Пространство имен `LogicalExpressions.Parsing.Tokenization`

### Класс `Tokenizer`
- `List<Token> Tokenize(string expression, bool enableSuggestions = true)`: Разбивает строку на токены.

### Класс `Token`
- `TokenType Type`: Тип токена (Identifier, Constant, Operator, LParen, RParen).
- `string Value`: Текстовое значение.
- `int Position`: Позиция в исходной строке.

---

## Пространство имен `LogicalExpressions.Compilation`

### Класс `CompilationOptions`
- `bool UseShortCircuiting`: Использовать ли `&&` / `||` (short-circuit) вместо `&` / `|` при компиляции в IL-код.

---

## Пространство имен `LogicalExpressions.Compilation.Visitors`

### Интерфейс `ILEVisitor`
- `void Visit(LogicNode node)`: Основной метод посещения.

### Абстрактный класс `BaseVisitor`
Базовая реализация посетителя с диспетчеризацией по типам узлов (`VisitConstant`, `VisitVariable`, `VisitUnary`, `VisitBinary`).

---

## Пространство имен `LogicalExpressions.Optimization.Bdd`

### Класс `BddManager`
Менеджер для создания и управления узлами BDD. Обеспечивает каноничность (Shared BDD).
- `BddNode CreateVariable(int varIndex)`: Создает переменную BDD.
- Методы операций: `And`, `Or`, `Not`, `Xor`, `Imply`.

### Класс `BddNode`
Узел бинарной диаграммы решений.
- `bool IsTerminal`: Является ли узел терминальным (константой).
- `bool Value`: Значение (для терминальных узлов).
- `BddNode Low`: Ветвь "False".
- `BddNode High`: Ветвь "True".

### Класс `BddConverter`
- `LogicNode Convert(BddNode node)`: Преобразует BDD обратно в дерево выражений (AST).

---

## Пространство имен `LogicalExpressions.Utils`

### Класс `ParseException`
Исключение, выбрасываемое при ошибках парсинга.
- `ParseErrorCode ErrorCode`: Код ошибки.
- `int Position`: Позиция ошибки.
- `IEnumerable<string> Suggestions`: Список предлагаемых исправлений (если есть).

### Перечисление `ParseErrorCode`
Коды ошибок парсинга (например, `EmptyExpression`, `UnmatchedParentheses`, `UnknownToken` и др.).
