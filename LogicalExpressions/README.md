# LogicalExpressions

**LogicalExpressions** — это мощная, высокопроизводительная и расширяемая библиотека для .NET, предназначенная для парсинга, компиляции, оценки и оптимизации логических (булевых) выражений.

Библиотека поддерживает работу с переменными, логическими константами, широким набором операторов (включая пользовательские), а также предоставляет инструменты для анализа выражений (BDD, нормализация, минимизация).

---

## Оглавление

- [Основные возможности](#основные-возможности)
- [Быстрый старт](#быстрый-старт)
- [Парсинг выражений](#парсинг-выражений)
  - [Поддерживаемые операторы](#поддерживаемые-операторы)
  - [Стратегии парсинга](#стратегии-парсинга)
  - [Настройка парсера](#настройка-парсера)
- [Работа с выражением](#работа-с-выражением)
  - [Оценка (Evaluate)](#оценка-evaluate)
  - [Компиляция (Compile)](#компиляция-compile)
  - [Управление переменными](#управление-переменными)
- [Анализ и оптимизация](#анализ-и-оптимизация)
  - [Нормализация и формы (CNF, DNF)](#нормализация-и-формы-cnf-dnf)
  - [Минимизация](#минимизация)
  - [BDD (Binary Decision Diagrams)](#bdd-binary-decision-diagrams)
- [Структура проекта](#структура-проекта)

---

## Основные возможности

*   **Гибкий парсер**: Поддержка стандартных операторов (`&`, `|`, `^`, `!`, `=>`, `<=>`), скобок, алиасов (например, `AND`, `OR`, `NOT`) и пользовательских операторов.
*   **Высокая производительность**: Использование `Span<T>`, кэширование AST и скомпилированных делегатов, оптимизация аллокаций.
*   **Компиляция**: Преобразование выражений в делегаты `Func<bool[], bool>` через `System.Linq.Expressions` для максимально быстрого вычисления.
*   **Оптимизация**: Поддержка Binary Decision Diagrams (BDD) для проверок на тавтологию/противоречие и минимизации выражений.
*   **Расширяемость**: Возможность добавления своих операторов и стратегий парсинга (Shunting Yard, Pratt).
*   **Потокобезопасность**: Кэши парсера и компилятора потокобезопасны.

---

## Быстрый старт

```csharp
using LogicalExpressions.Parsing;
using LogicalExpressions.Core;
using System.Collections.Generic;

// 1. Парсинг строки в AST
var node = ExpressionParser.Parse("A & (B | !C)");

// 2. Создание объекта выражения
var expr = new LogicalExpression(node);

// 3. Вычисление результата
// Можно передать массив значений (порядок переменных алфавитный по умолчанию: A, B, C)
bool result = expr.Evaluate(new[] { true, false, true }); 
// A=true, B=false, C=true -> true & (false | false) -> false

// Или использовать словарь
var context = new Dictionary<string, bool>
{
    ["A"] = true,
    ["B"] = true,
    ["C"] = false
};
bool result2 = expr.Evaluate(context); // true & (true | true) -> true
```

---

## Парсинг выражений

Основной класс для парсинга — `LogicalExpressions.Parsing.ExpressionParser`.

### Поддерживаемые операторы

| Оператор | Алиасы (по умолчанию) | Описание | Приоритет |
| :--- | :--- | :--- | :--- |
| `~` | `!`, `NOT`, `¬` | Отрицание (NOT) | Высокий |
| `&` | `&&`, `AND`, `∧` | И (AND) | Средний |
| `|` | `||`, `OR`, `∨` | ИЛИ (OR) | Низкий |
| `^` | `XOR`, `⊕` | Исключающее ИЛИ (XOR) | Низкий |
| `=>` | `->`, `IMPLIES`, `→` | Импликация | Очень низкий |
| `<=>` | `<->`, `IFF`, `≡`, `==` | Эквиваленция | Очень низкий |
| `!&` | `NAND` | И-НЕ | Средний |
| `!|` | `NOR` | ИЛИ-НЕ | Низкий |

Константы: `1`, `true` (Истина) и `0`, `false` (Ложь).

### Стратегии парсинга

Библиотека поддерживает два алгоритма парсинга:
1.  **Shunting Yard** (по умолчанию) — классический алгоритм Дейкстры.
2.  **Pratt Parser** — алгоритм Top-Down Operator Precedence, удобен для расширения грамматики.

Выбор стратегии:
```csharp
// Глобально
ExpressionParser.SetParserStrategy(new PrattParserStrategy());

// Или для конкретного вызова
var options = new LEParserOptions { Strategy = ParserStrategy.Pratt };
var node = ExpressionParser.Parse("A & B", options);
```

### Настройка парсера

Вы можете регистрировать свои операторы или алиасы:

```csharp
// Регистрация алиаса
ExpressionParser.RegisterOperatorAlias("И", "&");

// Регистрация нового оператора (например, обратная импликация)
ExpressionParser.RegisterBinaryOperator(
    symbol: "<=", 
    precedence: 10, 
    rightAssociative: false, 
    factory: (left, right) => new BinaryNode("<=", left, right)
);
```

---

## Работа с выражением

Класс `LogicalExpressions.Core.LogicalExpression` является основной оберткой над AST.

### Оценка (Evaluate)

Метод `Evaluate` вычисляет значение выражения.

```csharp
// Через массив bool[] (порядок переменных важен!)
bool res = expr.Evaluate(new[] { true, false });

// Через Span<bool> (для high-performance сценариев)
bool resSpan = expr.Evaluate(stackalloc bool[] { true, false });

// Через словарь (безопасно, порядок не важен)
bool resDict = expr.Evaluate(new Dictionary<string, bool> { {"A", true}, {"B", false} });
```

### Компиляция (Compile)

Для многократного вычисления одного и того же выражения рекомендуется использовать `Compile()`. Это преобразует дерево AST в скомпилированный делегат.

```csharp
expr.Compile(); // Компиляция происходит лениво при первом вызове Evaluate, но можно вызвать явно
```

Кэширование скомпилированных делегатов включено по умолчанию. Вы можете настроить параметры кэша через `LogicalExpression.ConfigureDelegateCache(...)`.

### Управление переменными

По умолчанию переменные сортируются по алфавиту. Вы можете задать свой порядок:

```csharp
// Исходное выражение: A & B
// По умолчанию порядок: A (index 0), B (index 1)

var exprReordered = expr.WithVariableOrder(new[] { "B", "A" });
// Теперь: B (index 0), A (index 1)
bool res = exprReordered.Evaluate(new[] { true, false }); // B=true, A=false -> false & true -> false
```

---

## Анализ и оптимизация

### Нормализация и формы (CNF, DNF)

Вы можете привести выражение к каноническому виду или нормальным формам.

```csharp
var norm = expr.Normalize(); // Упрощение (удаление двойных отрицаний, применение законов де Моргана)
var cnf = expr.ToCnf();      // Конъюнктивная нормальная форма (Product of Sums)
var dnf = expr.ToDnf();      // Дизъюнктивная нормальная форма (Sum of Products)
```

### Минимизация

Метод `Minimize()` пытается уменьшить сложность выражения. Использует BDD для построения канонического представления и восстановления упрощенного выражения.

```csharp
var complex = ExpressionParser.Parse("(A & B) | (A & !B)"); // A & (B | !B) -> A & 1 -> A
var expr = new LogicalExpression(complex);
var minimized = expr.Minimize();

Console.WriteLine(minimized); // Вывод: "A"
```

### BDD (Binary Decision Diagrams)

Библиотека использует BDD для эффективных проверок свойств выражения.

```csharp
bool isTautology = expr.IsTautology();       // Всегда истинно? (например, A | !A)
bool isContradiction = expr.IsContradiction(); // Всегда ложно? (например, A & !A)
bool isSatisfiable = expr.IsSatisfiable();     // Выполнимо? (есть хотя бы один набор аргументов, дающий true)
```

---

## Структура проекта

*   **Core**: Основные модели (`LogicalExpression`, `LogicNode` и наследники).
*   **Parsing**: Логика парсинга (`ExpressionParser`, `Tokenizer`, стратегии).
*   **Compilation**: Компиляция в делегаты, визиторы (`Visitors`).
*   **Optimization**: Алгоритмы оптимизации, реализация BDD (`BddManager`, `BddConverter`).
*   **Utils**: Вспомогательные классы.

---
