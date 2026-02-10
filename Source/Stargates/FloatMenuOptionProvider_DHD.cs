using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace StargatesMod;

public class FloatMenuOptionProvider_Dhd : FloatMenuOptionProvider
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
        if (!dhdComp.IsConnectedToStargate)
        {
            yield return new FloatMenuOption("SGM.CannotDial".Translate("SGM.Reason.NotConnected".Translate()), null);
            yield break;
        }
        
        CompStargate sgComp = dhdComp.GetLinkedStargateComp();

        AcceptanceReport canReachReport = CanReachDhd(context.FirstSelectedPawn, clickedThing);
        if (!canReachReport.Accepted)
        {
            yield return new FloatMenuOption("SGM.CannotDial".Translate(canReachReport.Reason), null);
            yield break;
        }
            
        if (dhdComp.Props.requiresPower)
        {
            CompPowerTrader compPowerTrader = dhdComp.parent.TryGetComp<CompPowerTrader>();
            if (compPowerTrader is { PowerOn: false })
            {
                yield return new FloatMenuOption("SGM.CannotDial".Translate("SGM.Reason.NoPower".Translate()), null);
                yield break;
            }
        }
        if (sgComp.IsHibernating)
        {
            yield return new FloatMenuOption("SGM.CannotDial".Translate("SGM.Reason.Hibernating".Translate()), null);
            yield break;
        }
        if (sgComp.StargateIsActive)
        {
            yield return new FloatMenuOption("SGM.CannotDial".Translate("SGM.Reason.GateIsActive".Translate()), null);
            yield break;
        }
            
        WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
        addressComp.CleanupAddresses();
        if (addressComp.AddressList.Count < 2)
        {
            yield return new FloatMenuOption("SGM.CannotDial".Translate("SGM.Reason.NoDestinations".Translate()), null);
            yield break;
        }

        if (!addressComp.AddressList.Contains(sgComp.GateAddress) && !addressComp.PocketMapAddressList.Contains(sgComp.GateAddress))
        {
            yield return new FloatMenuOption("SGM.CannotDial".Translate("SGM.Reason.InvalidAddress".Translate()), null);
            yield break;
        }
            
        if (sgComp.TicksUntilOpen > -1)
        {
            string failReason = sgComp.IsReceivingGate ? "SGM.Reason.Incoming" : "SGM.Reason.AlreadyDialling";

            yield return new FloatMenuOption("SGM.CannotDial".Translate(failReason.Translate()), null);
            yield break; 
        }
                
        foreach (PlanetTile tile in addressComp.AddressList)
        {
            if (tile == sgComp.GateAddress) continue;
                
            MapParent destMapParent = Find.WorldObjects.MapParentAt(tile);
                
            yield return new FloatMenuOption("SGM.DialGate".Translate(SgUtilities.GetStargateDesignation(tile), destMapParent.Label), () =>
            {
                dhdComp.QueuedAddress = tile;
                dhdComp.DialMode = DialMode.Map;
                
                Job job = JobMaker.MakeJob(SgJobDefOf.StargatesMod_DialStargate, dhdComp.parent);
                context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    
            }, MenuOptionPriority.SummonThreat);
        }

        foreach (int mapIndex in addressComp.PocketMapAddressList)
        {
            if (mapIndex == sgComp.GateAddress.tileId && sgComp.IsInPocketMap) continue;

            PocketMapParent pocketMapParent = Find.Maps[mapIndex].PocketMapParent;
                
            if (pocketMapParent == null)
            {
                Log.Error("[StargatesMod] Could not find PocketMapParent for mapIndex: " + mapIndex);
                continue;
            }

            int index = mapIndex;
            yield return new FloatMenuOption("SGM.DialGate".Translate(SgUtilities.GetStargateDesignation(pocketMapParent.sourceMap.Tile), pocketMapParent.Map.generatorDef.label), () =>
            {
                dhdComp.QueuedAddress = index;
                dhdComp.DialMode = DialMode.PocketMap;
                
                Job job = JobMaker.MakeJob(SgJobDefOf.StargatesMod_DialStargate, dhdComp.parent);
                context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }, MenuOptionPriority.SummonThreat);
        }
    }

    private static AcceptanceReport CanReachDhd(Pawn pawn, Thing dhd) => pawn.CanReach(dhd.InteractionCell, PathEndMode.OnCell, Danger.Deadly) ? true : "NoPath".Translate();
}