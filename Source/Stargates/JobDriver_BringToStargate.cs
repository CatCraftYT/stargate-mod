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
            Thing thing = (Thing)job.GetTarget(thingToHaul);
            job.count = thing.stackCount;
            return pawn.Reserve(thing, job, 1, thing.stackCount) &&
                pawn.Reserve(thing, job, 1, thing.stackCount);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Thing thing = (Thing)job.GetTarget(thingToHaul);

            this.FailOnDestroyedOrNull(targetStargate);
            this.FailOnDestroyedNullOrForbidden(thingToHaul);
            this.FailOn(() => !job.GetTarget(targetStargate).Thing.TryGetComp<CompStargate>().StargateIsActive);
            /*if (thing is Pawn) this.FailOnMobile(thingToHaul);*/

            yield return Toils_Goto.GotoCell(thingToHaul, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(thingToHaul);
            yield return Toils_Goto.GotoCell(job.GetTarget(targetStargate).Thing.InteractionCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate gateComp = job.GetTarget(targetStargate).Thing.TryGetComp<CompStargate>();
                    pawn.carryTracker.innerContainer.Remove(thing);
                    gateComp.AddToSendBuffer(thing);
                }
            };
        }
    }
}
