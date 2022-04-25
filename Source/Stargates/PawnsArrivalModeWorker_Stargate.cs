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
        private List<Thing> GetStargatesOnMap(Map map)
        {
            return map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("StargateMod_Stargate"));
        }

        public override void Arrive(List<Pawn> pawns, IncidentParms parms)
        {
            Map map = (Map)parms.target;
            List<Thing> stargatesOnMap = GetStargatesOnMap(map);

            CompStargate sgComp = stargatesOnMap[0].TryGetComp<CompStargate>();
            sgComp.OpenStargate(-1);
            foreach (Pawn pawn in pawns)
            {
                sgComp.AddToRecieveBuffer(pawn);
            }
        }
        public override bool TryResolveRaidSpawnCenter(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            parms.spawnRotation = Rot4.South;
            List<Thing> stargatesOnMap = GetStargatesOnMap(map);
            CompStargate sgComp = stargatesOnMap[0].TryGetComp<CompStargate>();
            if (stargatesOnMap.Count == 0 || sgComp == null || sgComp.stargateIsActive)
            {
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                return parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms);
            }

            parms.spawnCenter = stargatesOnMap[0].Position;
            return true;
        }
    }
}
