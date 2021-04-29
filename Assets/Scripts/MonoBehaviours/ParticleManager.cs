using System.Collections;
using UnityEngine;
using ArcCore.Interfaces;

namespace ArcCore.MonoBehaviours
{
    public class ParticleManager : MonoBehaviour
    {
        private static ParticleManager instance;

        void Start()
        {
            instance = this;
        }

        void Update()
        {

        }

        public static void ParseParticle(IParticleAction particleAction)
            => instance.ParseParticleI(particleAction);

        private void ParseParticleI(IParticleAction particleAction)
        {
            //TODO: THIS
        }
    }
}