using System.Linq;
using Verse;
using RimWorld.Planet;
using RimWorld;
using System.Text;

namespace StargatesMod
{
    public class SitePartWorker_Stargate : SitePartWorker
    {
        public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("SGM.GateAddress".Translate(CompStargate.GetStargateDesignation(site.Tile)));
            return sb.ToString();
        }
 
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null) { Log.Error("SitePartWorker map was null on PostMapGenerate. That makes no sense."); return; }
            
            Thing gateOnMap = CompStargate.GetStargateOnMap(map);
            if (gateOnMap == null) { Log.Error("[StargatesMod] Stargate was expected but not found on generated map."); return; }
            
            CompStargate gateComp = gateOnMap.TryGetComp<CompStargate>();
            
            var vortexCells = gateComp.VortexCells;
            var gateRadiusCells = GenRadial.RadialCellsAround(gateOnMap.InteractionCell, 11, true).ToList();

            //move pawns away from vortex
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                Room pawnRoom = pawn.Position.GetRoom(pawn.Map);
                if (!gateRadiusCells.Contains(pawn.Position)) continue;

                var nearSafeCells = GenRadial.RadialCellsAround(pawn.Position, 9, true).Where(c => 
                    c.InBounds(map) && c.Walkable(map) && c.GetRoom(map) == pawnRoom && !vortexCells.Contains(c)).ToList();
                
                if (!nearSafeCells.Any()) continue;
                    
                pawn.Position = nearSafeCells.RandomElement();
                pawn.pather.StopDead();
                pawn.jobs.StopAll();
            }
            
            //Fix stackCounts of certain items (especially things certain cases with stack increasing mods)
            //also things like res mech serums even without that
            ThingFilter itemsToRebalance = new ThingFilter();
            itemsToRebalance.SetAllow(ThingCategoryDefOf.BodyParts, true);
            itemsToRebalance.SetAllow(ThingCategoryDef.Named("Artifacts"), true);
            itemsToRebalance.SetAllow(ThingDef.Named("MechSerumResurrector"), true);
            itemsToRebalance.SetAllow(ThingDef.Named("MechSerumHealer"), true);
            
            foreach (Thing thing in map.listerThings.ThingsMatchingFilter(itemsToRebalance))
            {
                if (thing.stackCount <= 1) continue;
                
                Thing removedThing = thing.SplitOff(thing.stackCount - 1);
                if (!removedThing.DestroyedOrNull()) removedThing.Destroy();
            }
        }
    }
}