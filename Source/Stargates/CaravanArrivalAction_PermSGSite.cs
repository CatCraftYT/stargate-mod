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
		WorldObject arrivalSite;
		int plannedAddress;

        public override string Label => "EnterStargateAction".Translate();
        public override string ReportString => "ApproachingSite".Translate(arrivalSite.Label);

		public CaravanArrivalAction_PermSGSite(WorldObject site, int plannedAddress)
        {
			arrivalSite = site;
			this.plannedAddress = plannedAddress;
        }

		public override FloatMenuAcceptanceReport StillValid(Caravan caravan, int destinationTile)
		{
			if (this.arrivalSite != null && this.arrivalSite.Tile != destinationTile) { return false; }
			return true;
		}

        public override void Arrived(Caravan caravan)
		{
			Find.LetterStack.ReceiveLetter("LetterLabelCaravanEnteredMap".Translate(arrivalSite), "LetterCaravanEnteredMap".Translate(caravan.Label, arrivalSite).CapitalizeFirst(), LetterDefOf.NeutralEvent, caravan.PawnsListForReading);
			IStargate gate = arrivalSite as WorldObject_PermSGSite;
			gate.OpenStargate(plannedAddress, false);
			foreach (Thing thing in caravan.pawns) {
				gate.AddToSendBuffer(thing);
			}
			if (!caravan.Destroyed) { caravan.Destroy(); }
		}

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref this.arrivalSite, "arrivalSite");
		}
	}
}
