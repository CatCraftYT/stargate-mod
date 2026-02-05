using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace StargatesMod;

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

//FloatmenuOptionProvider_CarryPawn adds a carry <pawn> to <transporter> option to any building using CompTransporter, which is wonky for stargates because of the irregular way it uses CompTransporter.
//This stops the floatmenu option from being applied to stargates (an actually working version of this option exists in FloatMenuOptionProvider_Stargate)
[HarmonyPatch(typeof(FloatMenuOptionProvider_CarryingPawn), "CarryToTransporter")]
public static class FloatMenuOptionProvider_CarryingPawn_Patch
{
    private static ThingComp GetStargateComp(Thing clickedThing)
    {
        return clickedThing.TryGetComp<CompStargate>();
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        List<CodeInstruction> codes = new(instructions);

        Label jumpLabel = il.DefineLabel();
        Label finishJumpLabel = il.DefineLabel();

        CodeInstruction finishJumpCode = new(OpCodes.Ldloc_0);
        finishJumpCode.labels.Add(finishJumpLabel);

        CodeInstruction jumpCode = new(OpCodes.Ldloc_0);
        jumpCode.labels.Add(jumpLabel);


        int insertionPoint = -1;

        for(int i=0; i<codes.Count; i++)
        {
            if (codes[i].opcode != OpCodes.Ret) continue;

            insertionPoint = i+2;
            break;
        }

        if(insertionPoint == -1) Log.Error("[StargatesMod] HarmonyPatches: could not find FloatMenuOptionProvider_CarryingPawn_Patch insertion point!");
        else 
        {
            List<CodeInstruction> newCodes =
            [
                //LdLoc_0 stolen from original code because there's a jumpLabel on it that would otherwise skip our code
                new(OpCodes.Ldfld, AccessTools.Field(AccessTools.Inner(typeof(FloatMenuOptionProvider_CarryingPawn), "<>c__DisplayClass12_0"), "clickedThing")), //put clickedThing on stack
                new(OpCodes.Call, AccessTools.Method(typeof(FloatMenuOptionProvider_CarryingPawn_Patch), nameof(GetStargateComp))), //call custom method, because I have no idea how to call TryGetComp<CompStargate> directly  with all this..
                new(OpCodes.Brfalse, finishJumpLabel), //if no CompStargate found, continue as normal
                
                //Else, return false and exit
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Ret),
                
                finishJumpCode, //replace the Ldloc_0 that we stole (with finishJumpCode for finishJumpLabel to target)
            ];

            codes.InsertRange(insertionPoint, newCodes);
        }
        return codes.AsEnumerable();
    }
}