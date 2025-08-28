using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class JobDriver_EnterStargate : JobDriver
    {
        private const TargetIndex stargateTarg = TargetIndex.A;
        private const TargetIndex carriedPawnTarg = TargetIndex.B; // Optional

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(stargateTarg);
            this.FailOn(() => !job.GetTarget(stargateTarg).Thing.TryGetComp<CompStargate>().StargateIsActive);

            CompStargate gateComp = job.GetTarget(stargateTarg).Thing.TryGetComp<CompStargate>();
            Pawn carriedPawn = (Pawn)job.GetTarget(carriedPawnTarg).Thing;
            bool carryPawnToGate = carriedPawn != null;
            
            if (carryPawnToGate) yield return Toils_Haul.StartCarryThing(carriedPawnTarg);
            
            yield return Toils_Goto.GotoCell(job.GetTarget(stargateTarg).Thing.InteractionCell, PathEndMode.OnCell);
            if (carryPawnToGate)
            {
                yield return new Toil
                {
                    initAction = () =>
                    {
                        pawn.carryTracker.innerContainer.Remove(carriedPawn);
                        gateComp.AddToSendBuffer(carriedPawn);
                    }
                };
            }
            yield return new Toil
            {
                initAction = () =>
                {
                    pawn.DeSpawn();
                    gateComp.AddToSendBuffer(pawn);
                }
            };
        }
    }
}
