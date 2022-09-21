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
        const int glowRadius = 10;
        const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        List<Thing> sendBuffer = new List<Thing>();
        List<Thing> recvBuffer = new List<Thing>();
        public int ticksSinceBufferUnloaded;
        public int ticksSinceOpened;
        public int gateAddress;
        public bool stargateIsActive = false;
        public bool isRecievingGate;
        public bool hasIris = false;
        public int ticksUntilOpen = -1;
        bool irisIsActivated = false;
        int queuedAddress;
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

        bool GateIsLoadingTransporter
        {
            get
            {
                CompTransporter transComp = this.parent.GetComp<CompTransporter>();
                return transComp != null && (transComp.LoadingInProgressOrReadyToLaunch && transComp.AnyInGroupHasAnythingLeftToLoad);
            }
        }

        #region DHD Controls
        public void OpenStargateDelayed(int address, int delay)
        {
            queuedAddress = address;
            ticksUntilOpen = delay;
        }

        public void OpenStargate(int address)
        {
            Thing gate = GetDialledStargate(address);
            if (address > -1 && (gate == null || gate.TryGetComp<CompStargate>().stargateIsActive))
            {
                Messages.Message("GateDialFailed".Translate(), MessageTypeDefOf.NegativeEvent);
                SGSoundDefOf.StargateMod_SGFailDial.PlayOneShot(SoundInfo.InMap(this.parent));
                return;
            }
            stargateIsActive = true;
            connectedAddress = address;

            if (connectedAddress != -1)
            {
                connectedStargate = GetDialledStargate(connectedAddress);
                CompStargate sgComp = connectedStargate.TryGetComp<CompStargate>();
                sgComp.stargateIsActive = true;
                sgComp.isRecievingGate = true;
                sgComp.connectedAddress = gateAddress;
                sgComp.connectedStargate = this.parent;

                sgComp.puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(sgComp.parent));
                SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(sgComp.parent));

                CompGlower otherGlowComp = sgComp.parent.GetComp<CompGlower>();
                otherGlowComp.Props.glowRadius = glowRadius;
                otherGlowComp.PostSpawnSetup(false);
            }

            puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(this.parent));
            SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(this.parent));

            CompGlower glowComp = this.parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = glowRadius;
            glowComp.PostSpawnSetup(false);
            if (Prefs.LogVerbose) { Log.Message($"StargateMod: finished opening gate {this.parent}"); }
        }

        public void CloseStargate(bool closeOtherGate)
        {
            CompTransporter transComp = this.parent.GetComp<CompTransporter>();
            if (transComp != null) { transComp.CancelLoad(); }
            //clear buffers just in case
            foreach (Thing thing in sendBuffer)
            {
                GenSpawn.Spawn(thing, this.parent.InteractionCell, this.parent.Map);
            }
            foreach (Thing thing in recvBuffer)
            {
                GenSpawn.Spawn(thing, this.parent.InteractionCell, this.parent.Map);
            }

            CompStargate sgComp = connectedStargate.TryGetComp<CompStargate>();
            if (closeOtherGate)
            {
                if (connectedStargate == null || sgComp == null) { Log.Warning($"Recieving stargate connected to stargate {this.parent.ThingID} didn't have CompStargate, but this stargate wanted it closed."); }
                else { sgComp.CloseStargate(false); }
            }

            SoundDef puddleCloseDef = SGSoundDefOf.StargateMod_SGClose;
            puddleCloseDef.PlayOneShot(SoundInfo.InMap(this.parent));
            if (sgComp != null) { puddleCloseDef.PlayOneShot(SoundInfo.InMap(sgComp.parent)); }
            if (puddleSustainer != null) { puddleSustainer.End(); }

            CompGlower glowComp = this.parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = 0;
            glowComp.PostSpawnSetup(false);

            if (Props.explodeOnUse)
            {
                CompExplosive explosive = this.parent.TryGetComp<CompExplosive>();
                if (explosive == null) { Log.Warning($"Stargate {this.parent.ThingID} has the explodeOnUse tag set to true but doesn't have CompExplosive."); }
                else { explosive.StartWick(); }
            }

            stargateIsActive = false;
            ticksSinceBufferUnloaded = 0;
            ticksSinceOpened = 0;
            connectedAddress = -1;
            connectedStargate = null;
            isRecievingGate = false;
        }
        #endregion

        public static Thing GetStargateOnMap(Map map, Thing thingToIgnore = null)
        {
            Thing gateOnMap = null;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing != thingToIgnore && thing.def.thingClass == typeof(Building_Stargate))
                {
                    gateOnMap = thing;
                    break;
                }
            }
            return gateOnMap;
        }

        public static string GetStargateDesignation(int address)
        {
            if (address < 0) { return "UnknownLower".Translate(); }
            Rand.PushState(address);
            //pattern: P(num)(char)-(num)(num)(num)
            string designation = $"P{Rand.RangeInclusive(0, 9)}{alpha[Rand.RangeInclusive(0, 25)]}-{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}";
            Rand.PopState();
            return designation;
        }

        private Thing GetDialledStargate(int address)
        {
            if (address < 0) { return null; }
            MapParent connectedMap = Find.WorldObjects.MapParentAt(address);
            if (connectedMap == null)
            {
                Log.Error($"Tried to get a paired stargate at address {address} but the map parent does not exist!");
                return null;
            }
            if (!connectedMap.HasMap)
            {
                if (Prefs.LogVerbose) { Log.Message($"StargateMod: generating map for {connectedMap}"); }
                GetOrGenerateMapUtility.GetOrGenerateMap(connectedMap.Tile, connectedMap as WorldObject_PermSGSite != null ? new IntVec3(75, 1, 75) : Find.World.info.initialMapSize, null);
                if (Prefs.LogVerbose) { Log.Message($"StargateMod: finished generating map"); }
            }
            Map map = connectedMap.Map;
            Thing gate = GetStargateOnMap(map);
            
            return gate;
        }

        private void PlayTeleportSound()
        {
            DefDatabase<SoundDef>.GetNamed($"StargateMod_teleport_{Rand.RangeInclusive(1, 4)}").PlayOneShot(SoundInfo.InMap(this.parent));
        }

        private void DoUnstableVortex()
        {
            List<Thing> excludedThings = new List<Thing>() { this.parent };
            foreach (IntVec3 pos in Props.vortexPattern)
            {
                foreach (Thing thing in this.parent.Map.thingGrid.ThingsAt(this.parent.Position + pos))
                {
                    if (Props.thingsExcludedFromVortex.Contains(thing.def)) { excludedThings.Add(thing); }
                }
            }

            foreach (IntVec3 pos in Props.vortexPattern)
            {
                DamageDef damType = DefDatabase<DamageDef>.GetNamed("StargateMod_KawooshExplosion");

                Explosion explosion = (Explosion)GenSpawn.Spawn(ThingDefOf.Explosion, this.parent.Position, this.parent.Map, WipeMode.Vanish);
                explosion.damageFalloff = false;
                explosion.damAmount = damType.defaultDamage;
                explosion.Position = this.parent.Position + pos;
                explosion.radius = 0.5f;
                explosion.damType = damType;
                explosion.StartExplosion(null, excludedThings);
            }
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
        #region Comp Overrides

        public override void PostDraw()
        {
            base.PostDraw();
            if (irisIsActivated)
            {
                StargateIris.Draw(this.parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.01f), Rot4.North, this.parent);
            }
            if (stargateIsActive)
            {
                StargatePuddle.Draw(this.parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.02f), Rot4.North, this.parent);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (ticksUntilOpen > 0)
            {
                ticksUntilOpen--;
                if (ticksUntilOpen == 0)
                {
                    ticksUntilOpen = -1;
                    OpenStargate(queuedAddress);
                    queuedAddress = -1;
                }
            }
            if (stargateIsActive)
            {
                if (!irisIsActivated && ticksSinceOpened < 150 && ticksSinceOpened % 10 == 0)
                {
                    DoUnstableVortex();
                }

                if (this.parent.Fogged())
                {
                    FloodFillerFog.FloodUnfog(this.parent.Position, this.parent.Map);
                }
                CompStargate sgComp = connectedStargate.TryGetComp<CompStargate>();

                CompTransporter transComp = this.parent.GetComp<CompTransporter>();
                if (transComp != null)
                {
                    Thing thing = transComp.innerContainer.FirstOrFallback();
                    if (thing != null)
                    {
                        if (thing.Spawned) { thing.DeSpawn(); }
                        this.AddToSendBuffer(thing);
                        transComp.innerContainer.Remove(thing);
                        transComp.SubtractFromToLoadList(thing, int.MaxValue, false);
                    }
                    else if (transComp.LoadingInProgressOrReadyToLaunch && !transComp.AnyInGroupHasAnythingLeftToLoad) { transComp.CancelLoad(); }
                }

                if (sendBuffer.Any())
                {
                    if (!isRecievingGate)
                    {
                        for (int i = 0; i <= sendBuffer.Count; i++)
                        {
                            sgComp.AddToRecieveBuffer(sendBuffer[i]);
                            this.sendBuffer.Remove(sendBuffer[i]);
                        }

                    }
                    else
                    {
                        for (int i = 0; i <= sendBuffer.Count; i++)
                        {
                            sendBuffer[i].Kill();
                            this.sendBuffer.Remove(sendBuffer[i]);
                        }
                    }
                }

                if (recvBuffer.Any() && ticksSinceBufferUnloaded > Rand.Range(10, 80))
                {
                    ticksSinceBufferUnloaded = 0;
                    if (!irisIsActivated)
                    {
                        GenSpawn.Spawn(recvBuffer[0], this.parent.InteractionCell, this.parent.Map);
                        this.recvBuffer.Remove(recvBuffer[0]);
                        PlayTeleportSound();
                    }
                    else
                    {
                        recvBuffer[0].Kill();
                        this.recvBuffer.Remove(recvBuffer[0]);
                        SGSoundDefOf.StargateMod_IrisHit.PlayOneShot(SoundInfo.InMap(this.parent));
                    }
                }
                if (connectedAddress == -1 && !recvBuffer.Any()) { CloseStargate(false); }
                ticksSinceBufferUnloaded++;
                ticksSinceOpened++;
                if (isRecievingGate && ticksSinceBufferUnloaded > 2500 && !connectedStargate.TryGetComp<CompStargate>().GateIsLoadingTransporter) { CloseStargate(true); }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            gateAddress = this.parent.Map.Tile;
            Find.World.GetComponent<WorldComp_StargateAddresses>().AddAddress(gateAddress);

            if (stargateIsActive)
            {
                if (connectedStargate == null && connectedAddress != -1) { connectedStargate = GetDialledStargate(connectedAddress); }
                puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(this.parent));
            }

            //fix nullreferenceexception that happens when the innercontainer disappears for some reason, hopefully this doesn't end up causing a bug that will take hours to track down ;)
            CompTransporter transComp = this.parent.GetComp<CompTransporter>();
            if (transComp != null && transComp.innerContainer == null)
            {
                transComp.innerContainer = new ThingOwner<Thing>(transComp);
            }
            if (Prefs.LogVerbose) { Log.Message($"StargateMod: compsg postspawnssetup: sgactive={stargateIsActive} connectgate={connectedStargate} connectaddress={connectedAddress}, mapparent={this.parent.Map.Parent}"); }
        }

        public string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("GateAddress".Translate(GetStargateDesignation(gateAddress)));
            if (!stargateIsActive) { sb.AppendLine("InactiveFacility".Translate().CapitalizeFirst()); }
            else
            {
                sb.AppendLine("ConnectedToGate".Translate(GetStargateDesignation(connectedAddress), (isRecievingGate ? "Incoming" : "Outgoing").Translate()));
            }
            if (this.hasIris) { sb.AppendLine("IrisStatus".Translate((irisIsActivated ? "IrisClosed" : "IrisOpen").Translate())); }
            if (ticksUntilOpen > 0) { sb.AppendLine("TimeUntilGateLock".Translate(ticksUntilOpen.ToStringTicksToPeriod())); }
            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (Props.canHaveIris && hasIris)
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "OpenCloseIris".Translate(),
                    defaultDesc = "OpenCloseIrisDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get(Props.irisTexture, true),
                    action = delegate ()
                    {
                        irisIsActivated = !irisIsActivated;
                        if (irisIsActivated) { SGSoundDefOf.StargateMod_IrisOpen.PlayOneShot(SoundInfo.InMap(this.parent)); }
                        else { SGSoundDefOf.StargateMod_IrisClose.PlayOneShot(SoundInfo.InMap(this.parent)); }
                    }
                };
                yield return command;
            }

            if (Prefs.DevMode)
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "Add/remove iris",
                    action = delegate ()
                    {
                        this.hasIris = !this.hasIris;
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
            yield return new FloatMenuOption("EnterStargateAction".Translate(), () =>
            {
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_EnterStargate"), this.parent);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
            yield return new FloatMenuOption("BringPawnToGateAction".Translate(), () =>
            {
                TargetingParameters targetingParameters = new TargetingParameters()
                {
                    onlyTargetIncapacitatedPawns = true,
                    canTargetBuildings = false,
                    canTargetItems = true,
                };

                Find.Targeter.BeginTargeting(targetingParameters, delegate (LocalTargetInfo t)
                {
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_BringToStargate"), t.Thing, this.parent);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
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
            yield return new FloatMenuOption("EnterStargateWithSelectedAction".Translate(), () =>
            {
                foreach (Pawn selPawn in allowedPawns)
                {
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_EnterStargate"), this.parent);
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
            Scribe_Values.Look<bool>(ref hasIris, "hasIris");
            Scribe_Values.Look<bool>(ref irisIsActivated, "irisIsActivated");
            Scribe_Values.Look<int>(ref ticksSinceOpened, "ticksSinceOpened");
            Scribe_Values.Look<int>(ref connectedAddress, "connectedAddress");
            Scribe_References.Look(ref connectedStargate, "connectedStargate");
            Scribe_Collections.Look(ref recvBuffer, "recvBuffer", LookMode.GlobalTargetInfo);
            Scribe_Collections.Look(ref sendBuffer, "sendBuffer", LookMode.GlobalTargetInfo);
        }

        public override string CompInspectStringExtra()
        {
            return base.CompInspectStringExtra() + "RespawnGateString".Translate();
        }
        #endregion
    }

    public class CompProperties_Stargate : CompProperties
    {
        public CompProperties_Stargate()
        {
            this.compClass = typeof(CompStargate);
        }
        public bool canHaveIris = true;
        public bool explodeOnUse = false;
        public string puddleTexture;
        public string irisTexture;
        public Vector2 puddleDrawSize;
        public List<ThingDef> thingsExcludedFromVortex = new List<ThingDef>();
        public List<IntVec3> vortexPattern = new List<IntVec3>
        {
            new IntVec3(0,0,1),
            new IntVec3(1,0,1),
            new IntVec3(-1,0,1),
            new IntVec3(0,0,0),
            new IntVec3(1,0,0),
            new IntVec3(-1,0,0),
            new IntVec3(0,0,-1),
            new IntVec3(1,0,-1),
            new IntVec3(-1,0,-1),
            new IntVec3(0,0,-2),
            new IntVec3(1,0,-2),
            new IntVec3(-1,0,-2),
            new IntVec3(0,0,-3)
        };
    }
}
