using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using RimWorld.Planet;
using Verse;

namespace StargatesMod
{
    [StaticConstructorOnStartup]
    public class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("CCYT.StargatesMod");
            //patch all of the decorators in this assembly
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(MapPawns))]
    [HarmonyPatch("AnyPawnBlockingMapRemoval", MethodType.Getter)]
    class KeepMapWithStargateOpen
    {
        static void Postfix(Map ___map, ref bool __result)
        {
            Thing sgThing = CompStargate.GetStargateOnMap(___map);
            if (sgThing == null) return;
            CompStargate sgComp = sgThing.TryGetComp<CompStargate>();
            if (sgComp == null) return;

            if (sgComp.StargateIsActive) __result = true;
        }
    }

    [HarmonyPatch(typeof(Caravan))]
    [HarmonyPatch(nameof(Caravan.GetGizmos))]
    class AddCreateSGSiteToCaravanGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Caravan __instance)
        {
            foreach (Gizmo gizmo in gizmos) yield return gizmo;

            bool containsStargate = false;
            foreach (Thing thing in __instance.AllThings)
            {
                Thing inner = thing.GetInnerIfMinified();
                if (inner != null)
                {
                    if (inner.TryGetComp<CompStargate>() != null) containsStargate = true; break;
                }
            }
            Command_Action command = new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/sgsite_perm"),
                action = () =>
                {
                    ThingDef gateDef = null;
                    ThingDef dhdDef = null;

                    List<Thing> things = __instance.AllThings.ToList();
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing inner = things[i].GetInnerIfMinified();
                        if (inner != null && inner.def.thingClass == typeof(Building_Stargate)) { gateDef = inner.def; things[i].holdingOwner.Remove(things[i]); break; }
                    }
                    things = __instance.AllThings.ToList();
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing inner = things[i].GetInnerIfMinified();
                        if (inner?.TryGetComp<CompDialHomeDevice>() != null && inner.def.thingClass != typeof(Building_Stargate)) { dhdDef = inner.def; things[i].holdingOwner.Remove(things[i]); break; }
                    }
                    WorldObject_PermSGSite wo = (WorldObject_PermSGSite)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("StargateMod_SGSitePerm"));
                    wo.Tile = __instance.Tile;
                    wo.GateDef = gateDef;
                    wo.DhdDef = dhdDef;
                    Find.WorldObjects.Add(wo);
                },
                defaultLabel = "CreateSGSite".Translate(),
                defaultDesc = "CreateSGSiteDesc".Translate()
            };
            StringBuilder reason = new StringBuilder();
            if (!containsStargate) command.Disable("NoGateInCaravan".Translate());
            else if (!TileFinder.IsValidTileForNewSettlement(__instance.Tile, reason)) command.Disable(reason.ToString());
            yield return command;
        }
    }
}
