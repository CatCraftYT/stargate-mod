using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld.Planet;
using RimWorld;
using System.Text;

namespace StargatesMod;

public class SitePartWorker_Stargate : SitePartWorker
{
    public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart) => "SGM.GateAddress".Translate(SgUtilities.GetStargateDesignation(site.Tile));
 
    public override void PostMapGenerate(Map map)
    {
        base.PostMapGenerate(map);
        if (map == null) { Log.Error("SitePartWorker map was null on PostMapGenerate. That makes no sense."); return; }
            
        Thing gateOnMap = SgUtilities.GetAllStargatesOnMap(map).FirstOrFallback();
        if (gateOnMap == null) { Log.Error("[StargatesMod] Stargate was expected but not found on generated map."); return; }
            
        CompStargate gateComp = gateOnMap.TryGetComp<CompStargate>();
            
        IEnumerable<IntVec3> vortexCells = gateComp.VortexCells;
        List<IntVec3> gateRadiusCells = GenRadial.RadialCellsAround(gateOnMap.InteractionCell, 11, true).ToList();

        //move pawns away from vortex
        foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(p => gateRadiusCells.Contains(p.Position)))
        {
            Room pawnRoom = pawn.Position.GetRoom(pawn.Map);

            List<IntVec3> nearSafeCells = GenRadial.RadialCellsAround(pawn.Position, 9, true).Where(c => 
                c.InBounds(map) && c.Walkable(map) && c.GetRoom(map) == pawnRoom && !vortexCells.Contains(c)).ToList();
                
            if (!nearSafeCells.Any()) continue;
                    
            pawn.Position = nearSafeCells.RandomElement();
            pawn.pather.StopDead();
            pawn.jobs.StopAll();
        }
            
        //Fix stackCounts of certain items (especially things certain cases with stack increasing mods)
        //also things like res mech serums even without that
        //TODO remove when replacing loot on ground with loot containers
        ThingFilter itemsToRebalance = new();
        itemsToRebalance.SetAllow(ThingCategoryDefOf.BodyParts, true);
        itemsToRebalance.SetAllow(ThingCategoryDef.Named("Artifacts"), true);
        itemsToRebalance.SetAllow(ThingDef.Named("MechSerumResurrector"), true);
        itemsToRebalance.SetAllow(ThingDef.Named("MechSerumHealer"), true);
            
        foreach (Thing removedThing in from thing in map.listerThings.ThingsMatchingFilter(itemsToRebalance) 
                 where thing.stackCount > 1 select thing.SplitOff(thing.stackCount - 1) 
                 into removedThing where !removedThing.DestroyedOrNull() select removedThing)
        {
            removedThing.Destroy();
        }
    }
}