using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class WorldComp_StargateAddresses : WorldComponent
    {
        public List<PlanetTile> addressList = new List<PlanetTile>();

        public WorldComp_StargateAddresses(World world) : base(world) { }

        public void RemoveAddress(int address)
        {
            addressList.Remove(address);
        }

        public void AddAddress(PlanetTile address)
        {
            if (!addressList.Contains(address))
            {
                addressList.Add(address);
            }
        }

        public void CleanupAddresses()
        {
            foreach (var i in new List<PlanetTile>(this.addressList))
            {
                MapParent sgMap = Find.WorldObjects.MapParentAt(i);
                Site site = sgMap as Site;

                if (sgMap == null || (!sgMap.HasMap && (site == null || !site.MainSitePartDef.tags.Contains("StargateMod_StargateSite")) && sgMap as WorldObject_PermSGSite == null))
                {
                    this.RemoveAddress(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref addressList, "addressList");
        }
    }
}
