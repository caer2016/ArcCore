using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using ArcCore.Data;
using ArcCore.MonoBehaviours;
using Unity.Rendering;
using ArcCore.Utility;
using ArcCore.Structs;
using Unity.Mathematics;
using ArcCore.MonoBehaviours.EntityCreation;
using ArcCore;
using ArcCore.Tags;
using static JudgeManage;
using System;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public class JudgementSystem : SystemBase
{
    public static JudgementSystem Instance { get; private set; }
    public EntityManager entityManager;
    public NativeArray<int> currentArcFingers;

    public bool IsReady => currentArcFingers.IsCreated;
    public EntityQuery tapQuery, arcQuery, arctapQuery, holdQuery;

    public const float arcLeniencyGeneral = 2f;
    public static readonly float2 arctapBoxExtents = new float2(4f, 1f); //DUMMY VALUES

    BeginSimulationEntityCommandBufferSystem beginSimulationEntityCommandBufferSystem;
    public enum Taptype
    {
        Nil,
        ARCTAP,
        TAP,
        HOLD_HEAD
    }

    protected override void OnCreate()
    {
        Instance = this;
        var defaultWorld = World.DefaultGameObjectInjectionWorld;
        entityManager = defaultWorld.EntityManager;

        beginSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

        holdQuery = GetEntityQuery(
                typeof(HoldFunnelPtr),
                typeof(ChartTime),
                typeof(Track),
                typeof(WithinJudgeRange),
                typeof(JudgeHoldPoint)
            );

        arctapQuery = GetEntityQuery(
                typeof(EntityReference),
                typeof(ChartTime),
                typeof(ChartPosition),
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
    }

    public void SetupColors()
    {
        currentArcFingers = new NativeArray<int>(ArcEntityCreator.Instance.arcColors.Length, Allocator.Persistent);
    }
    protected override void OnDestroy()
    {
        currentArcFingers.Dispose();
    }
    protected override unsafe void OnUpdate()
    {
        //Only execute after full initialization
        if (!IsReady)
            return;

        //Get data from statics
        int currentTime = (int)(Conductor.Instance.receptorTime / 1000f);

        void JudgeFromTime(int time)
        {
            int timeDiff = time - currentTime;
            if (timeDiff > Constants.FarWindow)
                ScoreManager.RegisterLost();
            else if (timeDiff > Constants.PureWindow)
                ScoreManager.RegisterEFar();
            else if (timeDiff > Constants.MaxPureWindow)
                ScoreManager.RegisterEPure();
            else if (timeDiff > -Constants.MaxPureWindow)
                ScoreManager.RegisterMPure();
            else if (timeDiff > -Constants.PureWindow)
                ScoreManager.RegisterLPure();
            else
                ScoreManager.RegisterLFar();
        }

        void TapJudge(Entity en)
        {
            ChartTime chartTime = entityManager.GetComponentData<ChartTime>(en);
            JudgeFromTime(chartTime.value);

            entityManager.DestroyEntity(en);
        }
        void ArctapJudge(Entity en)
        {
            ChartTime chartTime = entityManager.GetComponentData<ChartTime>(en);
            JudgeFromTime(chartTime.value);

            entityManager.DestroyEntity(entityManager.GetComponentData<EntityReference>(en).Value);
            entityManager.DestroyEntity(en);
        }
        void HoldJudge(Entity en)
        {
            ScoreManager.RegisterMPure();
            entityManager.SetComponentData(en, new HoldLastJudge(true));

            ChartHoldTime holdTime = entityManager.GetComponentData<ChartHoldTime>(en);
            ChartTimeSpan timeSpan = entityManager.GetComponentData<ChartTimeSpan>(en);

            if (!holdTime.Increment(timeSpan))
            {
                entityManager.RemoveComponent<WithinJudgeRange>(en);
                entityManager.AddComponent<PastJudgeRange>(en);
            }
            else
            {
                entityManager.SetComponentData(en, holdTime);
            }
        }

        var commandBuffer = beginSimulationEntityCommandBufferSystem.CreateCommandBuffer();

        //Execute for each touch
        for (int i = 0; i < InputManager.MaxTouches; i++)
        {
            TouchPoint touch = InputManager.Get(i);

            double minTime = double.MaxValue;
            Entity minEntity = Entity.Null;
            Taptype taptype = Taptype.Nil;

            //Track lane notes
            if (touch.TrackRangeValid)
            {

                //Hold notes
                Entities.WithAll<WithinJudgeRange>().WithoutBurst().ForEach(

                    (Entity en, ref HoldIsHeld held, ref ChartHoldTime holdTime, ref HoldLastJudge lastJudge, in ChartTimeSpan span, in ChartPosition position)

                        =>

                    {
                        //Invalidate holds out of time range
                        if (!holdTime.CheckStart(Constants.FarWindow)) return;
                        
                        //Local function
                        void Increment(ref ChartHoldTime ht, in ChartTimeSpan s)
                        {
                            if (!ht.Increment(s))
                            {
                                entityManager.RemoveComponent<WithinJudgeRange>(en);
                                entityManager.AddComponent<PastJudgeRange>(en);
                            }
                        }

                        //Increment or kill holds out of time for judging
                        if (holdTime.CheckOutOfRange(currentTime))
                        {
                            ScoreManager.RegisterLost();
                            lastJudge.value = false;

                            Increment(ref holdTime, in span);
                        }

                        //Invalidate holds not in range; should also rule out all invalid data, i.e. positions with a lane of -1
                        if (!touch.trackRange.Contains(position.lane)) return;

                        //Holds not requiring a tap
                        if (held.value)
                        {
                            //If valid:
                            if (touch.status != TouchPoint.Status.RELEASED)
                            {
                                ScoreManager.RegisterMPure();
                                lastJudge.value = true;

                                Increment(ref holdTime, in span);
                            }
                            //If invalid:
                            else
                            {
                                held.value = false;
                            }
                        }
                        //Holds requiring a tap
                        else if (touch.status == TouchPoint.Status.TAPPED && holdTime.time < minTime)
                        {
                            minTime = holdTime.time;
                            minEntity = en;
                            taptype = Taptype.HOLD_HEAD;
                        }
                    }

                ).Run();

                //Tap notes; no EntityReference, those only exist on arctaps
                Entities.WithAll<WithinJudgeRange>().WithNone<EntityReference>().WithoutBurst().ForEach(

                    (Entity en, in ChartTime time, in ChartPosition position)

                        =>

                    {
                        //Increment or kill taps out of time for judging
                        if (time.CheckOutOfRange(currentTime))
                        {
                            ScoreManager.RegisterLost();
                            commandBuffer.DestroyEntity(en);
                        }

                        //Invalidate if not in range of a tap; should also rule out all invalid data, i.e. positions with a lane of -1
                        if (!touch.trackRange.Contains(position.lane)) return;

                        //Register tap lul
                        if (time.value < minTime)
                        {
                            minTime = time.value;
                            minEntity = en;
                            taptype = Taptype.TAP;
                        }
                    }

                ).Run();
            }

            //Arctap notes
            Entities.WithAll<WithinJudgeRange>().WithoutBurst().ForEach(

                (Entity en, in ChartTime time, in ChartPosition position, in EntityReference enRef)

                    =>

                {
                    //Increment or kill holds out of time for judging
                    if (time.CheckOutOfRange(currentTime))
                    {
                        ScoreManager.RegisterLost();
                        commandBuffer.DestroyEntity(en);
                        commandBuffer.DestroyEntity(enRef.Value);
                    }

                    //Invalidate if not in range of a tap;
                    if (!touch.inputPlane.CollidesWith(new AABB2D(position.xy - arctapBoxExtents, position.xy + arctapBoxExtents)))
                        return;

                    //If minimum, set process
                    if (time.value < minTime)
                    {
                        minTime = time.value;
                        minEntity = en;
                        taptype = Taptype.ARCTAP;
                    }
                }

            ).Run();

            //Call correct function;
            switch(taptype)
            {
                case Taptype.ARCTAP    : ArctapJudge(minEntity) ; break;
                case Taptype.TAP       : TapJudge(minEntity)    ; break;
                case Taptype.HOLD_HEAD : HoldJudge(minEntity)   ; break;
                default                                         : break;
            }

        }

        //TO BE REPLACED
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
                    InputManager.Instance.touchPoints[i].InputPlaneValid &&
                    InputManager.Instance.touchPoints[i].inputPlane.CollidesWith(
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
                        ScoreManager.Instance.AddJudge(JudgeType.LOST);
                    }
                    else
                    {
                        ScoreManager.Instance.AddJudge(JudgeType.MAX_PURE);
                    }

                    entityManager.AddComponent(entity, typeof(Disabled));

                }
            }
        }

        // Destroy array after use
        arcEns.Dispose();

    }
}
