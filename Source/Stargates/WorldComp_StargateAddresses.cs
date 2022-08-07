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
        public List<int> addressList = new List<int>();

        public WorldComp_StargateAddresses(World world) : base(world) { }

        public void RemoveAddress(int address)
        {
            addressList.Remove(address);
        }

        public void AddAddress(int address)
        {
            if (!addressList.Contains(address))
            {
                addressList.Add(address);
            }
        }

        public void CleanupAddresses()
        {
            foreach (int i in new List<int>(this.addressList))
            {
                MapParent sgMap = Find.WorldObjects.MapParentAt(i);
                Site site = sgMap as Site;

                //if the mapparent is null entirely, or it doesn't have a map and isn't either a gate site or a perm gate site, delete the address
                if (sgMap == null || (!sgMap.HasMap && ((site != null && !site.MainSitePartDef.tags.Contains("StargateMod_StargateSite")) || sgMap as WorldObject_PermSGSite == null)))
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
