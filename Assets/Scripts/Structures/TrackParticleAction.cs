using System;
using ArcCore.Mathematics;
using Unity.Mathematics;

namespace ArcCore.Structures
{
    using Interfaces;

    public readonly struct TrackParticleAction : IParticleAction
    {
        public enum Type
        {
            TAP = ComboParticleAction.Type.___len,
            HELD,

            ___len
        }

        public readonly Type type;
        public readonly int track;

        public float3 Position => new float3(Conversions.TrackToX(track), 0, 0);
        public int TypeID => (int)type;
    }
}