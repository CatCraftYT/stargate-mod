using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace StargatesMod
{
    public class FloatMenuOptionProvider_Stargate : FloatMenuOptionProvider
    {
        private static List<Pawn> tmpStargateEnteringPawns = new List<Pawn>();

        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => true;

        protected override bool RequiresManipulation => false;

        protected override bool MechanoidCanDo => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
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
            foreach (Pawn validSelectedPawn in context.ValidSelectedPawns)
            {
                if (CanReachStargate(validSelectedPawn, sgComp.parent))
                {
                    tmpStargateEnteringPawns.Add(validSelectedPawn);
                }
            }

            if (!tmpStargateEnteringPawns.NullOrEmpty())
            {
                yield return new FloatMenuOption("EnterStargateAction".Translate(), delegate
                {
                    foreach (Pawn tmpStargateEnteringPawn in tmpStargateEnteringPawns)
                    {
                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_EnterStargate"), sgComp.parent);
                        job.playerForced = true;
                        tmpStargateEnteringPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                }, MenuOptionPriority.High);

                
                yield return new FloatMenuOption("BringThingToGateAction".Translate(), () =>
                {
                    TargetingParameters targetingParameters = new TargetingParameters()
                    { 
                        canTargetSelf = false,
                        onlyTargetIncapacitatedPawns = false,
                        canTargetBuildings = false,
                        canTargetItems = true,
                        canTargetAnimals = true,
                        mapObjectTargetsMustBeAutoAttackable = false,
                        validator = (Predicate<TargetInfo>) (targ =>
                        {
                            if (!targ.HasThing)
                                return false;
                            if (targ.Thing is Pawn && targ.Thing == context.FirstSelectedPawn)
                                return false;
                            if (targ.Thing is Pawn targ2 && targ.Thing.Faction != Faction.OfPlayer && !targ2.IsPrisonerOfColony)
                                return false;
                            if (!(targ.Thing is Pawn) && targ.Thing.def.category != ThingCategory.Item)
                                return false;
                            return true;
                        })
                    };
                
                    Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo t)
                    {
                        Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_BringToStargate"), t.Thing, sgComp.parent); 
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    });
                });
            }
        }

        private static AcceptanceReport CanReachStargate(Pawn pawn, Thing stargate)
        {
            if (!pawn.CanReach(stargate, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                return "NoPath".Translate();
            }
            return true;
        }
    }
}
