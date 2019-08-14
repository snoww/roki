using System;

namespace Roki.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RokiOptionsAttribute : Attribute
    {
        public Type OptionType { get; set; }

        public RokiOptionsAttribute(Type t)
        {
            this.OptionType = t;
        }
    }
    
}