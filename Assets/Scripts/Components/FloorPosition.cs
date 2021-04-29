using ArcCore.Mathematics;
using Unity.Entities;

namespace ArcCore.Components
{
    [GenerateAuthoringComponent]
    public struct FloorPosition : IComponentData
    {
        public fixedQ7 Value;
    }

}
