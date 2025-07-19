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
        private const TargetIndex stargateToEnter = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(stargateToEnter);
            this.FailOn(() => !job.GetTarget(stargateToEnter).Thing.TryGetComp<CompStargate>().StargateIsActive);

            yield return Toils_Goto.GotoCell(job.GetTarget(stargateToEnter).Thing.InteractionCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate gateComp = job.GetTarget(stargateToEnter).Thing.TryGetComp<CompStargate>();
                    pawn.DeSpawn();
                    gateComp.AddToSendBuffer(pawn);
                }
            };
        }
    }
}
