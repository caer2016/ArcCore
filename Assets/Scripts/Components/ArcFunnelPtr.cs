using Unity.Entities;

namespace ArcCore.Components
{
    [GenerateAuthoringComponent]
    public unsafe struct ArcFunnelPtr : IComponentData
    {
        public ArcFunnel* Value;
    }
}
