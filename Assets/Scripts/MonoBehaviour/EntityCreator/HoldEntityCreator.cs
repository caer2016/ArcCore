﻿using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using ArcCore.Utility;
using ArcCore.Data;
using ArcCore.Structs;

namespace ArcCore.MonoBehaviours.EntityCreation
{
    public class HoldEntityCreator : MonoBehaviour
    {
        public static HoldEntityCreator Instance { get; private set; }
        [SerializeField] private GameObject holdNotePrefab;
        private Entity holdNoteEntityPrefab;
        private World defaultWorld;
        private EntityManager entityManager;
        private void Awake()
        {
            Instance = this;
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            GameObjectConversionSettings settings = GameObjectConversionSettings.FromWorld(defaultWorld, null);
            holdNoteEntityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(holdNotePrefab, settings);
        }

        public void CreateEntities(List<AffHold> affHoldList)
        {
            affHoldList.Sort((item1, item2) => { return item1.timing.CompareTo(item2.timing); });

            foreach (AffHold hold in affHoldList)
            {
                Entity holdEntity = entityManager.Instantiate(holdNoteEntityPrefab);

                float x = Convert.TrackToX(hold.track);
                const float y = 0;
                const float z = 0;

                const float scalex = 1;
                const float scaley = 1;
                fixed_dec endFloorPosition = Conductor.Instance.GetFloorPositionFromTiming(hold.endTiming, hold.timingGroup);
                fixed_dec startFloorPosition = Conductor.Instance.GetFloorPositionFromTiming(hold.timing, hold.timingGroup);
                float scalez = -endFloorPosition + startFloorPosition;

                entityManager.SetComponentData<Translation>(holdEntity, new Translation() {
                    Value = new float3(x, y, z)
                });
                entityManager.AddComponentData<NonUniformScale>(holdEntity, new NonUniformScale() {
                    Value = new float3(scalex, scaley, scalez)
                });
                entityManager.SetComponentData<FloorPosition>(holdEntity, new FloorPosition() {
                    Value = startFloorPosition
                });
                entityManager.SetComponentData<TimingGroup>(holdEntity, new TimingGroup() {
                    Value = hold.timingGroup
                });

            }
        }

        public void CreateJudgeEntities(AffHold hold)
        {
            float time = hold.timing;
            TimingEvent timingEvent = Conductor.Instance.GetTimingEventFromTiming(hold.timing, hold.timingGroup);

            while (time < hold.endTiming)
            {
                time += (timingEvent.bpm >= 255 ? 60_000f : 30_000f) / timingEvent.bpm;

                Entity judgeEntity = entityManager.CreateEntity(typeof(JudgeTime), typeof(JudgeLane), typeof(Tags.JudgeHold));
                entityManager.SetComponentData<JudgeTime>(judgeEntity, new JudgeTime()
                {
                    time = (int)time
                });
                entityManager.SetComponentData<JudgeLane>(judgeEntity, new JudgeLane()
                {
                    lane = hold.track
                });

                ScoreManager.Instance.maxCombo++;
            }
        }
    }

}