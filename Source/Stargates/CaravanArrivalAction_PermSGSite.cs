using RimWorld;
using RimWorld.Planet;
using Verse;

namespace StargatesMod
{
    public class CaravanArrivalAction_PermSGSite : CaravanArrivalAction
    {
		MapParent arrivalSite;

        public override string Label => "ApproachSite".Translate(arrivalSite.Label);
        public override string ReportString => "ApproachingSite".Translate(arrivalSite.Label);

		public CaravanArrivalAction_PermSGSite(MapParent site)
        {
			arrivalSite = site;
        }

		public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
		{
			if (arrivalSite != null && arrivalSite.Tile != destinationTile) return false; 
			return true;
		}

        public override void Arrived(Caravan caravan)
		{
			Find.LetterStack.ReceiveLetter("LetterLabelCaravanEnteredMap".Translate(arrivalSite), "LetterCaravanEnteredMap".Translate(caravan.Label, arrivalSite).CapitalizeFirst(), LetterDefOf.NeutralEvent, caravan.PawnsListForReading);
			LongEventHandler.QueueLongEvent(() =>
			{
				GetOrGenerateMapUtility.GetOrGenerateMap(arrivalSite.Tile, new IntVec3(75, 1, 75), arrivalSite.def);
				CaravanEnterMapUtility.Enter(caravan, arrivalSite.Map, CaravanEnterMode.Center);
			}, "GeneratingMapForNewEncounter", false, null);
		}

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref this.arrivalSite, "arrivalSite");
		}
	}
}
