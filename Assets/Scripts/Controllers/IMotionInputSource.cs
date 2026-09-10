using System.Collections.Generic;

namespace MotionControllers
{
    public interface IMotionInputSource
    {
        IReadOnlyDictionary<string, ControllerSession> Sessions { get; }
        bool TryGetController(string controllerId, out ControllerSession session);
    }
}
