using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace StargatesMod;

public class JobDriver_EnterStargate : JobDriver
{
    private const TargetIndex stargateTarg = TargetIndex.A;
    private const TargetIndex carriedPawnTarg = TargetIndex.B; // Optional

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        CompStargate sgComp = job.GetTarget(stargateTarg).Thing.TryGetComp<CompStargate>();
        
        this.FailOnDestroyedOrNull(stargateTarg);
        this.FailOn(() => !sgComp.StargateIsActive);

        
        Pawn carriedPawn = (Pawn)job.GetTarget(carriedPawnTarg).Thing;
        bool carryPawnToGate = carriedPawn != null;
            
        if (carryPawnToGate) yield return Toils_Haul.StartCarryThing(carriedPawnTarg);
            
        yield return Toils_Goto.GotoCell(sgComp.parent.InteractionCell, PathEndMode.OnCell);
        if (carryPawnToGate)
        {
            yield return new Toil
            {
                initAction = () =>
                {
                    if (!pawn.carryTracker.innerContainer.Contains(carriedPawn))
                    {
                        carriedPawn = null;
                        return;
                    }
                        
                    pawn.carryTracker.innerContainer.Remove(carriedPawn);
                }
            };
        }
        yield return new Toil
        {
            initAction = () =>
            {
                bool drafted = pawn.Drafted;
                pawn.DeSpawn();
                sgComp.AddToSendBuffer(new BufferItem(pawn, drafted, carriedPawn));
            }
        };
    }
}