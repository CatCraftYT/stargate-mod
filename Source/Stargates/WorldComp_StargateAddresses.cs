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
            foreach (PlanetTile pT in new List<PlanetTile>(AddressList))
            {
                MapParent sgMap = Find.WorldObjects.MapParentAt(pT);
                Site site = sgMap as Site;

                if (sgMap == null || (!sgMap.HasMap && (site == null || !site.MainSitePartDef.tags.Contains("StargateMod_StargateSite")) && sgMap is not WorldObject_PermSgSite))
                    RemoveAddress(pT);
            }

            foreach (int i in new List<int>(PocketMapAddressList))
            {
                Map map = Find.Maps[i];
                PocketMapParent pMParent = map.PocketMapParent;
                if (pMParent is not { HasMap: true }) RemovePocketMapAddress(i);
            }
        }

        public bool EnoughAddressesToDial() => AddressList.Count + PocketMapAddressList.Count >= 2;
        
    public bool IsRegistered(PlanetTile address) => address != PlanetTile.Invalid && (AddressList.Contains(address) || PocketMapAddressList.Contains(address.tileId));

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref AddressList, "AddressList");
        Scribe_Collections.Look(ref PocketMapAddressList, "PocketMapAddressList");
    }
}