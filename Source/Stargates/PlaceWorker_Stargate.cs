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
                CompProperties_Stargate sgProps = props as CompProperties_Stargate;
                if (sgProps != null)
                {
                    List<IntVec3> pattern = new List<IntVec3>();
                    foreach (IntVec3 vec in sgProps.vortexPattern)
                    {
                        pattern.Add(center + vec);
                    }
                    GenDraw.DrawFieldEdges(pattern, Color.red);
                    return;
                }
            }
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			if (SGUtils.GetStargateOnMap(map, thing) != null)
            {
				return new AcceptanceReport("OnlyOneSGPerMap".Translate());
			}
			return true;
		}
	}
}
