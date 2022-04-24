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
            return this.pawn.Reserve(this.job.GetTarget(stargateToEnter), this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(stargateToEnter);

            yield return Toils_Goto.GotoCell(this.job.GetTarget(stargateToEnter).Thing.InteractionCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate gateComp = this.job.GetTarget(stargateToEnter).Thing.TryGetComp<CompStargate>();
                    this.pawn.DeSpawn(DestroyMode.Vanish);
                    gateComp.AddToSendBuffer(this.pawn);
                }
            };
            yield break;
        }
    }
}
