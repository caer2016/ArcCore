﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using ArcCore.Data;
using ArcCore.MonoBehaviours;
using Unity.Rendering;
using ArcCore.Utility;
using Unity.Mathematics;
using ArcCore.MonoBehaviours.EntityCreation;
using ArcCore;
using ArcCore.Tags;

public struct JudgeEntityRef
{
    public enum EntityType
    {
        HOLD,
        TAP,
        ARCTAP
    }

    public bool exists;
    public Entity entity;
    public int time;
    public EntityType entityType;

    public JudgeEntityRef(Entity entity, int time, EntityType entityType)
    {
        exists = true;
        this.entity = entity;
        this.time = time;
        this.entityType = entityType;
    }

    public static JudgeEntityRef Empty() => new JudgeEntityRef()
    {
        exists = false,
        entity = new Entity(),
        time = -1,
        entityType = (EntityType)(-1)
    };
}

public class JudgementSystem : SystemBase
{
    public static JudgementSystem Instance { get; private set; }
    public EntityManager entityManager;
    public NativeArray<int> currentArcFingers;
    public NativeArray<AABB2D> laneAABB2Ds;
    public NativeArray<JudgeEntityRef> noteForTouch;

    public bool IsReady => currentArcFingers.IsCreated;

    public EntityQuery tapQuery, arcQuery, arctapQuery, holdQuery;

    public const float arcLeniencyGeneral = 2f;
    protected override void OnCreate()
    {
        Instance = this;
        var defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;
        
        laneAABB2Ds = new NativeArray<AABB2D>(
            new AABB2D[] {
                new AABB2D(new float2(Convert.TrackToX(1), 0), new float2(Constants.LaneWidth, float.PositiveInfinity)),
                new AABB2D(new float2(Convert.TrackToX(2), 0), new float2(Constants.LaneWidth, float.PositiveInfinity)),
                new AABB2D(new float2(Convert.TrackToX(3), 0), new float2(Constants.LaneWidth, float.PositiveInfinity)),
                new AABB2D(new float2(Convert.TrackToX(4), 0), new float2(Constants.LaneWidth, float.PositiveInfinity))
                    },
            Allocator.Persistent
            );

        holdQuery =  GetEntityQuery(
                typeof(HoldFunnelPtr),
                typeof(ChartTime),
                typeof(Track),
                typeof(WithinJudgeRange),
                typeof(JudgeHoldPoint)
            );

        arctapQuery = GetEntityQuery(
                typeof(EntityReference),
                typeof(ChartTime),
                typeof(SinglePosition),
                typeof(WithinJudgeRange)
            );


        tapQuery = GetEntityQuery(
                typeof(EntityReference),
                typeof(ChartTime),
                typeof(Track),
                typeof(WithinJudgeRange)
            );

        arcQuery = GetEntityQuery(
                typeof(ArcFunnelPtr),
                typeof(LinearPosGroup),
                typeof(ColorID),
                typeof(StrictArcJudge),
                typeof(WithinJudgeRange)
            );

        noteForTouch = new NativeArray<JudgeEntityRef>(InputManager.MaxTouches, Allocator.Persistent);
    }
    public void SetupColors()
    {
        currentArcFingers = new NativeArray<int>(ArcEntityCreator.Instance.arcColors.Length, Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        currentArcFingers.Dispose();
        laneAABB2Ds.Dispose();
        noteForTouch.Dispose();
    }
    protected override unsafe void OnUpdate()
    {
        //Only execute after full initialization
        if (!IsReady)
            return;

        //Get data from statics
        int currentTime = (int)(Conductor.Instance.receptorTime / 1000f);

        //Setup noteForTouch 
        for (int i = 0; i < noteForTouch.Length; i++)
        {
            noteForTouch[i] = JudgeEntityRef.Empty();
        }

        // Handle arc fingers once they are released //
        for(int i = 0; i < currentArcFingers.Length; i++)
        {
            if(currentArcFingers[i] != -1)
            {
                bool remove = true;
                for(int j = 0; j < InputManager.Instance.touchPoints.Length; j++)
                {
                    bool statusIsReleased = InputManager.Instance.touchPoints[j].status == TouchPoint.Status.RELEASED;
                    if (InputManager.Instance.touchPoints[j].fingerId == currentArcFingers[i])
                    {
                        if (!statusIsReleased)
                        {
                            remove = false;
                        }
                        break;
                    }
                }

                if(remove)
                {
                    currentArcFingers[i] = -1;
                }
            }
        }

        // Handle all arcs //
        NativeArray<Entity> arcEns = arcQuery.ToEntityArray(Allocator.TempJob);
        for (int en = 0; en < arcEns.Length; en++)
        {
            Entity entity = arcEns[en];

            // Get entity components
            ArcFunnelPtr arcFunnelPtr     = entityManager.GetComponentData<ArcFunnelPtr>  (entity);
            ColorID colorID               = entityManager.GetComponentData<ColorID>       (entity);
            LinearPosGroup linearPosGroup = entityManager.GetComponentData<LinearPosGroup>(entity);
            StrictArcJudge strictArcJudge = entityManager.GetComponentData<StrictArcJudge>(entity);

            // Get arc funnel pointer to allow indirect struct access
            ArcFunnel* arcFunnelPtrD = arcFunnelPtr.Value;

            // Kill all points that have passed
            if (linearPosGroup.endTime < currentTime)
            {

                arcFunnelPtrD->visualState =
                    arcFunnelPtrD->isHit ?
                    LongnoteVisualState.JUDGED_PURE :
                    LongnoteVisualState.JUDGED_LOST;

                ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.LOST);
                //PARTICLE MANAGEMENT HERE OR IN SCORE MANAGER

                entityManager.AddComponent(entity, typeof(Disabled));

                return;

            }

            // Loop through all touch points
            for (int i = 0; i < InputManager.Instance.touchPoints.Length; i++)
            {

                // Arc hit by finger
                if (linearPosGroup.startTime <= InputManager.Instance.touchPoints[i].time &&
                    linearPosGroup.endTime >= InputManager.Instance.touchPoints[i].time &&
                    InputManager.Instance.touchPoints[i].status != TouchPoint.Status.RELEASED &&
                    InputManager.Instance.touchPoints[i].inputPlaneValid &&
                    InputManager.Instance.touchPoints[i].inputPlane.CollidingWith(
                        new AABB2D(
                            linearPosGroup.PosAt(currentTime),
                            new float2(arcLeniencyGeneral)
                            )
                        ))
                {

                    // Set hit to true
                    arcFunnelPtrD->isHit = true;

                    // Set red based on current finger id
                    arcFunnelPtrD->isRed =
                       (InputManager.Instance.touchPoints[i].fingerId != currentArcFingers[colorID.Value] &&
                        currentArcFingers[colorID.Value] != -1) || !strictArcJudge.Value;

                    // If the point not is strict, remove the current finger id to allow for switching
                    if (!strictArcJudge.Value)
                    {
                        currentArcFingers[colorID.Value] = -1;
                    }
                    // If there is no finger currently, allow there to be a new one permitted that the arc is not hit
                    else if (currentArcFingers[colorID.Value] != InputManager.Instance.touchPoints[i].fingerId && !arcFunnelPtrD->isHit)
                    {
                        currentArcFingers[colorID.Value] = InputManager.Instance.touchPoints[i].fingerId;
                    }

                    // Kill arc judger
                    if (arcFunnelPtrD->isRed)
                    {
                        ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.LOST);
                    }
                    else
                    {
                        ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.MAX_PURE);
                    }

                    entityManager.AddComponent(entity, typeof(Disabled));

                }
            }
        }

        // Destroy array after use
        arcEns.Dispose();


        // Handle all holds //
        NativeArray<Entity> holdEns = holdQuery.ToEntityArray(Allocator.TempJob);
        for (int en = 0; en < holdEns.Length; en++)
        {
            Entity entity = holdEns[en];

            // Get entity components
            HoldFunnelPtr holdFunnelPtr = entityManager.GetComponentData<HoldFunnelPtr>(entity);
            ChartTime chartTime         = entityManager.GetComponentData<ChartTime>    (entity);
            Track track                 = entityManager.GetComponentData<Track>        (entity);

            HoldFunnel* holdFunnelPtrD = holdFunnelPtr.Value;

            //Kill old entities and make lost
            if (chartTime.Value + Constants.FarWindow < currentTime)
            {

                ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.LOST);

                entityManager.AddComponent(entity, typeof(Disabled));

                return;

            }

            //Reset hold value
            if (currentTime > chartTime.Value)
            {
                holdFunnelPtrD->visualState = LongnoteVisualState.JUDGED_LOST;
            }

            for (int i = 0; i < InputManager.Instance.touchPoints.Length; i++)
            {
                bool generalJudgeValid =
                    chartTime.Value - InputManager.Instance.touchPoints[i].time <= Constants.FarWindow &&
                    InputManager.Instance.touchPoints[i].time - chartTime.Value >= Constants.FarWindow &&
                    InputManager.Instance.touchPoints[i].trackPlaneValid &&
                    InputManager.Instance.touchPoints[i].trackPlane.CollidingWith(laneAABB2Ds[track.Value]);

                if (holdFunnelPtrD->isHit)
                {
                    if (generalJudgeValid)
                    {

                        if (InputManager.Instance.touchPoints[i].status == TouchPoint.Status.RELEASED)
                        {
                            holdFunnelPtrD->isHit = false;
                        }
                        else
                        {

                            ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.MAX_PURE);

                            entityManager.AddComponent(entity, typeof(Disabled));

                        }

                        return;

                    }
                }
                else
                {
                    if (
                       !entityManager.Exists(noteForTouch[i].entity) &&
                        noteForTouch[i].time < chartTime.Value &&
                        InputManager.Instance.touchPoints[i].status == TouchPoint.Status.TAPPED &&
                        generalJudgeValid
                    )
                    {
                        //Check for judge later
                        noteForTouch[i] = new JudgeEntityRef(entity, chartTime.Value, JudgeEntityRef.EntityType.HOLD);
                        return;
                    }
                }
            }
        }

        // Destroy array after use
        holdEns.Dispose();


        // Handle all arctaps //
        NativeArray<Entity> arctapEns = arctapQuery.ToEntityArray(Allocator.TempJob);
        for (int en = 0; en < arctapEns.Length; en++)
        {
            Entity entity = arctapEns[en];

            // Get entity components
            EntityReference entityReference = entityManager.GetComponentData<EntityReference>(entity);
            SinglePosition singlePosition   = entityManager.GetComponentData<SinglePosition> (entity);
            ChartTime chartTime             = entityManager.GetComponentData<ChartTime>      (entity);

            //Kill all remaining entities if they are overaged
            if (chartTime.Value + Constants.FarWindow < currentTime)
            {

                ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.LOST);

                entityManager.AddComponent(entity, typeof(Disabled));
                entityManager.AddComponent(entityReference.Value, typeof(Disabled));

                return;

            }

            for (int i = 0; i < InputManager.Instance.touchPoints.Length; i++)
            {
                if (
                   !entityManager.Exists(noteForTouch[i].entity) &&
                    noteForTouch[i].time < chartTime.Value &&
                    InputManager.Instance.touchPoints[i].status == TouchPoint.Status.TAPPED &&
                    InputManager.Instance.touchPoints[i].inputPlaneValid &&
                    InputManager.Instance.touchPoints[i].inputPlane.CollidingWith(
                        new AABB2D(
                            singlePosition.Value,
                            new float2(arcLeniencyGeneral)
                            )
                        ))
                {
                    //Check for judge later
                    noteForTouch[i] = new JudgeEntityRef(entity, chartTime.Value, JudgeEntityRef.EntityType.ARCTAP);
                    return;
                }
            }
        }

        // Destroy array after use
        arctapEns.Dispose();


        // Handle all taps //
        NativeArray<Entity> tapEns = tapQuery.ToEntityArray(Allocator.TempJob);
        for (int en = 0; en < tapEns.Length; en++)
        {
            Entity entity = tapEns[en];

            // Get entity components
            EntityReference entityReference = entityManager.GetComponentData<EntityReference>(entity);
            ChartTime chartTime             = entityManager.GetComponentData<ChartTime>      (entity);
            Track track                     = entityManager.GetComponentData<Track>          (entity);

            //Kill all remaining entities if they are overaged
            if (chartTime.Value + Constants.FarWindow < currentTime)
            {

                ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.LOST);

                entityManager.AddComponent(entity, typeof(Disabled));
                entityManager.AddComponent(entityReference.Value, typeof(Disabled));

                return;

            }

            for (int i = 0; i < InputManager.Instance.touchPoints.Length; i++)
            {
                if (
                   !entityManager.Exists(noteForTouch[i].entity) &&
                    noteForTouch[i].time < chartTime.Value &&
                    InputManager.Instance.touchPoints[i].trackPlaneValid &&
                    InputManager.Instance.touchPoints[i].trackPlane.CollidingWith(laneAABB2Ds[track.Value])
                )
                {
                    //Check for judge later
                    noteForTouch[i] = new JudgeEntityRef(entity, chartTime.Value, JudgeEntityRef.EntityType.TAP);
                    return;
                }
            }
        }

        // Destroy array after use
        tapEns.Dispose();


        // Complete code and find types //
        for (int t = 0; t < noteForTouch.Length; t++)
        {
            if (!noteForTouch[t].exists) continue;

            Entity minEntity = noteForTouch[t].entity;

            if (noteForTouch[t].entityType == JudgeEntityRef.EntityType.HOLD)
            {
                HoldFunnelPtr holdFunnelPtr = entityManager.GetComponentData<HoldFunnelPtr>(minEntity);
                holdFunnelPtr.Value->visualState = LongnoteVisualState.JUDGED_PURE;
                entityManager.AddComponent(minEntity, typeof(Disabled));
                ScoreManager.Instance.AddJudge(JudgeManage.JudgeType.MAX_PURE);
            }
            else
            {
                JudgeManage.JudgeType type = JudgeManage.GetType(currentTime - noteForTouch[t].time);
                Entity entityReferenced = entityManager.GetComponentData<EntityReference>(minEntity).Value;

                if(noteForTouch[t].entityType == JudgeEntityRef.EntityType.ARCTAP)
                {
                    // Destroy shadow
                    entityManager.AddComponent(entityManager.GetComponentData<EntityReference>(entityReferenced).Value, typeof(Disabled));
                }

                entityManager.AddComponent(minEntity, typeof(Disabled));
                entityManager.AddComponent(entityReferenced, typeof(Disabled));
                ScoreManager.Instance.AddJudge(type);
            }
        }
    }
}
