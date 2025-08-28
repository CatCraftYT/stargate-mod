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
            CompStargate gateComp = gateOnMap.TryGetComp<CompStargate>();
            
            var vortexCells = gateComp.VortexCells;
            var gateCells = GenRadial.RadialCellsAround(gateOnMap.InteractionCell, 11, true).ToList();

            //move pawns away from vortex
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: Moving pawns away from stargate vortex..");
            try
            {
                foreach (Pawn pawn in map.mapPawns.AllPawns)
                {
                    Room pawnRoom = pawn.Position.GetRoom(pawn.Map);
                    if (!gateCells.Contains(pawn.Position)) continue;

                    var cells = GenRadial.RadialCellsAround(pawn.Position, 9, true).Where(c =>
                        c.InBounds(map) && c.Walkable(map) && c.GetRoom(map) == pawnRoom && !vortexCells.Contains(c));
                    var cellsList = cells.ToList();
                    if (!cellsList.Any()) continue;

                    if (Prefs.LogVerbose) Log.Message($"StargatesMod: Pawn {pawn} position is {pawn.Position}");
                    pawn.Position = cellsList.RandomElement();
                    pawn.pather.StopDead();
                    pawn.jobs.StopAll();
                    if (Prefs.LogVerbose)
                        Log.Message($"StargatesMod: Moved {pawn} away from stargate vortex to {pawn.Position}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"StargatesMod: Caught error in SitePartWorker_Stargate.PostMapGenerate 'Move pawns away from vortex' - {e}");
            }

            try
            {
                //Fix stackCounts of certain items (especially things certain cases with stack increasing mods)
                // also things like res mech serums even without that
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
            catch (Exception  e)
            {
                Log.Error($"StargatesMod: Caught error in SitePartWorker_Stargate.PostMapGenerate 'Fix too high item stackCounts' - {e}");
            }
        }
    }
}