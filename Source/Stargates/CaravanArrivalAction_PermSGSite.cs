using RimWorld;
using RimWorld.Planet;
using Verse;

namespace StargatesMod;

public class CaravanArrivalAction_PermSgSite(MapParent site) : CaravanArrivalAction
{
    private MapParent _arrivalSite = site;

    public override string Label => "ApproachSite".Translate(_arrivalSite.Label);
    public override string ReportString => "ApproachingSite".Translate(_arrivalSite.Label);

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile) => _arrivalSite == null || _arrivalSite.Tile == destinationTile;

    public override void Arrived(Caravan caravan)
    {
        Find.LetterStack.ReceiveLetter("LetterLabelCaravanEnteredMap".Translate(_arrivalSite), "LetterCaravanEnteredMap".Translate(caravan.Label, _arrivalSite).CapitalizeFirst(), LetterDefOf.NeutralEvent, caravan.PawnsListForReading);
        LongEventHandler.QueueLongEvent(() =>
        {
            GetOrGenerateMapUtility.GetOrGenerateMap(_arrivalSite.Tile, new IntVec3(75, 1, 75), _arrivalSite.def);
            CaravanEnterMapUtility.Enter(caravan, _arrivalSite.Map, CaravanEnterMode.Center);
        }, "GeneratingMapForNewEncounter", false, null);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref _arrivalSite, "arrivalSite");
    }
}