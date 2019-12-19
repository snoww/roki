using System;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RokiOptions : Attribute
    {
        public RokiOptions(Type t)
        {
            OptionType = t;
        }

        public Type OptionType { get; set; }
    }
}