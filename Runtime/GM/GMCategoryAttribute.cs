#if JULYGF_DEBUG
using System;

namespace JulyGame
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class GMCategoryAttribute : Attribute
    {
        public string Category { get; }

        public GMCategoryAttribute(string category)
        {
            Category = category;
        }
    }
}
#endif
