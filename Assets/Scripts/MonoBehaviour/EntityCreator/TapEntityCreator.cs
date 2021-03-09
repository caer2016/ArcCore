﻿using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using ArcCore.Utility;
using ArcCore.Data;
using Unity.Collections;

namespace ArcCore.MonoBehaviours.EntityCreation
{

    public class TapEntityCreator : MonoBehaviour
    {
        public static TapEntityCreator Instance { get; private set; }
        [SerializeField] private GameObject tapNotePrefab;
        private Entity tapNoteEntityPrefab;
        private World defaultWorld;
        private EntityManager entityManager;
        private void Awake()
        {
            Instance = this;
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            GameObjectConversionSettings settings = GameObjectConversionSettings.FromWorld(defaultWorld, null);
            tapNoteEntityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(tapNotePrefab, settings);

            entityManager.AddChunkComponentData<ChunkAppearTime>(tapNoteEntityPrefab);
            entityManager.SetChunkComponentData<ChunkAppearTime>(entityManager.GetChunk(tapNoteEntityPrefab), new ChunkAppearTime(){
                Value = int.MaxValue
            });
        }

        public void CreateEntities(List<AffTap> affTapList)
        {
            affTapList.Sort((item1, item2) => { return item1.timing.CompareTo(item2.timing); });

            foreach (AffTap tap in affTapList)
            {
                //Main Entity
                Entity tapEntity = entityManager.Instantiate(tapNoteEntityPrefab);

                float x = Convert.TrackToX(tap.track);
                const float y = 0;
                const float z = 0;

                entityManager.SetComponentData<Translation>(tapEntity, new Translation(){ 
                    Value = new float3(x, y, z)
                });

                float floorpos = Conductor.Instance.GetFloorPositionFromTiming(tap.timing, tap.timingGroup);
                entityManager.SetComponentData<FloorPosition>(tapEntity, new FloorPosition(){
                    Value = floorpos 
                });
                entityManager.SetComponentData<TimingGroup>(tapEntity, new TimingGroup()
                {
                    Value = tap.timingGroup
                });

                //Appear time
                int t1 = Conductor.Instance.GetFirstTimingFromFloorPosition(floorpos - Constants.RenderFloorPositionRange, tap.timingGroup);
                int t2 = Conductor.Instance.GetFirstTimingFromFloorPosition(floorpos + Constants.RenderFloorPositionRange, tap.timingGroup);
                int appearTime = (t1 < t2) ? t1 : t2;

                ChunkAppearTime chunkAppearTime = entityManager.GetChunkComponentData<ChunkAppearTime>(tapEntity);
                ChunkAppearTime newMinAppearTime = chunkAppearTime.Value > appearTime ?
                                                   chunkAppearTime : new ChunkAppearTime(){ Value = appearTime };
                
                entityManager.SetChunkComponentData<ChunkAppearTime>(entityManager.GetChunk(tapEntity), newMinAppearTime);

                //Judge component
                Entity judgeEntity = entityManager.CreateEntity(typeof(ChartTime), typeof(Track));
                entityManager.SetComponentData<ChartTime>(judgeEntity, new ChartTime()
                {
                    Value = tap.timing
                });;
                entityManager.SetComponentData<Track>(judgeEntity, new Track()
                {
                    Value = tap.track
                });

                ScoreManager.Instance.maxCombo++;
            }
        }
    }

}