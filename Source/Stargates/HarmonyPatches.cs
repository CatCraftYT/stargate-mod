using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using RimWorld.Planet;
using Verse;
using Verse.AI;

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
            Thing sgThing = CompStargate.GetActiveStargateOnMap(___map);
            CompStargate sgComp = sgThing?.TryGetComp<CompStargate>();
            
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

            bool containsStargate = __instance.AllThings.Select(thing => thing.GetInnerIfMinified()).Where(inner => inner != null).Any(inner => inner.TryGetComp<CompStargate>() != null);
            
            Command_Action commandCreateSite = new()
            {
                icon = ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/sgsite_perm"),
                action = () =>
                {
                    ThingDef gateDef = null;
                    ThingDef dhdDef = null;

                    List<Thing> things = __instance.AllThings.ToList();
                    foreach (var thing in things)
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
                    WorldObject_PermSGSite wo = (WorldObject_PermSGSite)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("StargateMod_SGSitePerm"));
                    wo.Tile = __instance.Tile;
                    wo.GateDef = gateDef;
                    wo.DhdDef = dhdDef;
                    Find.WorldObjects.Add(wo);
                },
                defaultLabel = "SGM.CreateSGSite".Translate(),
                defaultDesc = "SGM.CreateSGSiteDesc".Translate()
            };
            StringBuilder reason = new StringBuilder();
            if (!containsStargate) commandCreateSite.Disable("SGM.NoGateInCaravan".Translate());
            else if (__instance.Tile.Tile.Landmark != null) commandCreateSite.Disable("SGM.BlockedByLandmark".Translate());
            else if (!TileFinder.IsValidTileForNewSettlement(__instance.Tile, reason)) commandCreateSite.Disable(reason.ToString());
            yield return commandCreateSite;
        }
    }


    [HarmonyPatch(typeof(FloatMenuOptionProvider_CarryingPawn))]
    [HarmonyPatch(nameof(FloatMenuOptionProvider_CarryingPawn.GetOptionsFor))]
    class FixCarryToTransporterForStargate
    {
        /*FloatMenuOptionProvider_CarryPawn adds a "Carry [Pawn] to [Thing]" option to any thing that has a CompTransporter, which doesn't work great with stargates because of the kinda unconventional way the CompStargate code utilizes the CompTransporter.
         This Harmony Prefix makes it not appear if the stargate is not active, and makes sure it actually works when the stargate is active.*/
        static bool Prefix(ref IEnumerable<FloatMenuOption> __result, Thing clickedThing, FloatMenuContext context)
        {
            CompStargate sgComp = clickedThing.TryGetComp<CompStargate>();
            Pawn selPawn = context.FirstSelectedPawn;
            Pawn carriedPawn = (Pawn)context.FirstSelectedPawn.carryTracker.CarriedThing;
            
            if (sgComp == null) return true;

            if (sgComp.StargateIsActive)
            {
                FloatMenuOption optionCarryToStargate = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("SGM.CarryHeldToStargateAction".Translate(carriedPawn, clickedThing), delegate
                {
                    selPawn.carryTracker.TryDropCarriedThing(selPawn.Position, ThingPlaceMode.Near, out Thing targPawn);
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_BringToStargate"), targPawn, clickedThing);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }), selPawn, clickedThing);

                __result = [optionCarryToStargate];
                return false;
            }

            /*Can't figure out how to not display a floatMenuOption without a NullRefException, so this's fine I guess*/
            __result = [new FloatMenuOption("SGM.CarryHeldToStargateAction_Disabled".Translate(), null)];
            return false;
        }
    }
}
