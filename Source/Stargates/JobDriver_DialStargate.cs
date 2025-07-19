using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using XmlExtensions;

namespace StargatesMod
{
    public class JobDriver_DialStargate : JobDriver
    {
        private const TargetIndex targetDHD = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(targetDHD), job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompDialHomeDevice dhdComp = job.GetTarget(targetDHD).Thing.TryGetComp<CompDialHomeDevice>();
            this.FailOnDestroyedOrNull(targetDHD);
            this.FailOn(() => dhdComp.GetLinkedStargate().StargateIsActive);

            yield return Toils_Goto.GotoCell(job.GetTarget(targetDHD).Thing.InteractionCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate linkedStargate = dhdComp.GetLinkedStargate();
                    linkedStargate.OpenStargateDelayed(dhdComp.lastDialledAddress, 200);
                }
            };
        }
    }
}
