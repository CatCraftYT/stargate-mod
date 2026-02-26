using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace StargatesMod;

public class FloatMenuOptionProvider_Stargate : FloatMenuOptionProvider
{
    protected override bool Drafted => true;

    protected override bool Undrafted => true;

    protected override bool Multiselect => true;

    protected override bool RequiresManipulation => false;

    protected override bool MechanoidCanDo => true;

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
    {
        List<Pawn> stargateEnteringPawns = [];
            
        CompStargate sgComp = clickedThing.TryGetComp<CompStargate>();
            
        if (sgComp == null) yield break;
        if (!sgComp.StargateIsActive || sgComp.IrisIsActivated) yield break;

        if (!context.IsMultiselect)
        {
            AcceptanceReport reachGateReport = CanReachStargate(context.FirstSelectedPawn, sgComp.parent);

            if (!reachGateReport.Accepted)
            {
                yield return new FloatMenuOption(reachGateReport.Reason, null);
                yield break;
            }
        }

        stargateEnteringPawns.Clear();
        stargateEnteringPawns.AddRange(context.ValidSelectedPawns.Where(validSelectedPawn => CanReachStargate(validSelectedPawn, sgComp.parent)));

        if (stargateEnteringPawns.NullOrEmpty()) yield break;
                
        yield return new FloatMenuOption("SGM.EnterStargateAction".Translate(), delegate
        {
            foreach (Pawn pawn in stargateEnteringPawns)
            {
                Pawn carriedPawn = pawn.carryTracker.CarriedThing as Pawn;
                        
                Job job = JobMaker.MakeJob(SgJobDefOf.StargatesMod_EnterStargate, sgComp.parent, carriedPawn);
                job.playerForced = true;
                job.count = 1;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }, MenuOptionPriority.SummonThreat);

            
        List<Pawn> pawnsCarryingPawns = stargateEnteringPawns.Where(p => p.carryTracker.CarriedThing is Pawn).ToList();
        if (!pawnsCarryingPawns.NullOrEmpty())
        {
            yield return new FloatMenuOption("SGM.CarryHeldPawnToStargateAction".Translate(), delegate
            {
                foreach (Pawn pawn in pawnsCarryingPawns)
                {
                    Pawn carriedPawn = pawn.carryTracker.CarriedThing as Pawn;
                        
                    Job job = JobMaker.MakeJob(SgJobDefOf.StargatesMod_BringToStargate, carriedPawn, sgComp.parent);
                    job.playerForced = true;
                    job.count = 1;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            }, MenuOptionPriority.SummonThreat);
        }
        else
        {
            yield return new FloatMenuOption("SGM.BringThingToGateAction".Translate(), () =>
            {
                TargetingParameters targetingParameters = new()
                { 
                    canTargetSelf = false,
                    onlyTargetIncapacitatedPawns = false,
                    canTargetBuildings = false,
                    canTargetItems = true,
                    canTargetAnimals = true,
                    mapObjectTargetsMustBeAutoAttackable = false,
                    validator = targ =>
                    {
                        if (!targ.HasThing) return false;
                        
                        Pawn targetPawn = targ.Thing as Pawn;
                        Pawn selectedPawn = context.FirstSelectedPawn;

                        if (targetPawn != null && (targetPawn == selectedPawn || (targetPawn.Faction != Faction.OfPlayer && !targetPawn.IsPrisonerOfColony)
                                                                              || (selectedPawn.IsColonyMech && targetPawn == selectedPawn.GetOverseer())))
                            return false;
                        
                        
                        return targetPawn != null || targ.Thing.def.category == ThingCategory.Item;
                    }
                };
                
                Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo t)
                {
                    Job job = JobMaker.MakeJob(SgJobDefOf.StargatesMod_BringToStargate, t.Thing, sgComp.parent); 
                    context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }, MenuOptionPriority.SummonThreat);
        }
    }

    private static AcceptanceReport CanReachStargate(Pawn pawn, Thing stargate) => pawn.CanReach(stargate, PathEndMode.ClosestTouch, Danger.Deadly) ? true : "NoPath".Translate();
}