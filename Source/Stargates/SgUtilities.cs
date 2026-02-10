using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace StargatesMod;

public enum DialMode
{
    Map,
    PocketMap,
    IncomingRaid,
    None
}

public readonly struct BufferItem(Thing thing, bool drafted = false, Pawn carriedPawn = null) : IEquatable<BufferItem>
{
    public readonly Thing Thing = thing;
    public readonly bool Drafted = drafted;
    public readonly Pawn CarriedPawn = carriedPawn;

    public Pawn Pawn
    {
        get
        {
            if (Thing is Pawn pawn) return pawn;
            return null;
        }
    }
    
    public bool Equals(BufferItem other) => Equals(Thing, other.Thing) && Drafted == other.Drafted && Equals(CarriedPawn, other.CarriedPawn);

    public override bool Equals(object obj) => obj is BufferItem other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Thing, Drafted, CarriedPawn);
}

public static class SgUtilities
{
    public static Thing GetActiveStargateOnMap(Map map, Thing thingToIgnore = null) => map.listerBuildings.allBuildingsColonist.Where(t => t.def.thingClass == typeof(Building_Stargate) && !t.TryGetComp<CompStargate>().IsHibernating && t != thingToIgnore).FirstOrFallback() ??
                                                                                       map.listerBuildings.allBuildingsNonColonist.Where(t => t.def.thingClass == typeof(Building_Stargate) && !t.TryGetComp<CompStargate>().IsHibernating && t != thingToIgnore).FirstOrFallback();

    public static List<Thing> GetAllStargatesOnMap(Map map, List<Thing> thingsToIgnore = null, bool excludeHibernating = true, bool includeLinkedMaps = false)
    {
        List<Thing> gates = [];
            
        gates.AddRange(map.listerBuildings.allBuildingsColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)));
        gates.AddRange(map.listerBuildings.allBuildingsNonColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)));
            
        if (excludeHibernating) gates.RemoveWhere(t => t.TryGetComp<CompStargate>().IsHibernating);
        if (thingsToIgnore != null) gates.RemoveWhere(thingsToIgnore.Contains);

        if (!includeLinkedMaps) return gates;
            
        if (map.IsPocketMap && map.PocketMapParent.sourceMap != null) gates.AddRange(GetAllStargatesOnMap(map.PocketMapParent.sourceMap, excludeHibernating: false));
        
        if (!map.ChildPocketMaps.Any()) return gates;
        
        foreach (Map childMap in map.ChildPocketMaps)
        {
            gates.AddRange(GetAllStargatesOnMap(childMap, excludeHibernating: false));
        }

        return gates;
    }

    public static Thing GetDHDOnMap(Map map) => map.listerBuildings.allBuildingsColonist.Where(t => t.TryGetComp<CompDialHomeDevice>() != null  && t.def.thingClass != typeof(Building_Stargate)).FirstOrFallback() ??
                                                map.listerBuildings.allBuildingsNonColonist.Where(t => t.TryGetComp<CompDialHomeDevice>() != null  && t.def.thingClass != typeof(Building_Stargate)).FirstOrFallback();
    
    public static string GetStargateDesignation(PlanetTile address)
    {
        const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        
        if (address.tileId < 0) return "UnknownLower".Translate();
            
        Rand.PushState(address.tileId);
        //pattern: (prefix)(num)(char)-(num)(num)(num)
        string prefix = address.Layer.Def.isSpace ? "O" : "P"; //Planet layer designation: O for orbit / space, P for planetary / other
        string designation = $"{prefix}{Rand.RangeInclusive(0, 9)}{alpha[Rand.RangeInclusive(0, 25)]}-{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}"; 
        Rand.PopState();
            
        return designation;
    }
}