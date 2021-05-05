using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using ArcCore.Components;
using ArcCore.MonoBehaviours;
using ArcCore.Tags;
using Unity.Rendering;
using ArcCore.Mathematics;
using UnityEngine;

public class MovingNotesSystem : SystemBase
{
    protected override void OnUpdate()
    {
        NativeArray<fixedQ7> currentFloorPosition = Conductor.Instance.currentFloorPosition;

        //All except arcs
        Entities.ForEach((ref Translation translation, in FloorPosition floorPosition, in TimingGroup group) => {
            translation.Value.z = (float)(floorPosition.Value - currentFloorPosition[group.Value]); 
        }).Schedule();

        //Arc segments
        Entities.WithNone<Translation>().
            ForEach((ref LocalToWorld lcwMatrix, in FloorPosition floorPosition, in TimingGroup group) =>
            {

                lcwMatrix.Value.c3.z = (float)(floorPosition.Value - currentFloorPosition[group.Value]);

            }).Schedule();
    }
}
