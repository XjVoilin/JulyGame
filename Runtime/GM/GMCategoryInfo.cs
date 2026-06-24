#if JULYGF_DEBUG
using System.Collections.Generic;

namespace JulyGame
{
    public sealed class GMCategoryInfo
    {
        public string Category;
        public List<GMCommandInfo> Commands = new();
    }
}
#endif
