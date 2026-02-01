using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class JobDriver_WatchStargate : JobDriver
    {
        private const TargetIndex stargateToWatch = TargetIndex.A;
        private const TargetIndex watchPosition = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(stargateToWatch);
            /*this.FailOn(() => !job.GetTarget(stargateToWatch).Thing.TryGetComp<CompStargate>().StargateIsActive);*/
            this.FailOn(() => pawn.DeadOrDowned);

            CompStargate gateComp = job.GetTarget(stargateToWatch).Thing.TryGetComp<CompStargate>();
            Toil watch;
            
            
            yield return Toils_Goto.GotoCell(watchPosition, PathEndMode.OnCell);

            watch = ToilMaker.MakeToil();
            watch.AddPreTickIntervalAction(WatchTickAction);
            watch.handlingFacing = true;
            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = 900;
            
            yield return watch;
        }

        protected void WatchTickAction(int delta)
        {
            pawn.rotationTracker.FaceCell(job.GetTarget(stargateToWatch).Cell);
        }
    }
}