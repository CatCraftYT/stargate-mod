using System;
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
                    if (inner.TryGetComp<CompStargate>() != null) { containsStargate = true; break; }
                    
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
                    foreach (var thing in things)
                    {
                        Thing inner = thing.GetInnerIfMinified();
                        if (inner != null && inner.def.thingClass == typeof(Building_Stargate))
                        {
                            gateDef = inner.def; 
                            thing.holdingOwner.Remove(thing); 
                            break; 
                            
                        }
                    }
                    if (gateDef != null && !gateDef.HasComp<CompDialHomeDevice>())
                    {
                        things = __instance.AllThings.ToList();
                        foreach (var thing in things)
                        {
                            Thing inner = thing.GetInnerIfMinified();
                            if (inner?.TryGetComp<CompDialHomeDevice>() != null && inner.def.thingClass != typeof(Building_Stargate))
                            {
                                dhdDef = inner.def; 
                                thing.holdingOwner.Remove(thing); 
                                break;
                            }
                        }
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
            else if (__instance.Tile.Tile.Landmark != null) command.Disable("BlockedByLandmark".Translate());
            else if (!TileFinder.IsValidTileForNewSettlement(__instance.Tile, reason)) command.Disable(reason.ToString());
            yield return command;
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
            FloatMenuOption option = null;
            CompTransporter transporter = clickedThing.TryGetComp<CompTransporter>();
            Pawn selPawn = context.FirstSelectedPawn;
            Pawn carriedPawn = (Pawn)context.FirstSelectedPawn.carryTracker.CarriedThing;
            
            if (sgComp == null)
            {
                return true;
            }

            if (sgComp.StargateIsActive)
            {
                option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("CarryHeldToStargateAction".Translate(carriedPawn, clickedThing), delegate
                {
                    selPawn.carryTracker.TryDropCarriedThing(selPawn.Position, ThingPlaceMode.Near, out var targPawn);
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_BringToStargate"), targPawn, clickedThing);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }), selPawn, clickedThing);

                __result = new[] { option };
                return false;
            }

            __result = null;
            return false;
        }
    }
}
