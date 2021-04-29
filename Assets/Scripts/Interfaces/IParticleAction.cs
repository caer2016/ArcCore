using Unity.Mathematics;

namespace ArcCore.Interfaces
{
    public interface IParticleAction
    {
        float3 Position { get; }
        int TypeID { get; }
    }
}
