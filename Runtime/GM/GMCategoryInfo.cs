#if JULYGF_DEBUG
using System;
using System.Collections.Generic;

namespace JulyGame
{
    public sealed class GMCategoryInfo
    {
        public string Category;
        public Type SourceType;
        public List<GMCommandInfo> Commands = new();
    }
}
#endif
