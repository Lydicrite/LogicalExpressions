using System;

namespace LogicalExpressions.Utils
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class NotThreadSafeAttribute : Attribute
    {
    }
}
