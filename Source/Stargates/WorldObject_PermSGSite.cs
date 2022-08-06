using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace StargatesMod
{
    public class WorldObject_PermSGSite : MapParent
    {
        public string siteName;

        public override string Label
        {
            get
            {
                if (siteName == null) { return base.Label; }
                else { return siteName; }
            }
        }

        public override string GetInspectString()
        {
            return $"Stargate address: {CompStargate.GetStargateDesignation(this.Tile)}";
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            Find.World.GetComponent<WorldComp_StargateAddresses>().AddAddress(this.Tile);
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = false;
            return Map.mapPawns.AnyPawnBlockingMapRemoval;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/Rename", true),
                action = () => { Find.WindowStack.Add(new Dialog_RenameSGSite(this)); },
                defaultLabel = "Rename site",
                defaultDesc = "Rename this stargate site to a more recognizable name."
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            return CaravanArrivalActionUtility.GetFloatMenuOptions(() => { return true; }, () => { return new CaravanArrivalAction_PermSGSite(this); }, $"Approach {this.Label}", caravan, this.Tile, this);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref siteName, "siteName");
        }
    }
}
