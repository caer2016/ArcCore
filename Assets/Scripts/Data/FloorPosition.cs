using ArcCore.Structs;
using Unity.Entities;

namespace ArcCore.Data
{
    [GenerateAuthoringComponent]
    public struct FloorPosition : IComponentData
    {
        public FixedQ7 Value;
    }

}
