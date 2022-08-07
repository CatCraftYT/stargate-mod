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
        public ThingDef gateDef;
        public ThingDef dhdDef;

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
            return "GateAddress".Translate(CompStargate.GetStargateDesignation(this.Tile));
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
            Thing gateOnMap = CompStargate.GetStargateOnMap(this.Map);
            Thing dhdOnMap = CompDialHomeDevice.GetDHDOnMap(this.Map);
            if (gateOnMap != null)
            {
                IntVec3 gatePos = gateOnMap.Position;
                gateOnMap.Destroy();
                if (gateDef != null) { GenSpawn.Spawn(gateDef, gatePos, this.Map); }
            }
            if (dhdOnMap != null)
            {
                IntVec3 dhdPos = dhdOnMap.Position;
                dhdOnMap.Destroy();
                if (dhdDef != null) { GenSpawn.Spawn(dhdDef, dhdPos, this.Map); }
            }
        }

        public override void Notify_MyMapAboutToBeRemoved()
        {
            Thing gateOnMap = CompStargate.GetStargateOnMap(this.Map);
            Thing dhdOnMap = CompDialHomeDevice.GetDHDOnMap(this.Map);
            dhdDef = dhdOnMap == null ? null : dhdOnMap.def;
            gateDef = gateOnMap == null ? null : gateOnMap.def;
        }

        public override void Notify_MyMapRemoved(Map map)
        {
            base.Notify_MyMapRemoved(map);
            if (gateDef == null && dhdDef == null) { this.Destroy(); }
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
                defaultLabel = "RenameGateSite".Translate(),
                defaultDesc = "RenameGateSiteDesc".Translate()
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            return CaravanArrivalActionUtility.GetFloatMenuOptions(() => { return true; }, () => { return new CaravanArrivalAction_PermSGSite(this); }, $"Approach {this.Label}", caravan, this.Tile, this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.siteName, "siteName");
            Scribe_Defs.Look(ref this.dhdDef, "dhdDef");
            Scribe_Defs.Look(ref this.gateDef, "gateDef");
        }
    }
}
