using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class CompDialHomeDevice : ThingComp
    {
        CompFacility compFacility;
        public int lastDialledAddress;

        public CompProperties_DialHomeDevice Props => (CompProperties_DialHomeDevice)this.props;

        public CompStargate GetLinkedStargate()
        {
            if (Props.selfDialler) { return this.parent.TryGetComp<CompStargate>(); }
            if (compFacility.LinkedBuildings.Count == 0) { return null; }
            return compFacility.LinkedBuildings[0].TryGetComp<CompStargate>();
        }

        private bool isConnectedToStargate
        {
            get
            {
                if (Props.selfDialler) { return true; }
                if (compFacility.LinkedBuildings.Count == 0)
                {
                    return false;
                }
                return true;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            this.compFacility = this.parent.GetComp<CompFacility>();
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (!isConnectedToStargate || !selPawn.CanReach(this.parent.InteractionCell, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn))
            {
                yield break;
            }

            CompStargate stargate = GetLinkedStargate();
            if (!stargate.stargateIsActive)
            {
                foreach (int i in Find.World.GetComponent<WorldComp_StargateAddresses>().addressList)
                {
                    if (i != stargate.gateAddress)
                    {
                        MapParent sgMap = Find.WorldObjects.MapParentAt(i);
                        
                        yield return new FloatMenuOption($"Dial {i} ({sgMap.Label})", () =>
                        {
                            lastDialledAddress = i;
                            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_DialStargate"), this.parent);
                            selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        });
                    }
                }
            }
            yield break;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            CompStargate stargate = this.GetLinkedStargate();
            if (stargate != null)
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "Close stargate",
                    defaultDesc = "Close the linked stargate.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true)
                };
                command.action = delegate ()
                {
                    stargate.CloseStargate(true);
                };
                if (!stargate.stargateIsActive) { command.Disable("Stargate is not active."); }
                else if (stargate.isRecievingGate) { command.Disable("Cannot disengage an incoming wormhole."); }
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
    }
}
