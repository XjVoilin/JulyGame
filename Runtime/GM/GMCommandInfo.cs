#if JULYGF_DEBUG
using System;
using System.Reflection;

namespace JulyGame
{
    public sealed class GMCommandInfo
    {
        public string DisplayName;
        public string Group;
        public int Order;
        public bool CloseAfter;
        public MethodInfo Method;
        public GMParamInfo[] Params;

        public object Invoke(object[] args)
        {
            try
            {
                return Method.Invoke(null, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException ?? e;
            }
        }
    }
}
#endif
