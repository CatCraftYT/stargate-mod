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
            job.count = 1;
            Thing stargate = (Thing)job.GetTarget(targetStargate);
            Thing iris = (Thing)job.GetTarget(irisItem);
            return pawn.Reserve(stargate, job) &&
                pawn.Reserve(iris, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            int useDuration = job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompUsable>().Props.useDuration;
            Thing iris = (Thing)job.GetTarget(irisItem);

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
                    CompStargate gateComp = job.GetTarget(targetStargate).Thing.TryGetComp<CompStargate>();
                    pawn.carryTracker.innerContainer.Remove(iris);
                    iris.Destroy();
                    gateComp.HasIris = true;
                }
            };
        }
    }
}
