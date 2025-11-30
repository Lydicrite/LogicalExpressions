using System;
using System.Collections.Generic;
using LogicalExpressions.Compilation.Visitors;

namespace LogicalExpressions.Core.Nodes
{
    /// <summary>
    /// Абстрактный класс элемента логического выражения.
    /// </summary>
    public abstract class LogicNode
    {
        /// <summary>
        /// Принимает посетителя <see cref="ILEVisitor"/>.
        /// </summary>
        /// <param name="visitor">Принимаемый посетитель.</param>
        public abstract void Accept(ILEVisitor visitor);

        /// <summary>
        /// Вычисляет значение узла по переданным входным параметрам <paramref name="inputs"/>.
        /// </summary>
        /// <param name="inputs">Входные параметры (значения переменных).</param>
        /// <returns>Значение логического выражения по входам <paramref name="inputs"/>.</returns>
        public abstract bool Evaluate(bool[] inputs);

        /// <summary>
        /// Вычисляет значение узла по переданным входным параметрам <paramref name="inputs"/> (без аллокаций, через ReadOnlySpan).
        /// </summary>
        /// <param name="inputs">Входные параметры (значения переменных).</param>
        /// <returns>Значение логического выражения по входам <paramref name="inputs"/>.</returns>
        public abstract bool Evaluate(ReadOnlySpan<bool> inputs);

        /// <summary>
        /// Собирает имена переменных в узле в <paramref name="variables"/>.
        /// </summary>
        /// <param name="variables">Хэш-сет, содержащий имена переменных.</param>
        public abstract void CollectVariables(HashSet<string> variables);
        
        public abstract override string ToString();
        public abstract override bool Equals(object? obj);
        public abstract override int GetHashCode();
    }
}
