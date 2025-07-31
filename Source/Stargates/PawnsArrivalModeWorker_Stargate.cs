using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class PawnsArrivalModeWorker_Stargate : PawnsArrivalModeWorker
    {
        public override void Arrive(List<Pawn> pawns, IncidentParms parms)
        {
            Map map = (Map)parms.target;
            Thing stargateOnMap = CompStargate.GetStargateOnMap(map);

            CompStargate sgComp = stargateOnMap.TryGetComp<CompStargate>();
            sgComp.OpenStargateDelayed(-1, 450);
            sgComp.TicksSinceBufferUnloaded = -150;
            sgComp.IsReceivingGate = true;
            sgComp.TicksSinceBufferUnloaded = -150;
            sgComp.IsReceivingGate = true;
            foreach (Pawn pawn in pawns)
            {
                sgComp.AddToReceiveBuffer(pawn);
            }
        }
        public override bool TryResolveRaidSpawnCenter(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            parms.spawnRotation = Rot4.South;
            Thing stargateOnMap = CompStargate.GetStargateOnMap(map);
            CompStargate sgComp = stargateOnMap?.TryGetComp<CompStargate>();
            if (stargateOnMap == null || sgComp == null || sgComp.StargateIsActive)
            {
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                return parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
            }

            parms.spawnCenter = stargateOnMap.Position;
            return true;
        }
    }
}
