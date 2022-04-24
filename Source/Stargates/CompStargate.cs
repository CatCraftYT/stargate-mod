using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StargatesMod
{
    public class CompStargate : ThingComp
    {
        List<Thing> sendBuffer = new List<Thing>();
        List<Thing> recvBuffer = new List<Thing>();
        int ticksSinceBufferUnloaded;
        public int gateAddress;
        public bool stargateIsActive = false;
        public bool isRecievingGate;
        bool irisIsActivated = false;
        int connectedAddress = -1;
        Thing connectedStargate;
        Sustainer puddleSustainer;

        Graphic stargatePuddle;
        Graphic stargateIris;

        public CompProperties_Stargate Props => (CompProperties_Stargate)this.props;

        Graphic StargatePuddle
        {
            get
            {
                if (stargatePuddle == null)
                {
                    stargatePuddle = GraphicDatabase.Get<Graphic_Single>(Props.puddleTexture, ShaderDatabase.Mote, Props.puddleDrawSize, Color.white);
                }
                return stargatePuddle;
            }
        }
        Graphic StargateIris
        {
            get
            {
                if (stargateIris == null)
                {
                    stargateIris = GraphicDatabase.Get<Graphic_Single>(Props.irisTexture, ShaderDatabase.Mote, Props.puddleDrawSize, Color.white);
                }
                return stargateIris;
            }
        }

        #region DHD Controls
        public void OpenStargate(int address)
        {
            stargateIsActive = true;
            connectedAddress = address;

            SoundDef puddleSustainerDef = DefDatabase<SoundDef>.GetNamed("StargateMod_SGIdle");
            SoundDef puddleOpenDef = DefDatabase<SoundDef>.GetNamed("StargateMod_SGOpen");
            if (connectedAddress != -1)
            {
                connectedStargate = GetDialledStargate();
                CompStargate sgComp = connectedStargate.TryGetComp<CompStargate>();
                sgComp.stargateIsActive = true;
                sgComp.isRecievingGate = true;
                sgComp.connectedAddress = gateAddress;
                sgComp.connectedStargate = this.parent;

                sgComp.puddleSustainer = puddleSustainerDef.TrySpawnSustainer(SoundInfo.InMap(sgComp.parent));
                puddleOpenDef.PlayOneShot(SoundInfo.InMap(sgComp.parent));
            }

            puddleSustainer = puddleSustainerDef.TrySpawnSustainer(SoundInfo.InMap(this.parent));
            puddleOpenDef.PlayOneShot(SoundInfo.InMap(this.parent));
            
        }

        public void CloseStargate(bool closeOtherGate)
        {
            //clear buffers just in case
            foreach (Thing thing in sendBuffer)
            {
                GenPlace.TryPlaceThing(thing, this.parent.InteractionCell, parent.Map, ThingPlaceMode.Near);
            }
            foreach (Thing thing in recvBuffer)
            {
                GenPlace.TryPlaceThing(thing, this.parent.InteractionCell, parent.Map, ThingPlaceMode.Near);
            }

            CompStargate sgComp = connectedStargate.TryGetComp<CompStargate>();
            if (closeOtherGate)
            {
                if (connectedStargate == null || sgComp == null) { Log.Error($"Recieving stargate connected to stargate {this.parent.ThingID} didn't have a Stargate component, but this stargate wanted it closed."); }
                sgComp.CloseStargate(false);
            }

            stargateIsActive = false;
            ticksSinceBufferUnloaded = 0;
            connectedAddress = -1;
            connectedStargate = null;
            isRecievingGate = false;

            SoundDef puddleCloseDef = DefDatabase<SoundDef>.GetNamed("StargateMod_SGClose");
            puddleCloseDef.PlayOneShot(SoundInfo.InMap(this.parent));
            if (sgComp != null) { puddleCloseDef.PlayOneShot(SoundInfo.InMap(sgComp.parent)); }
            puddleSustainer.End();
        }
        #endregion

        private Thing GetDialledStargate()
        {
            MapParent connectedMap = Find.WorldObjects.MapParentAt(connectedAddress);
            if (connectedMap == null)
            {
                Log.Error($"Tried to get a paired stargate at address {connectedAddress} but it does not exist!");
                return null;
            }
            if (!connectedMap.HasMap)
            {
                //generate stargate map
            }
            List<Thing> connectedGateList = connectedMap.Map.listerThings.ThingsOfDef(this.parent.def);
            if (connectedGateList.Count == 0) { Log.Error($"Tried to get dialled stargate in map {connectedMap.ID}, but it did not exist!"); return null; }
            if (connectedGateList.Count > 1) {  Log.Warning($"There were multiple stargates in map {connectedMap.ID}. Using the first found."); }
            return connectedGateList[0];
        }

        private void PlayTeleportSound()
        {
            DefDatabase<SoundDef>.GetNamed($"StargateMod_teleport_{Rand.RangeInclusive(1, 4)}").PlayOneShot(SoundInfo.InMap(this.parent));
        }

        public void AddToSendBuffer(Thing thing)
        {
            sendBuffer.Add(thing);
            PlayTeleportSound();
        }

        public void AddToRecieveBuffer(Thing thing)
        {
            recvBuffer.Add(thing);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (irisIsActivated)
            {
                StargateIris.Draw(this.parent.Position.ToVector3Shifted(), Rot4.North, this.parent);
            }
            else if (stargateIsActive)
            {
                StargatePuddle.Draw(this.parent.Position.ToVector3Shifted(), Rot4.North, this.parent);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (stargateIsActive)
            {
                CompStargate sgComp = connectedStargate.TryGetComp<CompStargate>();

                if (!isRecievingGate && sendBuffer.Any())
                {
                    for (int i = 0; i <= sendBuffer.Count; i++)
                    {
                        sgComp.AddToRecieveBuffer(sendBuffer[i]);
                        this.sendBuffer.Remove(sendBuffer[i]);
                    }
                }
                else if (isRecievingGate && sendBuffer.Any())
                {
                    for (int i = 0; i <= sendBuffer.Count; i++)
                    {
                        sendBuffer[i].Kill();
                        this.sendBuffer.Remove(sendBuffer[i]);
                    }
                }

                if (ticksSinceBufferUnloaded > Rand.Range(20, 40) && recvBuffer.Any())
                {
                    ticksSinceBufferUnloaded = 0;
                    if (!irisIsActivated)
                    {
                        GenPlace.TryPlaceThing(recvBuffer[0], this.parent.InteractionCell, parent.Map, ThingPlaceMode.Near).ToString();
                        this.recvBuffer.Remove(recvBuffer[0]);
                        PlayTeleportSound();
                    }
                    else
                    {
                        recvBuffer[0].Kill();
                        this.recvBuffer.Remove(recvBuffer[0]);
                        DefDatabase<SoundDef>.GetNamed("StargateMod_IrisHit").PlayOneShot(SoundInfo.InMap(this.parent));
                    }
                }
                if (connectedAddress == -1 && !recvBuffer.Any()) { CloseStargate(false); }
                ticksSinceBufferUnloaded++;
                if (ticksSinceBufferUnloaded > 2500) { CloseStargate(true); }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            gateAddress = this.parent.Map.Tile;
            Find.World.GetComponent<WorldComp_StargateAddresses>().AddAddress(gateAddress);

            if (stargateIsActive)
            {
                if (connectedStargate == null && connectedAddress != -1) { connectedStargate = GetDialledStargate(); }
                puddleSustainer = DefDatabase<SoundDef>.GetNamed("StargateMod_SGIdle").TrySpawnSustainer(SoundInfo.InMap(this.parent));
            }
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Gate address: {gateAddress}");
            if (!stargateIsActive) { sb.Append("Inactive"); }
            else
            {
                sb.Append($"Connected to stargate: {connectedAddress}");
            }
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (Props.hasIris && DefDatabase<ResearchProjectDef>.GetNamed("StargateMod_StargateIrisResearch").IsFinished)
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "Open/close iris",
                    defaultDesc = "Open or close this stargate's iris.",
                    icon = ContentFinder<Texture2D>.Get(Props.irisTexture, true),
                    action = delegate ()
                    {
                        irisIsActivated = !irisIsActivated;
                        if (irisIsActivated) { DefDatabase<SoundDef>.GetNamed("StargateMod_IrisOpen").PlayOneShot(SoundInfo.InMap(this.parent)); }
                        else { DefDatabase<SoundDef>.GetNamed("StargateMod_IrisClose").PlayOneShot(SoundInfo.InMap(this.parent)); }
                    }
                };
                yield return command;
            }
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (!stargateIsActive || irisIsActivated || !selPawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn))
            {
                yield break;
            }
            yield return new FloatMenuOption("Enter stargate", () =>
            {
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_EnterStargate"), this.parent);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
            yield break;
        }

        public override IEnumerable<FloatMenuOption> CompMultiSelectFloatMenuOptions(List<Pawn> selPawns)
        {
            if (!stargateIsActive) { yield break; }
            List<Pawn> allowedPawns = new List<Pawn>();
            foreach (Pawn selPawn in selPawns)
            {
                if (selPawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn))
                {
                    allowedPawns.Add(selPawn);
                }
            }
            yield return new FloatMenuOption("Enter stargate with selected", () =>
            {
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_EnterStargate"), this.parent);
                foreach (Pawn selPawn in allowedPawns)
                {
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            });
            yield break;
        }

        private void CleanupGate()
        {
            if (connectedStargate != null)
            {
                CloseStargate(true);
            }
            Find.World.GetComponent<WorldComp_StargateAddresses>().RemoveAddress(gateAddress);
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            CleanupGate();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            CleanupGate();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<bool>(ref stargateIsActive, "stargateIsActive");
            Scribe_Values.Look<bool>(ref isRecievingGate, "isRecievingGate");
            Scribe_Values.Look<bool>(ref irisIsActivated, "irisIsActivated");
            Scribe_Values.Look<int>(ref connectedAddress, "connectedAddress");
            Scribe_References.Look(ref connectedStargate, "connectedStargate");
            Scribe_Collections.Look(ref recvBuffer, "recvBuffer", LookMode.GlobalTargetInfo);
            Scribe_Collections.Look(ref sendBuffer, "sendBuffer", LookMode.GlobalTargetInfo);
        }
    }

    public class CompProperties_Stargate : CompProperties
    {
        public CompProperties_Stargate()
        {
            this.compClass = typeof(CompStargate);
        }
        public bool hasIris = true;
        public string puddleTexture;
        public string irisTexture;
        public Vector2 puddleDrawSize;
    }
}
