using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StargatesMod.Mod_Settings;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace StargatesMod
{
    public class CompStargate : ThingComp
    {
        const int glowRadius = 10;
        const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private List<Thing> _sendBuffer = new List<Thing>();
        private List<Thing> _recvBuffer = new List<Thing>();
        public int TicksSinceBufferUnloaded;
        public int TicksSinceOpened;
        public PlanetTile GateAddress;
        public int PocketMapGateAddress;
        public bool IsInPocketMap = false;
        public bool StargateIsActive;
        public bool IsReceivingGate;
        public bool IsHibernating;
        Thing _conflictingGate;
        public bool HasIris = false;
        public int TicksUntilOpen = -1;
        public bool IrisIsActivated = false;
        private int _prevRingSoundQueue = 0;
        private int _chevronSoundCounter = 0;
        PlanetTile _queuedAddress = -1;
        int _queuedAddressPocketMap = -1;
        PlanetTile _connectedAddress = -1;
        int _connectedAddressPocketMap;
        Thing _connectedStargate;

        private int _checkVortexPawnsTick = -1;
        private const int _checkVortexPawnsDelayTick = 120;
        private List<Pawn> _pawnsWatchingStargate;
        
        private Sustainer _puddleSustainer;
        private Graphic _stargatePuddle;
        private Graphic _stargateIris;

        private readonly StargatesMod_Settings _settings = LoadedModManager.GetMod<StargatesMod_Mod>().GetSettings<StargatesMod_Settings>();
        
        public CompProperties_Stargate Props => (CompProperties_Stargate)props;

        Graphic StargatePuddle =>
            _stargatePuddle ?? (_stargatePuddle = GraphicDatabase.Get<Graphic_Single>(Props.puddleTexture,
                ShaderDatabase.Mote, Props.puddleDrawSize, Color.white));

        Graphic StargateIris =>
            _stargateIris ?? (_stargateIris = GraphicDatabase.Get<Graphic_Single>(Props.irisTexture,
                ShaderDatabase.Mote, Props.puddleDrawSize, Color.white));

        bool GateIsLoadingTransporter
        {
            get
            {
                CompTransporter transComp = parent.GetComp<CompTransporter>();
                return transComp != null && (transComp.LoadingInProgressOrReadyToLaunch && transComp.AnyInGroupHasAnythingLeftToLoad);
            }
        }

        public IEnumerable<IntVec3> VortexCells
        {
            get
            {
                //TODO Improve?
                Rot4 rot = parent.Rotation;
                if (rot == Rot4.North)
                {
                    foreach (IntVec3 offset in Props.vortexPattern_north) yield return offset + parent.Position;
                }
                else if (rot == Rot4.South)
                {
                    foreach (IntVec3 offset in Props.vortexPattern_south) yield return offset + parent.Position;
                }
                else if (rot == Rot4.East)
                {
                    foreach (IntVec3 offset in Props.vortexPattern_east) yield return offset + parent.Position;
                }
                else if (rot == Rot4.West)
                {
                    foreach (IntVec3 offset in Props.vortexPattern_west) yield return offset + parent.Position;
                }
                
            }
        }

        #region DHD Controls

        public void OpenStargateDelayed(PlanetTile address, int delay)
        {
            _queuedAddress = address;
            TicksUntilOpen = delay;
        }
        
        public void OpenStargateDelayed(int mapIndex, int delay) //Pocket Map
        {
            _queuedAddressPocketMap = mapIndex;
            TicksUntilOpen = delay;
        }

        public void OpenStargate(PlanetTile address)
         {
             if (_queuedAddressPocketMap > -1)
             {
                 Log.Error($"StargatesMod: Tried opening stargate with regular address but a pocketmap address was queued - in CompStargate");
                 _queuedAddress = -1;
                 _queuedAddressPocketMap = -1;
                 return;
             }
             
             MapParent connectedMapParent = Find.WorldObjects.MapParentAt(address);
             if (!connectedMapParent.HasMap)
             {
                 if (Prefs.LogVerbose) Log.Message($"StargatesMod: generating map for {connectedMapParent}");
                
                LongEventHandler.QueueLongEvent(delegate
                {
                    GetOrGenerateMapUtility.GetOrGenerateMap(connectedMapParent.Tile, connectedMapParent is WorldObject_PermSGSite ? new IntVec3(75, 1, 75) : Find.World.info.initialMapSize, connectedMapParent.def);
                }, "SGM.GeneratingStargateSite", doAsynchronously: false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap, callback: delegate
                {
                    if (Prefs.LogVerbose) Log.Message($"StargatesMod: finished generating map");

                    FinishDiallingStargate(address, connectedMapParent);
                }); 
             }
             else
             {
                 FinishDiallingStargate(address, connectedMapParent);
             }
         }
        
        public void OpenStargate(int mapIndex) //Pocket Map
        {
            if (_queuedAddress > -1)
            {
                Log.Error($"StargatesMod: Tried opening stargate with pocketmap address but a regular address was queued - in CompStargate");
                _queuedAddress = -1;
                _queuedAddressPocketMap = -1;
                return;
            }
            
            PocketMapParent connectedMap = Find.Maps[mapIndex].PocketMapParent;
            if (connectedMap == null || !connectedMap.HasMap)
                return;

            Thing connectedGate = GetStargateOnMap(Find.Maps[mapIndex]);

            if (connectedGate == null || connectedGate.TryGetComp<CompStargate>().StargateIsActive)
            {
                DialFail();
                return;
            }
            StargateIsActive = true;
            _connectedAddressPocketMap = mapIndex;

            _connectedStargate = connectedGate;
            CompStargate sgComp = _connectedStargate.TryGetComp<CompStargate>();
            sgComp.StargateIsActive = true;
            sgComp.IsReceivingGate = true;
            
            if (IsInPocketMap) sgComp._connectedAddressPocketMap = PocketMapGateAddress;
            else sgComp._connectedAddress = GateAddress;
            sgComp._connectedStargate = parent;

            sgComp._puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(sgComp.parent));
            SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(sgComp.parent));

            CompGlower otherGlowComp = sgComp.parent.GetComp<CompGlower>();
            otherGlowComp.Props.glowRadius = glowRadius;
            otherGlowComp.PostSpawnSetup(false);
            
            _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(parent));

            CompGlower glowComp = parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = glowRadius;
            glowComp.PostSpawnSetup(false);
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: finished opening gate {parent}");
        }


        private void FinishDiallingStargate(PlanetTile address, MapParent connectedMapParent)
        {
            Thing connectedGate = GetStargateOnMap(connectedMapParent.Map);

            if (address > -1 && (connectedGate == null || connectedGate.TryGetComp<CompStargate>().StargateIsActive))
            {
                DialFail();
                return;
            }
            StargateIsActive = true;
            _connectedAddress = address;

            if (_connectedAddress > -1)
            {
                _connectedStargate = connectedGate;
                CompStargate sgComp = _connectedStargate.TryGetComp<CompStargate>();
                sgComp.StargateIsActive = true;
                sgComp.IsReceivingGate = true;
                sgComp._connectedAddress = GateAddress;
                sgComp._connectedStargate = parent;

                sgComp._puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(sgComp.parent));
                SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(sgComp.parent));

                CompGlower otherGlowComp = sgComp.parent.GetComp<CompGlower>();
                otherGlowComp.Props.glowRadius = glowRadius;
                otherGlowComp.PostSpawnSetup(false);
            }
            
            _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(parent));

            CompGlower glowComp = parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = glowRadius;
            glowComp.PostSpawnSetup(false);
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: finished opening gate {parent}");
        }
        
        public void CloseStargate(bool closeOtherGate)
        {
            CompTransporter transComp = parent.GetComp<CompTransporter>();
            transComp?.CancelLoad();

            //clear buffers just in case
            foreach (Thing thing in _sendBuffer)
                GenSpawn.Spawn(thing, parent.InteractionCell, parent.Map);

            foreach (Thing thing in _recvBuffer)
                GenSpawn.Spawn(thing, parent.InteractionCell, parent.Map);

            CompStargate connectedGateComp = null;
            if (closeOtherGate)
            {
                connectedGateComp = _connectedStargate.TryGetComp<CompStargate>();
                
                if (_connectedStargate == null || connectedGateComp == null)
                    Log.Warning($"Receiving stargate connected to stargate {parent.ThingID} didn't have CompStargate, but this stargate wanted it closed.");
                else connectedGateComp.CloseStargate(false);
            }

            SoundDef puddleCloseDef = SGSoundDefOf.StargateMod_SGClose;
            puddleCloseDef.PlayOneShot(SoundInfo.InMap(parent));
            if (connectedGateComp != null) puddleCloseDef.PlayOneShot(SoundInfo.InMap(connectedGateComp.parent));

            _puddleSustainer?.End();

            CompGlower glowComp = parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = 0;
            glowComp.PostSpawnSetup(false);

            if (Props.explodeOnUse)
            {
                CompExplosive explosive = parent.TryGetComp<CompExplosive>();
                if (explosive == null) Log.Warning($"Stargate {parent.ThingID} has the explodeOnUse tag set to true but doesn't have CompExplosive.");
                else explosive.StartWick();
            }

            StargateIsActive = false;
            TicksSinceBufferUnloaded = 0;
            TicksSinceOpened = 0;
            _connectedAddress = -1;
            _connectedStargate = null;
            IsReceivingGate = false;
        }

        private void DialFail()
        {
            Messages.Message("SGM.GateDialFailed".Translate(), MessageTypeDefOf.NegativeEvent);
            SGSoundDefOf.StargateMod_SGFailDial.PlayOneShot(SoundInfo.InMap(parent));

            _queuedAddress = -1;
            _queuedAddressPocketMap = -1;
            _connectedStargate = null;
        }
        
        #endregion

        public static Thing GetStargateOnMap(Map map, Thing thingToIgnore = null)
        {
            Thing gateOnMap = null;
            
            Thing thing = map.listerBuildings.allBuildingsColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)).FirstOrFallback() ??
                          map.listerBuildings.allBuildingsNonColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)).FirstOrFallback();

            if (thing != thingToIgnore) 
                gateOnMap = thing;
            
            return gateOnMap;
        }

        public static string GetStargateDesignation(PlanetTile address)
        {
            if (address.tileId < 0) return "UnknownLower".Translate();
            
            Rand.PushState(address.tileId);
            //pattern: (pLDesignation)(num)(char)-(num)(num)(num)
            string pLDesignation = address.Layer.Def.isSpace ? "O" : "P"; //Planet layer designation: O for orbit / space, P for planetary / other
            string designation = $"{pLDesignation}{Rand.RangeInclusive(0, 9)}{alpha[Rand.RangeInclusive(0, 25)]}-{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}"; 
            Rand.PopState();
            
            return designation;
        }

        private void PlayTeleportSound()
        {
            DefDatabase<SoundDef>.GetNamed($"StargateMod_teleport_{Rand.RangeInclusive(1, 4)}").PlayOneShot(SoundInfo.InMap(parent));
        }

        public void ChangeIrisState(bool checkValid = false)
        {
            if (checkValid && (!Props.canHaveIris || !HasIris)) return;
            IrisIsActivated = !IrisIsActivated;
            
            if (IrisIsActivated) SGSoundDefOf.StargateMod_IrisOpen.PlayOneShot(SoundInfo.InMap(parent));
            else SGSoundDefOf.StargateMod_IrisClose.PlayOneShot(SoundInfo.InMap(parent));
        }

        private void DoUnstableVortex()
        {
            List<Thing> excludedThings = new List<Thing> { parent };
            
            List<IntVec3> vortexPattern = new List<IntVec3>();
            Rot4 rot = parent.Rotation;
            if (rot == Rot4.North) vortexPattern = Props.vortexPattern_north;
            if (rot == Rot4.South) vortexPattern = Props.vortexPattern_south;
            if (rot == Rot4.East) vortexPattern = Props.vortexPattern_east;
            if (rot == Rot4.West) vortexPattern = Props.vortexPattern_west;
            
            excludedThings.AddRange(from pos in vortexPattern 
                from thing in parent.Map.thingGrid.ThingsAt(parent.Position + pos) 
                where thing.def.passability == Traversability.Standable select thing);
            
            /*List<ThingDef> destroySpecial = new List<ThingDef>(); // TODO ADD
            foreach (IntVec3 pos in Props.vortexPattern)
            {
                foreach (Thing thing in parent.Map.thingGrid.ThingsAt(parent.Position + pos))
                {
                    // Stop vortex from destroying walls in the vortex cells behind the gate, otherwise it will breach the gateroom of orbital gate sites
                    if ((pos == new IntVec3(0, 0, 1) || pos == new IntVec3(1, 0, 1) || pos == new IntVec3(-1, 0, 1)) && thing.def.category == ThingCategory.Building && thing.def.passability == Traversability.Impassable) 
                        excludedThings.Add(thing);
                    
                    // TODO ADD
                    // Exclude anything that is standable, but only if it is a building (and not a door) (any thing that doesn't have a traversability will come back as being Standable)
                    if (thing.def.category == ThingCategory.Building && thing.def.passability == Traversability.Standable && !thing.def.IsDoor)
                        excludedThings.Add(thing);
                        
                    //TODO ADD
                    // Mark for destroying metals that don't use hitpoints
                    if (thing.def.IsMetal && !thing.def.useHitPoints) 
                        destroySpecial.Add(thing.def);
                }
            }*/

            foreach (IntVec3 pos in vortexPattern)
            {
                DamageDef damType = DefDatabase<DamageDef>.GetNamed("StargateMod_KawooshExplosion");

                Explosion explosion = (Explosion)GenSpawn.Spawn(ThingDefOf.Explosion, parent.Position, parent.Map);
                explosion.damageFalloff = false;
                explosion.damAmount = damType.defaultDamage;
                explosion.Position = parent.Position + pos;
                explosion.radius = 0.5f;
                explosion.damType = damType;
                explosion.StartExplosion(null, excludedThings);

                //TODO ADD
                //
                // Destroy things (Metals) that were marked for destroying
                /*foreach (Thing thing in parent.Map.thingGrid.ThingsAt(parent.Position + pos))
                {
                    foreach (ThingDef toDestroy in destroySpecial)
                    {
                        if (thing.def.defName == toDestroy.defName && !thing.DestroyedOrNull()) thing.Destroy();
                    }
                }*/
            }
        }

        private void InitGate()
        {
            bool isHibernatingAlready = IsHibernating;
            
            if (GetStargateOnMap(parent.Map, parent) == null)
            {
                if (parent.Map.IsPocketMap)
                {
                    if (parent.Map.PocketMapParent.sourceMap != null && GetStargateOnMap(parent.Map.PocketMapParent.sourceMap) != null)
                    {
                        if (isHibernatingAlready) Messages.Message("CannotWake_SourceMap".Translate(), MessageTypeDefOf.RejectInput);
                        else Messages.Message("GateHibernating_SourceMap".Translate(), MessageTypeDefOf.CautionInput);
                        if (_conflictingGate == null) _conflictingGate = GetStargateOnMap(parent.Map.PocketMapParent.sourceMap);
                        IsHibernating = true;
                    }
                    else
                    {
                        PocketMapGateAddress = parent.Map.Index;
                        Find.World.GetComponent<WorldComp_StargateAddresses>().AddPocketMapAddress(PocketMapGateAddress);
                        IsInPocketMap = true;
                        IsHibernating = false;
                        _conflictingGate = null;
                    }

                }
                else
                {
                    GateAddress = parent.Map.Tile;
                    Find.World.GetComponent<WorldComp_StargateAddresses>().AddAddress(GateAddress);
                    IsHibernating = false;
                    _conflictingGate = null;
                }
            }
            else
            {
                if (isHibernatingAlready) Messages.Message("CannotWake".Translate(), MessageTypeDefOf.RejectInput);
                else Messages.Message("GateHibernating".Translate(), MessageTypeDefOf.CautionInput);
                if (_conflictingGate == null) _conflictingGate = GetStargateOnMap(parent.Map, parent);
                IsHibernating = true;
            }
            
            if (isHibernatingAlready && !IsHibernating) SGSoundDefOf.StargateMod_Steam.PlayOneShot(SoundInfo.InMap(parent));
        }

        private void GateDialTick()
        {
            if (!_settings.ShortenGateDialSeq)
            {
                if (TicksUntilOpen == 900 || TicksUntilOpen == 600 || TicksUntilOpen == 300)
                {
                    SGSoundDefOf.StargateMod_RingUsualStart.PlayOneShot(SoundInfo.InMap(parent));
                    _prevRingSoundQueue = TicksUntilOpen;
                }

                if (TicksUntilOpen == _prevRingSoundQueue - 240 && _chevronSoundCounter < 3)
                {
                    DefDatabase<SoundDef>.GetNamed($"StargateMod_ChevUsual_{_chevronSoundCounter + 1}")
                        .PlayOneShot(SoundInfo.InMap(parent));
                    _chevronSoundCounter++;
                }
            }
            else if (TicksUntilOpen == 200)
                SGSoundDefOf.StargateMod_RingUsualStart.PlayOneShot(SoundInfo.InMap(parent));

            TicksUntilOpen--;
            if (TicksUntilOpen == 0)
            {
                TicksUntilOpen = -1;
                if (_queuedAddress <= -1) OpenStargate(_queuedAddressPocketMap);
                else OpenStargate(_queuedAddress);
                    
                _queuedAddress = -1;
                _queuedAddressPocketMap = -1;

                _checkVortexPawnsTick = -1;
                
                _prevRingSoundQueue = 0;
                _chevronSoundCounter = 0;
            }
        }
        private void CheckVortexPawns()
        {
            if (_pawnsWatchingStargate == null) _pawnsWatchingStargate = new List<Pawn>();
            var radialCenter = parent.Position + new IntVec3(0, 0, -1).RotatedBy(parent.Rotation);
            var cells = GenRadial.RadialCellsAround(radialCenter, 5, true).ToList();
            Map map = parent.Map;
            
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: Checking on pawns in stargate danger zone.. (on TicksUntilOpen {TicksUntilOpen})");
            if (Prefs.LogVerbose) Log.Message($"StargatesMod: check radius center cell = {radialCenter}");
            
            foreach (IntVec3 pos in cells)
            {
                foreach (Thing thing in map.thingGrid.ThingsAt(pos))
                {
                    Pawn pawn = thing as Pawn;
                    if (pawn == null || _pawnsWatchingStargate.Contains(pawn) || pawn.Drafted) continue;
                    
                    Room pawnRoom = pawn.Position.GetRoom(pawn.Map);
                    var pawnCells = GenRadial.RadialCellsAround(pawn.Position, 3, true).Where(c => c.InBounds(map) && c.Walkable(map) && c.GetRoom(map) == pawnRoom && !VortexCells.Contains(c)).ToList();
                    if (!pawnCells.Any()) continue;
                    
                    pawn.jobs.StopAll();
                    pawn.pather.StopDead();
                    var destPos = pawnCells.RandomElement();
                    if (Prefs.LogVerbose) Log.Message($"StargatesMod: Directing {pawn} away from vortex to position {destPos}");
                    pawn.jobs.ClearQueuedJobs();
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_WatchStargate"), parent, destPos);
                    pawn.jobs.StartJob(job);
                    _pawnsWatchingStargate.Add(pawn);
                }
            }
        }

        private void EndStargateWatching()
        {
            if (!_pawnsWatchingStargate.Any()) return;
            foreach (Pawn pawn in _pawnsWatchingStargate.ToList())
            {
                if (pawn.CurJob.def == DefDatabase<JobDef>.GetNamed("StargateMod_WatchStargate")) 
                    pawn.jobs.StopAll();
                _pawnsWatchingStargate.Remove(pawn);
            }
        }
        
        private void WormholeContentDisposal(bool isRecvBuffer)
        {
            Thing thingToDestroy = isRecvBuffer ? _recvBuffer[0] : _sendBuffer[0];
            if (thingToDestroy is Pawn pawn)
            {
                // Remove death refusal hediff (if present) before killing pawn, to avoid error.
                foreach (var hediff in pawn.health.hediffSet.hediffs.ToList().Where(hediff => hediff.def.defName == "DeathRefusal"))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
            
            DamageInfo disintDeathInfo = new DamageInfo(DefDatabase<DamageDef>.GetNamed("StargateMod_DisintegrationDeath"), 99999f, 999f);

            if (!thingToDestroy.DestroyedOrNull()) thingToDestroy.Kill(disintDeathInfo);
            
            if (!isRecvBuffer) _sendBuffer.Remove(thingToDestroy);
            else
            {
                _recvBuffer.Remove(thingToDestroy);
                SGSoundDefOf.StargateMod_IrisHit.PlayOneShot(SoundInfo.InMap(parent));
            }
        }
        
        public void AddToSendBuffer(Thing thing)
        {
            _sendBuffer.Add(thing);
            PlayTeleportSound();
        }

        public void AddToReceiveBuffer(Thing thing)
        {
            _recvBuffer.Add(thing);
        }

        #region Comp Overrides

        public override void PostDraw()
        {
            base.PostDraw();
            if (IrisIsActivated)
                StargateIris.Draw(
                    parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.01f),
                    parent.Rotation, parent);

            if (StargateIsActive)
                StargatePuddle.Draw(
                    parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.02f),
                    parent.Rotation, parent);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (TicksUntilOpen > 0)
            {
                // Make pawns avoid vortex ?
                if (!IrisIsActivated)
                {
                    if (_checkVortexPawnsTick < 0) _checkVortexPawnsTick = TicksUntilOpen;
                    if (TicksUntilOpen == _checkVortexPawnsTick)
                    {
                        CheckVortexPawns();
                        _checkVortexPawnsTick -= _checkVortexPawnsDelayTick;
                        if (_checkVortexPawnsTick < 0) _checkVortexPawnsTick = 0;
                    }
                }
                
                GateDialTick();
            }

            if (!StargateIsActive) return;
            if (!IrisIsActivated && TicksSinceOpened < 150 && TicksSinceOpened % 10 == 0)
                DoUnstableVortex();

            if (parent.Fogged()) FloodFillerFog.FloodUnfog(parent.Position, parent.Map);

            if (_pawnsWatchingStargate.Any()) EndStargateWatching();
            
            CompStargate sgComp = _connectedStargate.TryGetComp<CompStargate>();
            CompTransporter transComp = parent.GetComp<CompTransporter>();
            
            if (transComp != null)
            {
                Thing transportThing = transComp.innerContainer.FirstOrFallback();
                if (transportThing != null)
                {
                    if (transportThing.Spawned) transportThing.DeSpawn();
                    AddToSendBuffer(transportThing);
                    transComp.innerContainer.Remove(transportThing);
                }
                else if (transComp.LoadingInProgressOrReadyToLaunch && !transComp.AnyInGroupHasAnythingLeftToLoad)
                    transComp.CancelLoad();
            }

            if (_sendBuffer.Any())
            {
                if (!IsReceivingGate)
                {
                    sgComp.AddToReceiveBuffer(_sendBuffer[0]);
                    _sendBuffer.Remove(_sendBuffer[0]);
                }
                else WormholeContentDisposal(false);
            }

            if (_recvBuffer.Any() && TicksSinceBufferUnloaded > Rand.Range(10, 80))
            {
                TicksSinceBufferUnloaded = 0;
                if (!IrisIsActivated)
                {
                    GenSpawn.Spawn(_recvBuffer[0], parent.InteractionCell, parent.Map);
                    _recvBuffer.Remove(_recvBuffer[0]);
                    PlayTeleportSound();
                }
                else WormholeContentDisposal(true);

                if (sgComp.IsInPocketMap)
                {
                    if (_connectedAddressPocketMap == -1 && !_recvBuffer.Any())
                    {
                        CloseStargate(false);
                    }
                }
                else
                {
                    if (_connectedAddress == -1 && !_recvBuffer.Any())
                    {
                        CloseStargate(false);
                    }
                }

                TicksSinceBufferUnloaded++;
                TicksSinceOpened++;
                if (IsReceivingGate && TicksSinceBufferUnloaded > 2500 && !_connectedStargate.TryGetComp<CompStargate>().GateIsLoadingTransporter)
                    CloseStargate(true);
            }

            if (sgComp.IsInPocketMap)
            {
                if (_connectedAddressPocketMap == -1 && !_recvBuffer.Any())
                    CloseStargate(false);
            }
            else
            {
                if (_connectedAddress == -1 && !_recvBuffer.Any())
                    CloseStargate(false);
            }

            TicksSinceBufferUnloaded++;
            TicksSinceOpened++;

            if (IsReceivingGate && TicksSinceBufferUnloaded > 2500 && !_connectedStargate.TryGetComp<CompStargate>().GateIsLoadingTransporter)
                CloseStargate(true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!IsHibernating) InitGate();
            
            if (StargateIsActive)
            {
                if (IsInPocketMap)
                {
                    if (_connectedStargate == null && _connectedAddressPocketMap != -1)
                        _connectedStargate = GetStargateOnMap(Find.Maps[_connectedAddressPocketMap]);
                }
                else
                {
                    if (_connectedStargate == null && _connectedAddress != -1)
                        _connectedStargate = GetStargateOnMap(Find.WorldObjects.MapParentAt(_connectedAddress).Map);
                }
                _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            }

            //fix nullreferenceexception that happens when the innercontainer disappears for some reason, hopefully this doesn't end up causing a bug that will take hours to track down ;)
            CompTransporter transComp = parent.GetComp<CompTransporter>();
            if (transComp != null && transComp.innerContainer == null)
                transComp.innerContainer = new ThingOwner<Thing>(transComp);
            
            if (Prefs.LogVerbose) Log.Message($"StargateMod: compsg postspawnssetup: sgactive={StargateIsActive} connectgate={_connectedStargate} connectaddress={_connectedAddress}, mapparent={parent.Map.Parent}");
        }

        public string GetInspectString()
        {
            WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
            
            string displayedAddress = GetStargateDesignation(GateAddress);
            if (IsInPocketMap) displayedAddress = "PM-" + addressComp.PocketMapAddressList.IndexOf(PocketMapGateAddress);
            
            string connectedDisplayAddress;
            if (_connectedAddress > -1)
                connectedDisplayAddress = "" + GetStargateDesignation(_connectedAddress);
            else connectedDisplayAddress = "PM-" + addressComp.PocketMapAddressList.IndexOf(_connectedAddressPocketMap);
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(!IsHibernating
                ? "GateAddress".Translate(displayedAddress)
                : "GateHibernating".Translate());
            if (!StargateIsActive && TicksUntilOpen <= -1)
                sb.AppendLine("InactiveFacility".Translate().CapitalizeFirst());
            if (StargateIsActive)
                sb.AppendLine("SGM.ConnectedToGate".Translate(connectedDisplayAddress,
                    (IsReceivingGate ? "SGM.Incoming" : "SGM.Outgoing").Translate()));

            if (HasIris) sb.AppendLine("SGM.IrisStatus".Translate((IrisIsActivated ? "SGM.IrisClosed" : "SGM.IrisOpen").Translate()));
            if (TicksUntilOpen > 0) sb.AppendLine("SGM.TimeUntilGateLock".Translate(TicksUntilOpen.ToStringTicksToPeriod()));
            
            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;

            // Gizmo to select connected gate
            if (StargateIsActive && (_connectedAddress > -1 || _connectedAddressPocketMap > -1))
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "SGM.SelectConnectedGate".Translate(),
                    defaultDesc = "SGM.SelectConnectedGateDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/SelectStargate"),
                    action = delegate
                    {
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(_connectedStargate));
                    }
                };
                yield return command;
            }
            
            if (Props.canHaveIris && HasIris)
            {
                Command_Action irisControl = new Command_Action
                {
                    defaultLabel = "SGM.OpenCloseIris".Translate(),
                    defaultDesc = "SGM.OpenCloseIrisDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get(Props.irisTexture),
                    action = delegate
                    {
                        ChangeIrisState();
                    }
                };
                yield return irisControl;
            }
            
            // Remotely open Iris of gate on the other side if closed
            if (!IsReceivingGate && Faction.OfPlayer.def.techLevel >= TechLevel.Industrial)
            {
                CompStargate connectedSGComp = _connectedStargate.TryGetComp<CompStargate>();
                
                if (connectedSGComp != null && _connectedStargate.Faction == Faction.OfPlayer
                    && connectedSGComp.Props.canHaveIris && connectedSGComp.HasIris)
                {
                    Command_Action command = new Command_Action
                    {
                        defaultLabel = "SGM.TransmitGDO".Translate(),
                        defaultDesc = "SGM.TransmitGDODesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmos/StargateTransmitGDO"),
                        action = delegate
                        {
                            CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(_connectedStargate));
                            connectedSGComp.ChangeIrisState();
                        }
                    };
                    if (!connectedSGComp.IrisIsActivated) command.Disable("SGM.CannotGDO".Translate());
                    yield return command;
                }
            }
            
            // Gizmo to activate gate if hibernating
            if (IsHibernating)
            {
                Command_Action command = new Command_Action
                {
                    defaultLabel = "SGM.WakeHibernation".Translate(),
                    defaultDesc = "SGM.WakeHibernationDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/StargateUnHibernate"),
                    action = InitGate
                };
                yield return command;

                if (_conflictingGate != null)
                {
                    if (GetStargateOnMap(parent.Map, parent) == null)
                    {
                        _conflictingGate = null;
                    }
                    else
                    {
                        Command_Action command2 = new Command_Action
                        {
                            defaultLabel = "SelectGateConflict".Translate(),
                            defaultDesc = "SelectGateConflictDesc".Translate(),
                            icon = ContentFinder<Texture2D>.Get("UI/Gizmos/SelectStargate"),
                            action = delegate
                            {
                                CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(_conflictingGate));
                            }
                        };
                        yield return command2;
                    }
                }
            }
            
            if (!Prefs.DevMode) yield break;
            
            Command_Action devForceClose = new Command_Action
            {
                defaultLabel = "Force close",
                defaultDesc = "Force close this gate to hopefully remove strange behaviours (this will not close gate at the other end).",
                action = delegate
                {
                    CloseStargate(false);
                    Log.Message($"Stargate {parent.ThingID} was force-closed.");
                }
            };
            yield return devForceClose;
            
            if (!Props.canHaveIris) yield break;
            Command_Action devAddRemoveIris = new Command_Action
            {
                defaultLabel = "Add/remove iris",
                action = delegate { HasIris = !HasIris; }
            };
            yield return devAddRemoveIris;
        }

        private void CleanupGate()
        {
            if (_connectedStargate != null) CloseStargate(true);

            if (IsInPocketMap) Find.World.GetComponent<WorldComp_StargateAddresses>().RemovePocketMapAddress(PocketMapGateAddress);
            else Find.World.GetComponent<WorldComp_StargateAddresses>().RemoveAddress(GateAddress);
        }
        
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
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
            Scribe_Values.Look(ref StargateIsActive, "StargateIsActive");
            Scribe_Values.Look(ref IsReceivingGate, "IsReceivingGate");
            Scribe_Values.Look(ref IsInPocketMap, "IsInPocketMap");
            Scribe_Values.Look(ref HasIris, "HasIris");
            Scribe_Values.Look(ref IrisIsActivated, "IrisIsActivated");
            Scribe_Values.Look(ref TicksSinceOpened, "TicksSinceOpened");
            Scribe_Values.Look(ref IsHibernating, "IsHibernating");
            Scribe_Values.Look(ref _connectedAddress, "_connectedAddress");
            Scribe_Values.Look(ref _connectedAddressPocketMap, "_connectedAddressPocketMap");
            Scribe_References.Look(ref _connectedStargate, "_connectedStargate");
            Scribe_Collections.Look(ref _recvBuffer, "_recvBuffer", LookMode.GlobalTargetInfo);
            Scribe_Collections.Look(ref _sendBuffer, "_sendBuffer", LookMode.GlobalTargetInfo);
        }

        public override string CompInspectStringExtra()
        {
            return base.CompInspectStringExtra() + "SGM.RespawnGateString".Translate();
        }

        #endregion
    }

    public class CompProperties_Stargate : CompProperties
    {
        public CompProperties_Stargate()
        {
            compClass = typeof(CompStargate);
        }

        public bool canHaveIris = true;
        public bool explodeOnUse = false;
        public string puddleTexture;
        public string irisTexture;
        public Vector2 puddleDrawSize;

        public List<IntVec3> vortexPattern_north = new List<IntVec3>
        {
            new IntVec3(0, 0, 1),
            new IntVec3(1, 0, 1),
            new IntVec3(-1, 0, 1),
            new IntVec3(0, 0, 0),
            new IntVec3(1, 0, 0),
            new IntVec3(-1, 0, 0),
            new IntVec3(0, 0, -1),
            new IntVec3(1, 0, -1),
            new IntVec3(-1, 0, -1),
            new IntVec3(0, 0, -2),
            new IntVec3(1, 0, -2),
            new IntVec3(-1, 0, -2),
            new IntVec3(0, 0, -3)
        };
        
        public List<IntVec3> vortexPattern_south = new List<IntVec3>
        {
            new IntVec3(0, 0, -1),
            new IntVec3(1, 0, -1),
            new IntVec3(-1, 0, -1),
            new IntVec3(0, 0, 0),
            new IntVec3(1, 0, 0),
            new IntVec3(-1, 0, 0),
            new IntVec3(0, 0, 1),
            new IntVec3(1, 0, 1),
            new IntVec3(-1, 0, 1),
            new IntVec3(0, 0, 2),
            new IntVec3(1, 0, 2),
            new IntVec3(-1, 0, 2),
            new IntVec3(0, 0, 3)
        };
        
        public List<IntVec3> vortexPattern_east = new List<IntVec3>
        {
            new IntVec3(1, 0, 0),
            new IntVec3(1, 0, 1),
            new IntVec3(1, 0, -1),
            
            new IntVec3(0, 0, 0),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, -1),
            
            new IntVec3(-1, 0, 0),
            new IntVec3(-1, 0, 1),
            new IntVec3(-1, 0, -1),
            
            new IntVec3(-2, 0, 0),
            new IntVec3(-2, 0, 1), 
            new IntVec3(-2, 0, -1),
            
            new IntVec3(-3, 0, 0)
        };
        
        public List<IntVec3> vortexPattern_west = new List<IntVec3>
        {
            new IntVec3(-1, 0, 0),
            new IntVec3(-1, 0, 1),
            new IntVec3(-1, 0, -1),
            
            new IntVec3(0, 0, 0),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, -1),
            
            new IntVec3(1, 0, 0),
            new IntVec3(1, 0, 1),
            new IntVec3(1, 0, -1),
            
            new IntVec3(2, 0, 0),
            new IntVec3(2, 0, 1), 
            new IntVec3(2, 0, -1),
            
            new IntVec3(3, 0, 0)
        };

    }
}

