using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using RimWorld.Planet;
using Verse;

namespace StargatesMod;

[StaticConstructorOnStartup]
public class HarmonyPatches
{
    static HarmonyPatches()
    {
        Harmony harmony = new Harmony("CCYT.StargatesMod");
        harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(MapPawns))]
[HarmonyPatch("AnyPawnBlockingMapRemoval", MethodType.Getter)]
class KeepMapWithStargateOpen
{
    static void Postfix(Map ___map, ref bool __result)
    {
        Thing sgThing = SgUtilities.GetActiveStargateOnMap(___map);
        CompStargate sgComp = sgThing?.TryGetComp<CompStargate>();
            
        if (sgComp == null) return;

        if (sgComp.StargateIsActive) __result = true;
    }
}

[HarmonyPatch(typeof(Caravan))]
[HarmonyPatch(nameof(Caravan.GetGizmos))]
class AddCreateSgSiteToCaravanGizmos
{
    static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Caravan __instance)
    {
        foreach (Gizmo gizmo in gizmos) yield return gizmo;

        bool containsStargate = __instance.AllThings.Select(thing => thing.GetInnerIfMinified()).Where(inner => inner != null).Any(inner => inner.TryGetComp<CompStargate>() != null);
            
        Command_Action commandCreateSite = new()
        {
            icon = ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/sgsite_perm"),
            action = () =>
            {
                ThingDef gateDef = null;
                ThingDef dhdDef = null;

                List<Thing> things = __instance.AllThings.ToList();
                foreach (Thing thing in things)
                {
                    Thing inner = thing.GetInnerIfMinified();
                    if (inner == null || inner.def.thingClass != typeof(Building_Stargate)) continue;
                        
                    gateDef = inner.def; 
                    thing.holdingOwner.Remove(thing); 
                    break;
                }
                    
                if (gateDef != null && !gateDef.HasComp<CompDialHomeDevice>())
                {
                    things = __instance.AllThings.ToList();
                    foreach (Thing thing in things)
                    {
                        Thing inner = thing.GetInnerIfMinified();
                        if (inner?.TryGetComp<CompDialHomeDevice>() == null || inner.def.thingClass == typeof(Building_Stargate)) continue;
                            
                        dhdDef = inner.def; 
                        thing.holdingOwner.Remove(thing); 
                        break;
                    }
                }
                WorldObject_PermSgSite wo = (WorldObject_PermSgSite)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("StargateMod_SGSitePerm"));
                wo.Tile = __instance.Tile;
                wo.GateDef = gateDef;
                wo.DhdDef = dhdDef;
                Find.WorldObjects.Add(wo);
            },
            defaultLabel = "SGM.CreateSGSite".Translate(),
            defaultDesc = "SGM.CreateSGSiteDesc".Translate()
        };
        StringBuilder reason = new();
        if (!containsStargate) commandCreateSite.Disable("SGM.NoGateInCaravan".Translate());
        else if (__instance.Tile.Tile.Landmark != null) commandCreateSite.Disable("SGM.BlockedByLandmark".Translate());
        else if (!TileFinder.IsValidTileForNewSettlement(__instance.Tile, reason)) commandCreateSite.Disable(reason.ToString());
        yield return commandCreateSite;
    }
}

//A patch to stop the 'carry pawn to transporter' floatmenu option from being applied to stargates, as it doesn't work properly due to how CompTransporter is used in this case.
[HarmonyPatch(typeof(FloatMenuOptionProvider_CarryingPawn), "CarryToTransporter")]
public static class FloatMenuOptionProvider_CarryingPawn_Patch
{
    static bool Prefix(ref bool __result, Thing clickedThing, FloatMenuContext context, Pawn carriedPawn)
    {
        if (clickedThing.TryGetComp<CompStargate>() == null) return true;
        __result = false;
        return false;

    }
}