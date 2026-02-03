using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace StargatesMod
{
    public class FloatMenuOptionProvider_Stargate : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => true;

        protected override bool RequiresManipulation => false;

        protected override bool MechanoidCanDo => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            List<Pawn> tmpStargateEnteringPawns = [];
            
            CompStargate sgComp = clickedThing.TryGetComp<CompStargate>();
            
            if (sgComp == null) yield break;
            if (!sgComp.StargateIsActive || sgComp.IrisIsActivated) yield break;
            
            if (!context.IsMultiselect)
            {
                AcceptanceReport acceptanceReport = CanReachStargate(context.FirstSelectedPawn, sgComp.parent);
                if (!acceptanceReport.Accepted)
                {
                    yield return new FloatMenuOption(acceptanceReport.Reason, null);
                    yield break;
                }
            }

            tmpStargateEnteringPawns.Clear();
            tmpStargateEnteringPawns.AddRange(context.ValidSelectedPawns.Where(validSelectedPawn => CanReachStargate(validSelectedPawn, sgComp.parent)));

            if (tmpStargateEnteringPawns.NullOrEmpty()) yield break;
                
            yield return new FloatMenuOption("SGM.EnterStargateAction".Translate(), delegate
            {
                foreach (Pawn tmpStargateEnteringPawn in tmpStargateEnteringPawns)
                {
                    Pawn carriedPawn = tmpStargateEnteringPawn.carryTracker.CarriedThing as Pawn;
                        
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_EnterStargate"), sgComp.parent, carriedPawn);
                    job.playerForced = true;
                    job.count = 1;
                    tmpStargateEnteringPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            }, MenuOptionPriority.SummonThreat);

                
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
                        
                        if (targetPawn != null && targ.Thing == context.FirstSelectedPawn)
                            return false;
                        if (targetPawn != null && targ.Thing.Faction != Faction.OfPlayer && !targetPawn.IsPrisonerOfColony)
                            return false;
                        
                        return targetPawn != null || targ.Thing.def.category == ThingCategory.Item;
                    }
                };
                
                Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo t)
                {
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_BringToStargate"), t.Thing, sgComp.parent); 
                    context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }, MenuOptionPriority.SummonThreat);
        }

        private static AcceptanceReport CanReachStargate(Pawn pawn, Thing stargate)
        {
            if (!pawn.CanReach(stargate, PathEndMode.ClosestTouch, Danger.Deadly))
                return "NoPath".Translate();
            
            return true;
        }
    }
}
