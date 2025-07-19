using RimWorld.Planet;
using Verse;
using System.Collections.Generic;

namespace StargatesMod
{
    public class WorldComp_StargateAddresses : WorldComponent
    {
        public List<PlanetTile> AddressList = new List<PlanetTile>();

        public WorldComp_StargateAddresses(World world) : base(world) { }

        public void RemoveAddress(PlanetTile address)
        {
            AddressList.Remove(address);
        }

        public void AddAddress(PlanetTile address)
        {
            if (AddressList.Contains(address)) return;
            AddressList.Add(address);
        }

        public void CleanupAddresses()
        {
            foreach (PlanetTile pT in new List<PlanetTile>(AddressList))
            {
                MapParent sgMap = Find.WorldObjects.MapParentAt(pT);
                Site site = sgMap as Site;

                if (sgMap == null || (!sgMap.HasMap && (site == null || !site.MainSitePartDef.tags.Contains("StargateMod_StargateSite")) && sgMap as WorldObject_PermSGSite == null))
                {
                    this.RemoveAddress(i);
                }
                    RemoveAddress(pT);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref AddressList, "AddressList");
        }
    }
}