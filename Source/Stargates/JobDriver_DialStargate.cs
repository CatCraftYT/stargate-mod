using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace StargatesMod;

public class JobDriver_DialStargate : JobDriver
{
    private const TargetIndex targetDHD = TargetIndex.A;
        
    private readonly StargatesModSettings _modSettings = LoadedModManager.GetMod<StargatesMod>().GetSettings<StargatesModSettings>();

    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.GetTarget(targetDHD), job);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        CompDialHomeDevice dhdComp = job.GetTarget(targetDHD).Thing.TryGetComp<CompDialHomeDevice>();
        CompStargate sgComp = dhdComp.GetLinkedStargateComp();
        
        this.FailOnDestroyedOrNull(targetDHD);
        this.FailOn(() => sgComp.StargateIsActive);
        this.FailOn(() => sgComp.IsExpectingIncomingWormhole);
            
        IntVec3 targetCell = job.GetTarget(targetDHD).Thing.InteractionCell;
            
        // If self-dialler, make pawn initiate dial from the (left) side instead of the center
        int subX = dhdComp.parent.def.size.x / 2;
        if (dhdComp.Props.selfDialler)
            targetCell = job.GetTarget(targetDHD).Thing.InteractionCell + new IntVec3(-subX, 0, 0).RotatedBy(sgComp.parent.Rotation);
            
            
            
        yield return Toils_Goto.GotoCell(targetCell, PathEndMode.OnCell);
        yield return new Toil
        {
            initAction = () =>
            {
                int lockDelayTicks = _modSettings.ShortenGateDialSeq ? 200 : 900;

                sgComp.OpenStargateDelayed(dhdComp.QueuedAddress, lockDelayTicks, dhdComp.DialMode);
                dhdComp.QueuedAddress = -1;
                    
                if (!dhdComp.Props.selfDialler) SgSoundDefOf.StargateMod_DhdUsual_1.PlayOneShot(SoundInfo.InMap(dhdComp.parent));
            }
        };
    }
}