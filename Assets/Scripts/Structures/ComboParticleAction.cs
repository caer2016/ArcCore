using Unity.Mathematics;

namespace ArcCore.Structures
{
    using Interfaces;

    public readonly struct ComboParticleAction : IParticleAction
    {
        public enum Type
        {
            LATE = SkyParticleAction.Type.___len,
            EARLY,

            ___len
        }

        public readonly Type type;
        private static readonly float3 pos = new float3(0,10f,0); //TEMPORARY

        public float3 Position => pos;
        public int TypeID => (int)type;
    }
}