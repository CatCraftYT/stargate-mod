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
        AddressList ??= [];
        if (address is {Valid: true} && !AddressList.Contains(address)) AddressList.Add(address);
    }

    public void RemoveAddress(PlanetTile address)
    {
        if (address is {Valid: true}) AddressList.Remove(address);
    }

    public void AddPocketMapAddress(int mapIndex)
    {
        PocketMapAddressList ??= [];
        if (mapIndex >= 0 && !PocketMapAddressList.Contains(mapIndex)) PocketMapAddressList.Add(mapIndex);
    }

    public void RemovePocketMapAddress(int mapIndex)
    {
        if (mapIndex >= 0) PocketMapAddressList.Remove(mapIndex);
    }

    public void CleanupAddresses()
    {
        if (!AddressList.NullOrEmpty())
        {
            foreach (PlanetTile pT in new List<PlanetTile>(AddressList))
            {
                MapParent sgMap = Find.WorldObjects.MapParentAt(pT);
                Site site = sgMap as Site;

                if (sgMap == null || (!sgMap.HasMap && (site == null || !site.MainSitePartDef.tags.Contains("StargateMod_StargateSite")) && sgMap is not WorldObject_PermSgSite))
                    RemoveAddress(pT);
            }
        }

        if (!PocketMapAddressList.NullOrEmpty())
        {
            foreach (int i in new List<int>(PocketMapAddressList))
            {
                Map map = Find.Maps[i];
                PocketMapParent pMParent = map.PocketMapParent;
                if (pMParent is not { HasMap: true }) RemovePocketMapAddress(i);
            }
        }
    }

    public bool EnoughAddressesToDial()
    {
        int addressCount = 0;
        
        if (!AddressList.NullOrEmpty()) addressCount += AddressList.Count;
        if (!PocketMapAddressList.NullOrEmpty()) addressCount += PocketMapAddressList.Count;

        return addressCount >= 2;
    }
        
    public bool IsRegistered(PlanetTile address)
    {
        if (address is not { Valid: true }) return false;
        if (!AddressList.NullOrEmpty() && AddressList.Contains(address)) return true;
        return !PocketMapAddressList.NullOrEmpty() && PocketMapAddressList.Contains(address.tileId);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref AddressList, "AddressList");
        Scribe_Collections.Look(ref PocketMapAddressList, "PocketMapAddressList");
    }
}