using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class JobDriver_BringToStargate : JobDriver
    {
        private const TargetIndex thingToHaul = TargetIndex.A;
        private const TargetIndex targetStargate = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing thing = (Thing)this.job.GetTarget(thingToHaul);
            this.job.count = thing.stackCount;
            return this.pawn.Reserve(thing, this.job, 1, thing.stackCount) &&
                this.pawn.Reserve(thing, this.job, 1, thing.stackCount);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Thing thing = (Thing)this.job.GetTarget(thingToHaul);

            this.FailOnDestroyedOrNull(targetStargate);
            this.FailOnDestroyedNullOrForbidden(thingToHaul);
            this.FailOn(() => !this.job.GetTarget(targetStargate).Thing.TryGetComp<CompStargate>().stargateIsActive);
            if (thing as Pawn != null) { this.FailOnMobile(thingToHaul); }

            yield return Toils_Goto.GotoCell(thingToHaul, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(thingToHaul);
            yield return Toils_Goto.GotoCell(this.job.GetTarget(targetStargate).Thing.InteractionCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate gateComp = this.job.GetTarget(targetStargate).Thing.TryGetComp<CompStargate>();
                    this.pawn.carryTracker.innerContainer.Remove(thing);
                    gateComp.AddToSendBuffer(thing);
                }
            };
            yield break;
        }
    }
}
