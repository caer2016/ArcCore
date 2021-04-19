using System.Collections;
using UnityEngine;
using ArcCore.Structs;

namespace ArcCore.MonoBehaviours
{
    public class ParticleManager : MonoBehaviour
    {
        public static ParticleManager Instance { get; private set; }

        void Start()
        {
            Instance = this;
        }

        void Update()
        {

        }

        public static void ParseParticle(IParticleAction particleAction)
            => Instance.ParseParticleI(particleAction);

        private void ParseParticleI(IParticleAction particleAction)
        {
            //TODO: THIS
        }
    }
}