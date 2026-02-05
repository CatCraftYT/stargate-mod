using System.Collections.Generic;
using System.Linq;
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
            
            
        if (!CanReachDhd(context.FirstSelectedPawn, clickedThing) || !dhdComp.IsConnectedToStargate) yield break;
            
        if (dhdComp.Props.requiresPower)
        {
            CompPowerTrader compPowerTrader = dhdComp.parent.TryGetComp<CompPowerTrader>();
            if (compPowerTrader is { PowerOn: false })
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
            if (!addressComp.PocketMapAddressList.Contains(sgComp.GateAddressPocketMap))
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
                
        foreach (PlanetTile tile in addressComp.AddressList)
        {
            if (tile == sgComp.GateAddress) continue;
                
            MapParent destMapParent = Find.WorldObjects.MapParentAt(tile);
                
            yield return new FloatMenuOption("SGM.DialGate".Translate(CompStargate.GetStargateDesignation(tile), destMapParent.Label), () =>
            {
                dhdComp.queuedAddress = tile;
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DialStargate"), dhdComp.parent);
                context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    
            }, MenuOptionPriority.SummonThreat);
        }

        foreach (int mapIndex in addressComp.PocketMapAddressList)
        {
            if (mapIndex == sgComp.GateAddressPocketMap) continue;

            PocketMapParent pocketMapParent = Find.Maps[mapIndex].PocketMapParent;
                
            if (pocketMapParent == null)
            {
                Log.Error("[StargatesMod] Could not find PocketMapParent for mapIndex: " + mapIndex);
                continue;
            }

            int index = mapIndex;
            yield return new FloatMenuOption("SGM.DialGate".Translate(CompStargate.GetStargateDesignation(pocketMapParent.sourceMap.Tile), pocketMapParent.Map.generatorDef.label), () =>
            {
                dhdComp.queuedPocketMapAddress = index;
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DialStargate"), dhdComp.parent);
                context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }, MenuOptionPriority.SummonThreat);
        }
    }

    private static AcceptanceReport CanReachDhd(Pawn pawn, Thing dhd)
    {
        if (!pawn.CanReach(dhd, PathEndMode.ClosestTouch, Danger.Deadly))
            return "NoPath".Translate();
            
        return true;
    }
}