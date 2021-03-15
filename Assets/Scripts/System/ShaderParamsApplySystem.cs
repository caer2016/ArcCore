﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using ArcCore.Data;
using ArcCore.MonoBehaviours;
using ArcCore.Tags;
using Unity.Rendering;

[UpdateAfter(typeof(JudgementSystem))]
public class ShaderParamsApplySystem : SystemBase
{

    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;

        //ARCS
        Entities.WithNone<ChartTime>().WithAll<ColorID>().ForEach(

            (ref ShaderCutoff cutoff, ref ShaderRedmix redmix, in EntityReference eref)

                =>

            {
                HitState hit = entityManager.GetComponentData<HitState>(eref.Value);
                ArcIsRed red = entityManager.GetComponentData<ArcIsRed>(eref.Value);

                if (red.Value)
                {
                    redmix.Value = math.min(redmix.Value + 0.08f, 1);
                }
                else
                {
                    redmix.Value = 0;
                }

                if (hit.Value != 0)
                {
                    cutoff.Value = hit.Value;
                }
            }

        )
            .WithName("ArcShaders")
            .Schedule();

        Dependency.Complete();

        //HOLDS
        Entities.WithNone<Translation>().ForEach(

            (ref ShaderCutoff cutoff, in HitState hit)

                =>

            {
                if (hit.Value != 0)
                {
                    cutoff.Value = hit.Value;
                }
            }

        )
            .WithName("HoldShaders")
            .Schedule();

        //COMPLETE ALL
        Dependency.Complete();
    }
}
