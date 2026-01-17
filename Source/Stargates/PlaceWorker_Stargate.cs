using Verse;
using System.Collections.Generic;
using System.Linq;
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
                if (!(props is CompProperties_Stargate sgProps)) continue;
                
                List<IntVec3> vortexPattern = sgProps.vortexPattern.Select(pos => center + pos.RotatedBy(rot)).ToList();
                GenDraw.DrawFieldEdges(vortexPattern, Color.red);
                
                return;
            }
        }
	}
}
