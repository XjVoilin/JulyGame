using System.Collections.Generic;

namespace JulyGame
{
    public interface IBIEvent
    {
        string EventName { get; }
        Dictionary<string, object> ToParams();
    }

    public interface IBIProperties
    {
        Dictionary<string, object> ToParams();
    }
}
