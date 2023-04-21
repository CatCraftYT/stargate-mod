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
                WorldObject sgObject = Find.WorldObjects.WorldObjectAt<WorldObject>(i);
                Site site = sgObject as Site;

                // if the object doesn't exist entirely, or it's not a gate site and there's no other gate there
                if (sgObject == null || SGUtils.AddressHasGate(i))
                {
                    if (Prefs.LogVerbose) { Log.Message($"StargateMod: cleaned up gate address {i}"); }
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
