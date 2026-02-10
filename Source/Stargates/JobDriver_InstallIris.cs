using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace StargatesMod;

public class JobDriver_InstallIris : JobDriver
{
    private const TargetIndex irisItemTarg = TargetIndex.A;
    private const TargetIndex stargateTarg = TargetIndex.B;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.GetTarget(stargateTarg).Thing, job) && pawn.Reserve(job.GetTarget(irisItemTarg).Thing, job);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        int useDuration = job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompUsable>().Props.useDuration;
        Thing irisItem = (Thing)job.GetTarget(irisItemTarg);

        this.FailOnDestroyedOrNull(stargateTarg);
        this.FailOnDestroyedNullOrForbidden(irisItemTarg);

        yield return Toils_Goto.GotoThing(irisItemTarg, PathEndMode.Touch);
        yield return Toils_Haul.StartCarryThing(irisItemTarg);
        yield return Toils_Goto.GotoThing(stargateTarg, PathEndMode.Touch);
            
        Toil toil = Toils_General.Wait(useDuration);
        toil.WithProgressBarToilDelay(stargateTarg);
        toil.WithEffect(TargetThingB.def.repairEffect, TargetIndex.B);
        yield return toil;
            
        yield return new Toil
        {
            initAction = () =>
            {
                CompStargate gateComp = job.GetTarget(stargateTarg).Thing.TryGetComp<CompStargate>();
                    
                pawn.carryTracker.innerContainer.Remove(irisItem);
                irisItem.Destroy();
                gateComp.HasIris = true;
            }
        };
    }
}