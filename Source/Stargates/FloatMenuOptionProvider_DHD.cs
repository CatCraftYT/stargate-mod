using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace StargatesMod
{
    public class FloatMenuOptionProvider_DHD : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => true;

        protected override bool RequiresManipulation => true;

        protected override bool MechanoidCanDo => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            CompDialHomeDevice dhdComp =  clickedThing.TryGetComp<CompDialHomeDevice>();
            if (dhdComp == null) yield break;
            CompStargate sgComp = dhdComp.GetLinkedStargateComp();
            
            
            if (!CanReachDHD(context.FirstSelectedPawn, clickedThing) || !dhdComp.IsConnectedToStargate) yield break;
            
            if (dhdComp.Props.requiresPower)
            {
                CompPowerTrader compPowerTrader = dhdComp.parent.TryGetComp<CompPowerTrader>();
                if (compPowerTrader != null && !compPowerTrader.PowerOn)
                {
                    yield return new FloatMenuOption("SGM.CannotDialNoPower".Translate(), null);
                    yield break;
                }
            }
            if (sgComp.IsHibernating)
            {
                yield return new FloatMenuOption("SGM.CannotDialHibernating".Translate(), null);
                yield break;
            }
            if (sgComp.StargateIsActive)
            {
                yield return new FloatMenuOption("SGM.CannotDialGateIsActive".Translate(), null);
                yield break;
            }
            
            WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
            addressComp.CleanupAddresses();
            if (addressComp.AddressList.Count < 2)
            {
                yield return new FloatMenuOption("SGM.CannotDialNoDestinations".Translate(), null);
                yield break;
            }

            if (!addressComp.AddressList.Contains(sgComp.GateAddress))
            {
                if (!addressComp.PocketMapAddressList.Contains(sgComp.PocketMapGateAddress))
                {
                    yield return new FloatMenuOption("SGM.CannotDialInvalidAddress".Translate(), null);
                    yield break;
                }
            }
            
            if (sgComp.TicksUntilOpen > -1)
            {
                if (sgComp.IsReceivingGate)
                {
                    yield return new FloatMenuOption("SGM.CannotDialIncoming".Translate(), null);
                    yield break;
                }
                yield return new FloatMenuOption("SGM.CannotDialAlreadyDialling".Translate(), null);
                yield break; 
            }
                
            foreach (PlanetTile pT in addressComp.AddressList)
            {
                if (pT == sgComp.GateAddress) continue;
                
                MapParent destMapParent = Find.WorldObjects.MapParentAt(pT);
                MenuOptionPriority priority = MenuOptionPriority.High;
                
                yield return new FloatMenuOption("SGM.DialGate".Translate(CompStargate.GetStargateDesignation(pT), destMapParent.Label), () =>
                {
                    dhdComp.queuedAddress = pT;
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DialStargate"), dhdComp.parent);
                    context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    
                }, MenuOptionPriority.SummonThreat);
            }

            for (var i = 0; i < addressComp.PocketMapAddressList.Count; i++)
            {
                var mapIndex = addressComp.PocketMapAddressList[i];
                if (mapIndex == sgComp.PocketMapGateAddress) continue;

                PocketMapParent pMParent = Find.Maps[mapIndex].PocketMapParent;
                
                if (pMParent == null)
                {
                    Log.Error("[StargatesMod] PocketMapParent was null in FloatMenuOptionProvider_DHD");
                    continue;
                }

                yield return new FloatMenuOption("SGM.DialGate".Translate("PM-" + i, pMParent.Map.generatorDef.label), () =>
                {
                    dhdComp.queuedPocketMapAddress = mapIndex;
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DialStargate"), dhdComp.parent);
                    context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }, MenuOptionPriority.SummonThreat);
            }
        }

        private static AcceptanceReport CanReachDHD(Pawn pawn, Thing dhd)
        {
            if (!pawn.CanReach(dhd, PathEndMode.ClosestTouch, Danger.Deadly))
                return "NoPath".Translate();
            
            return true;
        }
    }
}
