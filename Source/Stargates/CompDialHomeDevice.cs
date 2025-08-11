using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using Verse.AI;

namespace StargatesMod
{
    public class CompDialHomeDevice : ThingComp
    {
        CompFacility compFacility;
        public PlanetTile lastDialledAddress;

        public CompProperties_DialHomeDevice Props => (CompProperties_DialHomeDevice)this.props;

        public CompStargate GetLinkedStargate()
        {
            if (Props.selfDialler) return parent.TryGetComp<CompStargate>(); 
            if (compFacility.LinkedBuildings.Count == 0)  return null; 
            return compFacility.LinkedBuildings[0].TryGetComp<CompStargate>();
        }

        public static Thing GetDHDOnMap(Map map)
        {
            Thing dhdOnMap = null;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing.TryGetComp<CompDialHomeDevice>() != null && thing.def.thingClass != typeof(Building_Stargate))
                {
                    dhdOnMap = thing;
                    break;
                }
            }
            return dhdOnMap;
        }

        public bool IsConnectedToStargate
        {
            get
            {
                if (Props.selfDialler) return true;
                if (compFacility.LinkedBuildings.Count == 0)
                    return false;

                return true;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compFacility = parent.GetComp<CompFacility>();
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (!IsConnectedToStargate || !selPawn.CanReach(parent.InteractionCell, PathEndMode.Touch, Danger.Deadly))
            {
                yield break;
            }
            
            if (Props.requiresPower)
            {
                CompPowerTrader compPowerTrader = this.parent.TryGetComp<CompPowerTrader>();


                if (compPowerTrader != null && !compPowerTrader.PowerOn)
                {
                    yield return new FloatMenuOption("CannotDialNoPower".Translate(), null);
                    yield break;
                }
            }
            
            CompStargate stargate = GetLinkedStargate();
            
            if (stargate != null)
            {
                if (stargate.StargateIsActive)
                {
                    yield return new FloatMenuOption("CannotDialGateIsActive".Translate(), null);
                    yield break;
                }

                WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
                addressComp.CleanupAddresses();
                
                if (addressComp.AddressList.Count < 2)
                {
                    yield return new FloatMenuOption("CannotDialNoDestinations".Translate(), null);
                    yield break;
                }
                
                if (stargate.TicksUntilOpen > -1)
                {
                    yield return new FloatMenuOption("CannotDialIncoming".Translate(), null);
                    yield break;
                }


                
                foreach (int i in addressComp.AddressList)
                {
                    if (i != stargate.GateAddress)
                    {
                        MapParent sgMap = Find.WorldObjects.MapParentAt(i);
                        yield return new FloatMenuOption("DialGate".Translate(CompStargate.GetStargateDesignation(i), sgMap.Label), () =>
                        {
                            lastDialledAddress = i;
                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DialStargate"), parent);
                            selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        });
                    }
                }
            }
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) 
                yield return gizmo;

            CompStargate stargate = GetLinkedStargate();
            if (stargate != null)
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "CloseStargate".Translate(),
                    defaultDesc = "CloseStargateDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = delegate
                    {
                        stargate.CloseStargate(true);
                    }
                };
                if (!stargate.StargateIsActive) { command.Disable("GateIsNotActive".Translate()); }
                else if (stargate.IsReceivingGate) { command.Disable("CannotCloseIncoming".Translate()); }
                yield return command;
            }
        }
    }
    public class CompProperties_DialHomeDevice : CompProperties
    {
        public CompProperties_DialHomeDevice()
        {
            this.compClass = typeof(CompDialHomeDevice);
        }
        public bool selfDialler = false;
        public bool requiresPower = false;
    }
}
