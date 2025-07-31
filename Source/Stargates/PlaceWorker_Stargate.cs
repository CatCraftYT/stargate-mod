using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StargatesMod
{
    public class PlaceWorker_Stargate : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            base.DrawGhost(def, center, rot, ghostCol, thing);
            foreach (CompProperties props in def.comps)
            {
                if (!(props is CompStargate.CompProperties_Stargate sgProps)) continue;
                List<IntVec3> pattern = new List<IntVec3>();
                foreach (IntVec3 vec in sgProps.vortexPattern)
                {
                    pattern.Add(center + vec);
                }
                GenDraw.DrawFieldEdges(pattern, Color.red);
                return;
            }
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
            /*Pocket Maps do not have an associated PlanetTile, so no gate address, so stargates cannot work on them*/
            if(map.IsPocketMap) return new AcceptanceReport("PocketMapNo".Translate());

			return true;
		}
	}
}
