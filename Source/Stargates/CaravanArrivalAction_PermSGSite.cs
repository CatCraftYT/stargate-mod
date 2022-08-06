using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace StargatesMod
{
    public class CaravanArrivalAction_PermSGSite : CaravanArrivalAction
    {
		MapParent arrivalSite;

        public override string Label => "Approach new stargate site";
        public override string ReportString => "Approaching new stargate site";

		public CaravanArrivalAction_PermSGSite(MapParent site)
        {
			arrivalSite = site;
        }

		public override FloatMenuAcceptanceReport StillValid(Caravan caravan, int destinationTile)
		{
			if (this.arrivalSite != null && this.arrivalSite.Tile != destinationTile) { return false; }
			return true;
		}

        public override void Arrived(Caravan caravan)
		{
			LongEventHandler.QueueLongEvent(() =>
			{
				Map map = null;
				map = GetOrGenerateMapUtility.GetOrGenerateMap(arrivalSite.Tile, new IntVec3(75, 1, 75), arrivalSite.def);
				CaravanEnterMapUtility.Enter(caravan, arrivalSite.Map, CaravanEnterMode.Edge);
			}, "GeneratingMapForNewEncounter", false, null);
		}

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref this.arrivalSite, "arrivalSite");
		}
	}
}
