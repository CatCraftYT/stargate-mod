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
        
        CompStargate sgComp = dhdComp.GetLinkedStargateComp();
        
        AcceptanceReport reachDhdReport = CanReachDhd(context.FirstSelectedPawn, clickedThing);
        if (!reachDhdReport.Accepted)
        {
            yield return new FloatMenuOption(reachDhdReport.Reason.Translate(), null);
            yield break;
        }
        
        WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
        addressComp.CleanupAddresses();

        AcceptanceReport dialReport = CanDialGate(dhdComp, sgComp, addressComp);
        if (!dialReport.Accepted)
        {
            yield return new FloatMenuOption("SGM.CannotDial".Translate(dialReport.Reason.Translate()), null);
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

    private static AcceptanceReport CanDialGate(CompDialHomeDevice dhdComp, CompStargate sgComp, WorldComp_StargateAddresses addressComp)
    {
        if (!dhdComp.IsConnectedToStargate) return "SGM.Reason.NotConnected";
        if (dhdComp.Props.requiresPower) return "SGM.Reason.NoPower";
        if (sgComp.IsHibernating) return "SGM.Reason.Hibernating";
        if (sgComp.StargateIsActive) return "SGM.Reason.GateIsActive";
        if (!addressComp.EnoughAddressesToDial()) return "SGM.Reason.NoDestinations";
        if (!addressComp.IsRegistered(sgComp.GateAddress)) return "SGM.Reason.InvalidAddress";
        if (sgComp.IsExpectingIncomingWormhole) return "SGM.Reason.Incoming";
        if (sgComp.TicksUntilOpen > -1) return sgComp.IsReceivingGate ? "SGM.Reason.Incoming" : "SGM.Reason.AlreadyDialling";

        return true;
    }
    
    private static AcceptanceReport CanReachDhd(Pawn pawn, Thing dhd) => pawn.CanReach(dhd.InteractionCell, PathEndMode.OnCell, Danger.Deadly) ? true : "NoPath";
}