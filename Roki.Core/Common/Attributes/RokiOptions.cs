using System;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RokiOptionsAttribute : Attribute
    {
        public RokiOptionsAttribute(Type t)
        {
            OptionType = t;
        }

        public Type OptionType { get; set; }
    }
}