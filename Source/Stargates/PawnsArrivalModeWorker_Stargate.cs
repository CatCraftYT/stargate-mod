using RimWorld;
using System.Collections.Generic;
using Verse;

namespace StargatesMod;

public class PawnsArrivalModeWorker_Stargate : PawnsArrivalModeWorker
{
    private readonly StargatesModSettings _modSettings = LoadedModManager.GetMod<StargatesMod>().GetSettings<StargatesModSettings>();
        
    public override void Arrive(List<Pawn> pawns, IncidentParms parms)
    {
        Map map = (Map)parms.target;
        Thing stargateOnMap = SgUtilities.GetAllStargatesOnMap(map, includeLinkedMaps: true).FirstOrFallback();

        CompStargate sgComp = stargateOnMap.TryGetComp<CompStargate>();
            
        int lockDelay = 900;
        if (_modSettings.ShortenGateDialSeq) lockDelay = 450;

        sgComp.OpenStargateDelayed(-1, lockDelay, DialMode.IncomingRaid);
            
        foreach (Pawn pawn in pawns)
        {
            sgComp.AddToReceiveBuffer(new BufferItem(pawn));
        }
    }
    public override bool TryResolveRaidSpawnCenter(IncidentParms parms)
    {
        Map map = (Map)parms.target;
            
        Thing stargateOnMap = SgUtilities.GetAllStargatesOnMap(map, includeLinkedMaps: true).FirstOrFallback();
        CompStargate sgComp = stargateOnMap?.TryGetComp<CompStargate>();
            
        if (stargateOnMap == null || sgComp == null || sgComp.StargateIsActive)
        {
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            return parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
        }

        parms.spawnRotation = stargateOnMap.Rotation;
        parms.spawnCenter = stargateOnMap.Position;
        return true;
    }
}