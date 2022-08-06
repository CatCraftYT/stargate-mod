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
            if (sgThing == null) { return; }
            CompStargate sgComp = sgThing.TryGetComp<CompStargate>();
            if (sgComp == null) { return; }

            if (sgComp.stargateIsActive) { __result = true; return; }
        }
    }

    [HarmonyPatch(typeof(Caravan))]
    [HarmonyPatch(nameof(Caravan.GetGizmos))]
    class AddCreateSGSiteToCaravanGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Caravan __instance)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }

            bool containsStargate = false;
            bool containsDHD = false;
            foreach (Thing thing in __instance.AllThings)
            {
                Thing inner = thing.GetInnerIfMinified();
                if (inner != null)
                {
                    if (inner.TryGetComp<CompStargate>() != null) { containsStargate = true; }
                    if (inner.TryGetComp<CompDialHomeDevice>() != null) { containsDHD = true; }
                }
                if (containsStargate && containsDHD) { break; }
            }
            Command_Action command = new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/sgsite_perm", true),
                action = () =>
                {
                    List<Thing> things = __instance.AllThings.ToList();
                    for (int i = 0; i < things.Count(); i++)
                    {
                        Thing inner = things[i].GetInnerIfMinified();
                        if (inner != null && inner.TryGetComp<CompStargate>() != null) { things[i].holdingOwner.Remove(things[i]); break; }
                    }
                    for (int i = 0; i < things.Count(); i++)
                    {
                        Thing inner = things[i].GetInnerIfMinified();
                        if (inner != null && inner.TryGetComp<CompDialHomeDevice>() != null) { things[i].holdingOwner.Remove(things[i]); break; }
                    }
                    WorldObject wo = WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("StargateMod_SGSitePerm"));
                    wo.Tile = __instance.Tile;
                    MapParent mapParent = (MapParent)wo;
                    Find.WorldObjects.Add(mapParent);
                },
                defaultLabel = "Create stargate site",
                defaultDesc = "Create a stargate site at this location."
            };
            StringBuilder reason = new StringBuilder();
            if (!(containsStargate && containsDHD)) { command.Disable("Dial home device and stargate required in caravan inventory."); }
            else if (!TileFinder.IsValidTileForNewSettlement(__instance.Tile, reason)) { command.Disable(reason.ToString()); }
            yield return command;
        }
    }
}
