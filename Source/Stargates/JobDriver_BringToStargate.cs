using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace StargatesMod;

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
        this.FailOn(() => !job.GetTarget(targetStargate).Thing.TryGetComp<CompStargate>().StargateIsActive);
            
            
        yield return Toils_Goto.GotoCell(thingToHaul, PathEndMode.Touch);
        yield return Toils_Haul.StartCarryThing(thingToHaul);
        yield return Toils_Goto.GotoCell(job.GetTarget(targetStargate).Thing.InteractionCell, PathEndMode.OnCell).FailOn(() => job.GetTarget(thingToHaul).Thing.Spawned);
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