using Unity.Entities;

namespace ArcCore.Components
{
    [GenerateAuthoringComponent]
    public struct HoldLastJudge : IComponentData
    {
        public bool value;
        public HoldLastJudge(bool v) 
            => value = v;
    }
}
