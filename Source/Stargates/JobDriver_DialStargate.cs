using System.Collections.Generic;
using StargatesMod.Mod_Settings;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace StargatesMod
{
    public class JobDriver_DialStargate : JobDriver
    {
        private const TargetIndex targetDHD = TargetIndex.A;
        
        private readonly StargatesMod_Settings _settings = LoadedModManager.GetMod<StargatesMod_Mod>().GetSettings<StargatesMod_Settings>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(targetDHD), job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompDialHomeDevice dhdComp = job.GetTarget(targetDHD).Thing.TryGetComp<CompDialHomeDevice>();
            this.FailOnDestroyedOrNull(targetDHD);
            this.FailOn(() => dhdComp.GetLinkedStargateComp().StargateIsActive);
            
            IntVec3 targetCell = job.GetTarget(targetDHD).Thing.InteractionCell;
            
            // If self-dialler, make pawn initiate dial from the (left) side instead of the center
            int subX = dhdComp.parent.def.size.x / 2;
            if (dhdComp.Props.selfDialler)
                targetCell = job.GetTarget(targetDHD).Thing.InteractionCell + new IntVec3(-subX, 0, 0).RotatedBy(dhdComp.GetLinkedStargateComp().parent.Rotation);
            
            
            
            yield return Toils_Goto.GotoCell(targetCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
                    CompStargate linkedStargate = dhdComp.GetLinkedStargateComp();
                    int lockDelay = 900;
                    if (_settings.ShortenGateDialSeq) lockDelay = 200;

                    if (dhdComp.queuedAddress > -1)
                    {
                        linkedStargate.OpenStargateDelayed(dhdComp.queuedAddress, lockDelay);
                        dhdComp.queuedAddress = -1;
                    }
                    else
                    {
                        linkedStargate.OpenStargateDelayed(dhdComp.queuedPocketMapAddress, lockDelay);
                        dhdComp.queuedPocketMapAddress = -1;
                    }
                    
                    if (!dhdComp.Props.selfDialler) SGSoundDefOf.StargateMod_DhdUsual_1.PlayOneShot(SoundInfo.InMap(dhdComp.parent));
                }
            };
        }
    }
}