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
    public class WorldObject_PermSGSite : MapParent, IRenameable
    {
        public string SiteName;
        public ThingDef GateDef = ThingDef.Named("StargateMod_Stargate");
        public ThingDef DhdDef = ThingDef.Named("StargateMod_DialHomeDevice");

        public override string Label => SiteName ?? base.Label;

        public string RenamableLabel {
            get => Label;
            set => SiteName = value;
        }
        public string BaseLabel => Label;
        public string InspectLabel => Label;

        public override string GetInspectString()
        {
            return "GateAddress".Translate(CompStargate.GetStargateDesignation(Tile));
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            Find.World.GetComponent<WorldComp_StargateAddresses>().AddAddress(Tile);
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
            foreach (var pawn in Map.mapPawns.AllPawns.Where(p => p.RaceProps.Humanlike || p.HostileTo(Faction)).ToList()) 
                pawn.Destroy();

            Thing gateOnMap = CompStargate.GetStargateOnMap(Map);
            Thing dhdOnMap = CompDialHomeDevice.GetDHDOnMap(Map);
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: perm sg site post map gen: dhddef={DhdDef} gatedef={GateDef} gateonmap={gateOnMap} dhdonmap={dhdOnMap}");
            
            if (gateOnMap != null)
            {
                IntVec3 gatePos = gateOnMap.Position;
                gateOnMap.Destroy();
                if (GateDef != null)
                {
                    Thing spGate = GenSpawn.Spawn(GateDef, gatePos, Map);
                    spGate.SetFaction(Faction.OfPlayer);
                }
                
            }
            if (dhdOnMap != null)
            {
                IntVec3 dhdPos = dhdOnMap.Position;
                dhdOnMap.Destroy();
                if (DhdDef != null)
                {
                    Thing spDhd = GenSpawn.Spawn(DhdDef, dhdPos, Map);
                    spDhd.SetFaction(Faction.OfPlayer);
                }
            }
        }

        public override void Notify_MyMapAboutToBeRemoved()
        {
            Thing gateOnMap = CompStargate.GetStargateOnMap(Map);
            Thing dhdOnMap = CompDialHomeDevice.GetDHDOnMap(Map);
            DhdDef = dhdOnMap?.def;
            GateDef = gateOnMap?.def;
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: perm map about to be removed: dhddef={DhdDef} gatedef={GateDef}");
        }

        public override void Notify_MyMapRemoved(Map map)
        {
            base.Notify_MyMapRemoved(map);
            if (GateDef == null && DhdDef == null) Destroy();
        }

        public override void Destroy()
        {
            base.Destroy();
            Find.World.GetComponent<WorldComp_StargateAddresses>().RemoveAddress(Tile);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;

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
            return CaravanArrivalActionUtility.GetFloatMenuOptions(() => true, () => new CaravanArrivalAction_PermSGSite(this), $"Approach {Label}", caravan, Tile, this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref SiteName, "SiteName");
            Scribe_Defs.Look(ref DhdDef, "DhdDef");
            Scribe_Defs.Look(ref GateDef, "GateDef");
        }
    }
}
