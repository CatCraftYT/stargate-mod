using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class JobDriver_InstallIris : JobDriver
    {
        private const TargetIndex irisItem = TargetIndex.A;
        private const TargetIndex targetStargate = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            this.job.count = 1;
            Thing stargate = (Thing)this.job.GetTarget(targetStargate);
            Thing iris = (Thing)this.job.GetTarget(irisItem);
            return this.pawn.Reserve(stargate, this.job) &&
                this.pawn.Reserve(iris, this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            int useDuration = this.job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompUsable>().Props.useDuration;
            Thing iris = (Thing)this.job.GetTarget(irisItem);

            this.FailOnDestroyedOrNull(targetStargate);
            this.FailOnDestroyedNullOrForbidden(irisItem);

            yield return Toils_Goto.GotoThing(irisItem, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(irisItem);
            yield return Toils_Goto.GotoThing(targetStargate, PathEndMode.Touch);
            Toil toil = Toils_General.Wait(useDuration);
            toil.WithProgressBarToilDelay(targetStargate);
            toil.WithEffect(base.TargetThingB.def.repairEffect, TargetIndex.B);
            yield return toil;
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate gateComp = this.job.GetTarget(targetStargate).Thing.TryGetComp<CompStargate>();
                    this.pawn.carryTracker.innerContainer.Remove(iris);
                    iris.Destroy();
                    gateComp.hasIris = true;
                }
            };
            yield break;
        }
    }
}
