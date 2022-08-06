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
            return !Map.mapPawns.AnyPawnBlockingMapRemoval;
        }

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            //from https://github.com/AndroidQuazar/VanillaExpandedFramework/blob/4331195034c15a18930b85c5f5671ff890e6776a/Source/Outposts/Outpost/Outpost_Attacks.cs. I like your bodgy style, VE devs
            foreach (var pawn in Map.mapPawns.AllPawns.Where(p => p.RaceProps.Humanlike || p.HostileTo(Faction)).ToList()) { pawn.Destroy(); }
        }

        public override void Destroy()
        {
            base.Destroy();
            Find.World.GetComponent<WorldComp_StargateAddresses>().RemoveAddress(this.Tile);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/Rename"),
                action = () => { Find.WindowStack.Add(new Dialog_RenameSGSite(this)); },
                defaultLabel = "Rename site",
                defaultDesc = "Rename this stargate site to a more recognizable name."
            };
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach (Gizmo gizmo in base.GetCaravanGizmos(caravan))
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/Abandon"),
                action = () =>
                {
                    //TODO: save exact gate def on site creation and then here create thing from def and put it in caravan inventory
                    this.Destroy();
                },
                defaultLabel = "Remove site",
                defaultDesc = "Remove this stargate site and take its stargate."
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
