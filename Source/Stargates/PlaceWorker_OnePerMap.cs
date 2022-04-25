using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StargatesMod
{
    public class PlaceWorker_OnePerMap : PlaceWorker
    {
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			if (CompStargate.GetStargateOnMap(map) != null)
            {
				return new AcceptanceReport("Only one of this kind of building can be built per map.");
			}
			return true;
		}
	}
}
