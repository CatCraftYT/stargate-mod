using RimWorld.Planet;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod;

public class WorldComp_StargateAddresses(World world) : WorldComponent(world)
{
    public List<PlanetTile> AddressList = [];

    public List<int> PocketMapAddressList = [];

    public void AddAddress(PlanetTile address)
    {
        if (!AddressList.Contains(address)) AddressList.Add(address);
    }
    public void RemoveAddress(PlanetTile address) => AddressList.Remove(address);

    public void AddPocketMapAddress(int mapIndex)
    {
        if (!PocketMapAddressList.Contains(mapIndex)) PocketMapAddressList.Add(mapIndex);
    }
    public void RemovePocketMapAddress(int mapIndex) => PocketMapAddressList.Remove(mapIndex);

    public void CleanupAddresses()
    {
        foreach (PlanetTile tile in from tile in new List<PlanetTile>(AddressList) let sgMap = Find.WorldObjects.MapParentAt(tile) let site = sgMap as Site
                 where sgMap == null || (!sgMap.HasMap && (site == null || !site.MainSitePartDef.tags.Contains("StargateMod_StargateSite")) && sgMap is not WorldObject_PermSgSite) select tile)
        {
            RemoveAddress(tile);
        }

        foreach (int address in from address in new List<int>(PocketMapAddressList) let map = Find.Maps[address] 
                 let pMParent = map.PocketMapParent where pMParent is not { HasMap: true } select address)
        {
            RemovePocketMapAddress(address);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref AddressList, "AddressList");
    }
}