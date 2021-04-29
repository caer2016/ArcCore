using Unity.Mathematics;

namespace ArcCore.Structures
{
    using Interfaces;

    public readonly struct SkyParticleAction : IParticleAction
    {
        public enum Type
        {
            ARCHELD,
            TAP,

            PURE,
            FAR,
            LOST,

            ___len
        }

        public readonly Type type;
        private readonly float2 position;

        public float3 Position => new float3(position, 0);
        public int TypeID => (int)type;
    }
}