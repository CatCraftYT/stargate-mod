using System;
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
            sb.Append("GateAddress".Translate(CompStargate.GetStargateDesignation(site.Tile)));
            return sb.ToString();
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null) { Log.Error("SitePartWorker map was null on PostMapGenerate. That makes no sense."); return; }
            Thing gateOnMap = CompStargate.GetStargateOnMap(map);
            var VortexCells = gateOnMap.TryGetComp<CompStargate>().VortexCells;

            //move pawns away from vortex
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                Room pawnRoom = pawn.Position.GetRoom(pawn.Map);
                var cells = GenRadial.RadialCellsAround(pawn.Position, 9, true).Where(c => c.InBounds(map) && c.Walkable(map) && c.GetRoom(map) == pawnRoom && !VortexCells.Contains(c));
                var cellsList = cells.ToList();
                if (!cellsList.Any()) continue;
                pawn.Position = cellsList.RandomElement();
                pawn.pather.StopDead();
                pawn.jobs.StopAll();
            }
            
            //Fix stackCounts of certain items (especially things certain cases with stack increasing mods)
            // also things like res mech serums even without that
            //TODO (Alyssa) remove when switching structures to crate system
            foreach (Thing thing in map.listerThings.AllThings.Where(t => t.HasThingCategory(ThingCategoryDefOf.BodyParts)))
            {
                if (thing.stackCount > 1)
                {
                    Thing removedThing = thing.SplitOff(thing.stackCount - 1);
                    if (!removedThing.DestroyedOrNull()) removedThing.Destroy();
                }
            }
            foreach (Thing thing in map.listerThings.AllThings.Where(t => t.def.defName == "MechSerumResurrector" || t.def.defName == "MechSerumHealer"))
            {
                if (thing.stackCount > 1)
                {
                    Thing removedThing = thing.SplitOff(thing.stackCount - 1);
                    if (!removedThing.DestroyedOrNull()) removedThing.Destroy();
                }
            }
            foreach (Thing thing in map.listerThings.AllThings.Where(t => t.HasThingCategory(ThingCategoryDef.Named("Artifacts"))))
            {
                if (thing.stackCount > 1)
                {
                    Thing removedThing = thing.SplitOff(thing.stackCount - 1);
                    if (!removedThing.DestroyedOrNull()) removedThing.Destroy();
                }
            }
        }
    }
}