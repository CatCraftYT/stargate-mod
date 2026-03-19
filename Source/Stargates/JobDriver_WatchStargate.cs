using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod;

public class JobDriver_WatchStargate : JobDriver
{
    private const TargetIndex stargateToWatch = TargetIndex.A;
    private const TargetIndex watchPosition = TargetIndex.B;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(stargateToWatch);
        this.FailOn(() => pawn.DeadOrDowned);


        yield return Toils_Goto.GotoCell(watchPosition, PathEndMode.OnCell);

        Toil watch = ToilMaker.MakeToil();
        watch.AddPreTickIntervalAction(WatchTickAction);
        watch.handlingFacing = true;
        watch.defaultCompleteMode = ToilCompleteMode.Delay;
        watch.defaultDuration = 900;
            
        yield return watch;
    }

    private void WatchTickAction(int delta) => pawn.rotationTracker.FaceCell(job.GetTarget(stargateToWatch).Cell);
}