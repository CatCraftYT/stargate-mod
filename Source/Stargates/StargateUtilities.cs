using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StargatesMod
{
    public interface IStargate
    {
        bool IsActive { get; set; }
        void AddToRecieveBuffer(Thing thing);
        void AddToSendBuffer(Thing thing);
        void OpenStargate(int address, bool isRecieving);
        void CloseStargate();
        bool TryCloseConnection();
        bool TryRecieveConnection(int address);
    }

    public static class SGUtils
    {
        const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static Thing GetStargateOnMap(Map map, Thing thingToIgnore = null)
        {
            Thing gateOnMap = null;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing != thingToIgnore && thing.def.thingClass == typeof(Building_Stargate))
                {
                    gateOnMap = thing;
                    break;
                }
            }
            return gateOnMap;
        }

        public static Thing GetDHDOnMap(Map map)
        {
            Thing dhdOnMap = null;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing.TryGetComp<CompDialHomeDevice>() != null && thing.def.thingClass != typeof(Building_Stargate))
                {
                    dhdOnMap = thing;
                    break;
                }
            }
            return dhdOnMap;
        }

        public static string GetStargateDesignation(int address)
        {
            if (address < 0) { return "UnknownLower".Translate(); }
            Rand.PushState(address);
            //pattern: P(num)(char)-(num)(num)(num)
            string designation = $"P{Rand.RangeInclusive(0, 9)}{alpha[Rand.RangeInclusive(0, 25)]}-{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}";
            Rand.PopState();
            return designation;
        }
    }
}
