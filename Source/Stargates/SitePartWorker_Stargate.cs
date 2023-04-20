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
        const int maxMarketValue = 2000;

		public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("GateAddress".Translate(SGUtils.GetStargateDesignation(site.Tile)));
			return sb.ToString();
		}

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null) { Log.Error("SitePartWorker map was null on PostMapGenerate. That makes no sense."); return; }
            Thing gateOnMap = SGUtils.GetStargateOnMap(map);
            var VortexCells = gateOnMap.TryGetComp<CompStargate>().VortexCells;

            //move pawns away from vortex
            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                Room pawnRoom = GridsUtility.GetRoom(pawn.Position, pawn.Map);
                var cells = GenRadial.RadialCellsAround(pawn.Position, 9, true).Where(c => c.InBounds(map) && c.Walkable(map) && GridsUtility.GetRoom(c, map) == pawnRoom && !VortexCells.Contains(c));
                if (!cells.Any()) { continue; }
                pawn.Position = cells.RandomElement();
                pawn.pather.StopDead();
                pawn.jobs.StopAll();
            }

            //rebalance items (this may cause performance issues)
            foreach (Thing thing in map.listerThings.AllThings.Where(t => t.HasThingCategory(ThingCategoryDefOf.Items)))
            {
                if (thing.MarketValue * thing.stackCount > maxMarketValue)
                {
                    int stackCount = Rand.Range(0, (int)Math.Ceiling(maxMarketValue / thing.MarketValue));
                }
            }
        }
    }
}
