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

        public static bool AddressHasGate(int address)
        {
            WorldObject worldObject = Find.WorldObjects.WorldObjectAt<WorldObject>(address);
            MapParent connectedMap = worldObject as MapParent;
            if (connectedMap == null)
            {
                IStargate worldStargate = worldObject as WorldObject_PermSGSite;
                Site site = worldObject as Site;
                return worldStargate != null || (site != null && site.MainSitePartDef.tags.Contains("StargateMod_StargateSite"));
            }
            else
            {
                return GetStargateOnMap(connectedMap.Map) != null;
            }
        }

        public static IStargate GetStargate(int address)
        {
            if (address < 0) { return null; }
            WorldObject worldObject = Find.WorldObjects.WorldObjectAt<WorldObject>(address);
            MapParent connectedMap = worldObject as MapParent;

            // is permanent world gate site
            if (connectedMap == null)
            {
                IStargate worldStargate = worldObject as WorldObject_PermSGSite;
                Site site = worldObject as Site;
                if (worldStargate != null)
                {
                    return worldStargate;
                }
                else if (site != null && site.MainSitePartDef.tags.Contains("StargateMod_StargateSite"))
                {
                    if (Prefs.LogVerbose) { Log.Message($"StargateMod: generating map for {connectedMap}"); }
                    Map map = GetOrGenerateMapUtility.GetOrGenerateMap(connectedMap.Tile, Find.World.info.initialMapSize, null);
                    if (Prefs.LogVerbose) { Log.Message($"StargateMod: finished generating map"); }
                    return SGUtils.GetStargateOnMap(map).TryGetComp<CompStargate>() as IStargate;
                }
                else
                {
                    Log.Error("Tried to get a stargate at {address}, and it has no map parent, but the WorldObject has no stargate interface.");
                    return null;
                }
                
            }

            // is gate on map
            else
            {
                Map map = connectedMap.Map;
                Thing gate = GetStargateOnMap(connectedMap.Map);
                if (gate == null)
                {
                    Log.Error($"Tried to get stargate at {address}, and it had a map parent, but it had no map.");
                    return null;
                }

                return gate.TryGetComp<CompStargate>() as IStargate;
            }
        }

        public static Thing GetStargateOnMap(Map map, Thing thingToIgnore = null)
        {
            if (map == null) { return null; }

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
