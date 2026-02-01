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
        int _connectedAddressPocketMap = -1;
        Thing _connectedStargate;

        private int _checkVortexPawnsTick = 120;
        private const int _checkVortexPawnsDelayTick = 10;
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
                foreach (IntVec3 offset in Props.vortexPattern) yield return parent.Position + offset.RotatedBy(parent.Rotation);
            }
        }

        #region DHD Controls

        public void OpenStargateDelayed(PlanetTile address, int delay, bool isPocketMapAddress = false)
        {
            if (!isPocketMapAddress)
            {
                _queuedAddress = address;
                TicksUntilOpen = delay;
            }
            else
            {
                _queuedAddressPocketMap = address;
                TicksUntilOpen = delay;
            }

            _checkVortexPawnsTick = 120;
        }
        
        /*public void OpenStargateDelayed(int mapIndex, int delay) //Pocket Map
        {
            _queuedAddressPocketMap = mapIndex;
            TicksUntilOpen = delay;
            
            _checkVortexPawnsTick = 120;
        }*/

        public void OpenStargate(PlanetTile address)
        {
             if (_queuedAddressPocketMap > -1)
             {
                 Log.Error($"[StargatesMod] Tried opening stargate with regular address but a pocketmap address was queued");
                 _queuedAddress = -1;
                 _queuedAddressPocketMap = -1;
                 return;
             }

             if (_queuedAddress == -1 && _queuedAddressPocketMap == -1)
             {
                 IsReceivingGate = true;
                 TicksSinceBufferUnloaded = -150;
                 StargateIsActive = true;
             }
             
             MapParent connectedMapParent = Find.WorldObjects.MapParentAt(address);
             if (connectedMapParent == null)
             {
                 Log.Error($"[StargatesMod] Failed to find MapParent at address {address}");
                 DialFail();
                 return;
             }
             
             if (!connectedMapParent.HasMap)
             {
                 if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] generating map for {connectedMapParent}");
                
                LongEventHandler.QueueLongEvent(delegate
                {
                    GetOrGenerateMapUtility.GetOrGenerateMap(connectedMapParent.Tile, connectedMapParent is WorldObject_PermSGSite ? new IntVec3(75, 1, 75) : Find.World.info.initialMapSize, connectedMapParent.def);
                }, "SGM.GeneratingStargateSite", doAsynchronously: false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap, callback: delegate
                {
                    if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] finished generating map");

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
                Log.Error($"[StargatesMod] Tried opening stargate with pocketmap address but a regular address was queued");
                _queuedAddress = -1;
                _queuedAddressPocketMap = -1;
                return;
            }
            
            if (_queuedAddressPocketMap > -1)
            {
                PocketMapParent connectedMap = Find.Maps.ElementAt(mapIndex).PocketMapParent;
                if (connectedMap == null || !connectedMap.HasMap)
                {
                    Log.Error($"[StargatesMod] Failed to find pocketMap at mapIndex {mapIndex}");
                    DialFail();
                    return;
                }

                
                Thing connectedGate = GetStargateOnMap(connectedMap.Map);

                if (connectedGate == null || connectedGate.TryGetComp<CompStargate>().StargateIsActive)
                {
                    if (connectedGate == null) Log.Error($"StargatesMod] Failed to find target stargate");
                    else if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] failed to dial stargate; target stargate was already active");
                    DialFail();
                    return;
                }
                StargateIsActive = true;
                
                _connectedAddressPocketMap = mapIndex;

                _connectedStargate = connectedGate;
                CompStargate connectedSgComp = _connectedStargate.TryGetComp<CompStargate>();
                connectedSgComp.StargateIsActive = true;
                connectedSgComp.IsReceivingGate = true;
            
                if (IsInPocketMap) connectedSgComp._connectedAddressPocketMap = PocketMapGateAddress;
                else connectedSgComp._connectedAddress = GateAddress;
                connectedSgComp._connectedStargate = parent;

                connectedSgComp._puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(connectedSgComp.parent));
                SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(connectedSgComp.parent));

                CompGlower otherGlowComp = connectedSgComp.parent.GetComp<CompGlower>();
                otherGlowComp.Props.glowRadius = glowRadius;
                otherGlowComp.PostSpawnSetup(false);
            }
            else if (_queuedAddressPocketMap == -1)
            {
                _connectedAddressPocketMap = mapIndex;
                IsReceivingGate = true;
                TicksSinceBufferUnloaded = -150;
                StargateIsActive = true;
            }
            else
            {
                Log.Error($"[StargatesMod] queued pocketmap address was invalid: {_queuedAddressPocketMap}");
                DialFail();
                return;
            }
            
            _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(parent));

            CompGlower glowComp = parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = glowRadius;
            glowComp.PostSpawnSetup(false);
            if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] finished opening gate {parent}");
        }


        private void FinishDiallingStargate(PlanetTile address, MapParent connectedMapParent)
        {
            Thing connectedGate = null;

            if (address > -1)
            {
                connectedGate = GetStargateOnMap(connectedMapParent.Map);
                
                if (connectedGate == null || connectedGate.TryGetComp<CompStargate>().StargateIsActive)
                {
                    if (connectedGate == null) Log.Error($"StargatesMod] Failed to find target stargate");
                    else if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] failed to dial stargate; target stargate was already active");
                    DialFail();
                    return;
                }
            }
            StargateIsActive = true;
            _connectedAddress = address;

            if (_connectedAddress > -1)
            {
                _connectedStargate = connectedGate;
                CompStargate connectedSgComp = _connectedStargate.TryGetComp<CompStargate>();
                connectedSgComp.StargateIsActive = true;
                connectedSgComp.IsReceivingGate = true;
                connectedSgComp._connectedAddress = GateAddress;
                connectedSgComp._connectedStargate = parent;

                connectedSgComp._puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(connectedSgComp.parent));
                SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(connectedSgComp.parent));

                CompGlower otherGlowComp = connectedSgComp.parent.GetComp<CompGlower>();
                otherGlowComp.Props.glowRadius = glowRadius;
                otherGlowComp.PostSpawnSetup(false);
            }
            
            _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            SGSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(parent));

            CompGlower glowComp = parent.GetComp<CompGlower>();
            glowComp.Props.glowRadius = glowRadius;
            glowComp.PostSpawnSetup(false);
            if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] finished opening gate {parent}");
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
            List<IntVec3> vortexPattern = VortexCells.ToList();
            
            // exclude walls directly behind gate (e.g. walls of enclosed rooms of orbital complexes)
            excludedThings.AddRange(from pos in vortexPattern
                from thing in parent.Map.thingGrid.ThingsAt(pos)
                where (pos - parent.Position == new IntVec3(0, 0, 1) || pos - parent.Position == new IntVec3(1, 0, 1) || pos - parent.Position== new IntVec3(-1, 0, 1)) 
                      && thing.def.category == ThingCategory.Building && thing.def.passability == Traversability.Impassable 
                select thing);
            
            excludedThings.AddRange(from pos in vortexPattern
                from thing in parent.Map.thingGrid.ThingsAt(pos)
                where thing.def.category == ThingCategory.Building &&
                      thing.def.passability == Traversability.Standable && !thing.def.IsDoor
                select thing);
            
            List<Thing> destroySpecial = new List<Thing>();
            destroySpecial.AddRange(from pos in vortexPattern
                from thing in parent.Map.thingGrid.ThingsAt(pos)
                where thing.def.IsMetal && !thing.def.useHitPoints
                    select thing);

            foreach (IntVec3 pos in vortexPattern)
            {
                DamageDef damType = DefDatabase<DamageDef>.GetNamed("StargateMod_KawooshExplosion");

                Explosion explosion = (Explosion)GenSpawn.Spawn(ThingDefOf.Explosion, parent.Position, parent.Map);
                explosion.damageFalloff = false;
                explosion.damAmount = damType.defaultDamage;
                explosion.Position = pos;
                explosion.radius = 0.5f;
                explosion.damType = damType;
                explosion.StartExplosion(null, excludedThings);
                
                
                foreach (var thing in destroySpecial)
                {
                    if(Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] destroying specialThing {thing}");
                    thing.Destroy();
                }
                destroySpecial.Clear();
            }
        }

        private void InitGate()
        {
            bool isHibernatingAlready = IsHibernating;
            bool mapHasStargate = GetStargateOnMap(parent.Map, parent) != null;

            bool mapHasChildMap = parent.Map.ChildPocketMaps.Any();
            List<Map> childMapsWithStargate = new List <Map>();
            childMapsWithStargate.AddRange(from map in parent.Map.ChildPocketMaps where GetStargateOnMap(map) != null select map);
            bool childMapHasStargate = mapHasChildMap && childMapsWithStargate.Any();
            
            bool mapIsPocketMap = parent.Map.IsPocketMap;
            bool pocketMapHasSourceMap = mapIsPocketMap && parent.Map.PocketMapParent.sourceMap != null;
            bool sourceMapHasStargate = pocketMapHasSourceMap && GetStargateOnMap(parent.Map.PocketMapParent.sourceMap) != null;

            
            if (mapHasStargate || childMapHasStargate || sourceMapHasStargate)
            {
                string cannotWakeMessage = "SGM.CannotWake";
                string gateHibernatingMessage = "SGM.GateHibernating";

                if (childMapHasStargate)
                {
                    cannotWakeMessage += "_ChildMap";
                    gateHibernatingMessage += "_ChildMap";
                    
                    _conflictingGate = GetStargateOnMap(childMapsWithStargate.First());
                }
                else if (sourceMapHasStargate)
                {
                    cannotWakeMessage += "_SourceMap";
                    gateHibernatingMessage += "_SourceMap";
                    
                    _conflictingGate = GetStargateOnMap(parent.Map.PocketMapParent.sourceMap);
                }
                else _conflictingGate = GetStargateOnMap(parent.Map, parent);
                
                if (isHibernatingAlready) Messages.Message(cannotWakeMessage.Translate(), MessageTypeDefOf.RejectInput);
                else Messages.Message(gateHibernatingMessage.Translate(), MessageTypeDefOf.CautionInput);

                IsHibernating = true;
            }
            else
            {
                var addressWorldComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
                
                IsInPocketMap = mapIsPocketMap;
                if (IsInPocketMap)
                {
                    PocketMapGateAddress = parent.Map.Index;
                    addressWorldComp.AddPocketMapAddress(PocketMapGateAddress);
                }
                else
                {
                    GateAddress = parent.Map.Tile;
                    addressWorldComp.AddAddress(GateAddress);
                }
                IsHibernating = false;
                _conflictingGate = null;
                
                
                if (isHibernatingAlready) SGSoundDefOf.StargateMod_Steam.PlayOneShot(SoundInfo.InMap(parent));
            }
        }

        private void GateDialTick()
        {
            if (!_settings.ShortenGateDialSeq)
            {
                switch (TicksUntilOpen)
                {
                    case 900:
                    case 600:
                    case 300:
                        SGSoundDefOf.StargateMod_RingUsualStart.PlayOneShot(SoundInfo.InMap(parent));
                        _prevRingSoundQueue = TicksUntilOpen;
                        break;
                }

                if (TicksUntilOpen == _prevRingSoundQueue - 240 && _chevronSoundCounter < 3)
                {
                    DefDatabase<SoundDef>.GetNamed($"StargateMod_ChevUsual_{_chevronSoundCounter + 1}").PlayOneShot(SoundInfo.InMap(parent));
                    _chevronSoundCounter++;
                }
            }
            else if (TicksUntilOpen == 200)
                SGSoundDefOf.StargateMod_RingUsualStart.PlayOneShot(SoundInfo.InMap(parent));

            TicksUntilOpen--;
            
            if (TicksUntilOpen != 0) return;
            
            TicksUntilOpen = -1;
            
            if (_queuedAddress <= -1) OpenStargate(_queuedAddressPocketMap);
            else OpenStargate(_queuedAddress);
                    
            _queuedAddress = -1;
            _queuedAddressPocketMap = -1;

            _checkVortexPawnsTick = -1;
                
            _prevRingSoundQueue = 0;
            _chevronSoundCounter = 0;
        }
        
        private void CheckVortexPawns()
        {
            if (_pawnsWatchingStargate == null) _pawnsWatchingStargate = new List<Pawn>();
            var radialCenter = parent.Position + new IntVec3(0, 0, -1).RotatedBy(parent.Rotation);
            var cells = GenRadial.RadialCellsAround(radialCenter, 4, true).ToList();
            Map map = parent.Map;
            
            if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] Checking on pawns in stargate danger zone.. (on TicksUntilOpen {TicksUntilOpen})");
            if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] check radius center cell = {radialCenter}");
            
            foreach (var thing in cells.SelectMany(pos => map.thingGrid.ThingsAt(pos)).Where(thing => thing is Pawn pawn && !pawn.DeadOrDowned && !pawn.Drafted && _pawnsWatchingStargate.Contains(pawn)))
            {
                if (!(thing is Pawn pawn)) continue;
                
                Room pawnRoom = pawn.Position.GetRoom(pawn.Map);
                var pawnCells = GenRadial.RadialCellsAround(pawn.Position, 4, true).Where(c => c.InBounds(map) && c.Walkable(map) && c.GetRoom(map) == pawnRoom && !VortexCells.Contains(c)).ToList();
                if (!pawnCells.Any()) continue;
                    
                pawn.jobs.StopAll();
                pawn.pather.StopDead();
                var destPos = pawnCells.RandomElement();
                if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] Directing {pawn} away from vortex to position {destPos}");
                pawn.jobs.ClearQueuedJobs();
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_WatchStargate"), parent, destPos);
                pawn.jobs.StartJob(job);
                _pawnsWatchingStargate.Add(pawn);
            }
        }

        private void EndStargateWatching()
        {
            if (!_pawnsWatchingStargate.Any()) return;
            foreach (var pawn in _pawnsWatchingStargate.ToList().Where(pawn => !pawn.DeadOrDowned && !pawn.Drafted && pawn.CurJob.def == DefDatabase<JobDef>.GetNamed("StargateMod_WatchStargate")))
            {
                pawn.jobs.StopAll();
            }
            _pawnsWatchingStargate.Clear();
        }
        
        private void WormholeContentDisposal(bool isRecvBuffer)
        {
            Thing thingToDestroy = isRecvBuffer ? _recvBuffer[0] : _sendBuffer[0];
            
            //Remove deathRefusal hediff to avoid error
            if (thingToDestroy is Pawn pawn && pawn.health.hediffSet.HasHediff(HediffDefOf.DeathRefusal))
                pawn.health.RemoveHediff(pawn.health.hediffSet.hediffs.Find(hediff  => hediff is Hediff_DeathRefusal));
            
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
                StargateIris.Draw(parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.01f),
                    parent.Rotation, parent);

            if (StargateIsActive)
                StargatePuddle.Draw(parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.BuildingOnTop) - (Vector3.one * 0.02f),
                    parent.Rotation, parent);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (TicksUntilOpen > 0) 
                GateDialTick();
            
            //TODO Test
            if (!StargateIsActive && !IrisIsActivated)
            {
                if (TicksUntilOpen > -1 && _checkVortexPawnsTick < 0) _checkVortexPawnsTick = _checkVortexPawnsDelayTick;
                if (TicksUntilOpen == _checkVortexPawnsTick)
                {
                    CheckVortexPawns();
                    _checkVortexPawnsTick = TicksUntilOpen - _checkVortexPawnsDelayTick;
                }
            }

            if (!StargateIsActive) return;
            
            if (!IrisIsActivated && TicksSinceOpened < 150 && TicksSinceOpened % 10 == 0)
                DoUnstableVortex();
            
            if (IsReceivingGate && TicksSinceOpened < 60 && parent.Fogged()) FloodFillerFog.FloodUnfog(parent.Position, parent.Map);
            
            if (_pawnsWatchingStargate != null && _pawnsWatchingStargate.Any() && TicksSinceOpened == 210 ) EndStargateWatching();
            
            
            CompStargate connectedStargateComp = _connectedStargate.TryGetComp<CompStargate>();
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
                    connectedStargateComp.AddToReceiveBuffer(_sendBuffer[0]);
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
            }

            TicksSinceBufferUnloaded++;
            TicksSinceOpened++;
            
            if (_connectedAddress == -1 && _connectedAddressPocketMap == -1  && !_recvBuffer.Any())
                CloseStargate(false);
            

            if (IsReceivingGate && TicksSinceBufferUnloaded > 2500 && !connectedStargateComp.GateIsLoadingTransporter)
                CloseStargate(true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (!IsHibernating) InitGate();
            
            if (StargateIsActive)
            {
                bool connAddress = _connectedAddress != -1;
                bool pocketConnAddress = _connectedAddressPocketMap != -1;
                if (_connectedStargate == null && (connAddress || pocketConnAddress))
                    _connectedStargate = GetStargateOnMap(connAddress ? Find.WorldObjects.MapParentAt(_connectedAddress).Map : Find.Maps[_connectedAddressPocketMap]);
                
                _puddleSustainer = SGSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
            }

            //fix nullreferenceexception that happens when the innercontainer disappears for some reason, hopefully this doesn't end up causing a bug that will take hours to track down ;)
            CompTransporter transComp = parent.GetComp<CompTransporter>();
            if (transComp != null && transComp.innerContainer == null)
                transComp.innerContainer = new ThingOwner<Thing>(transComp);
            
            if (Prefs.LogVerbose || _settings.DebugMode) Log.Message($"[StargatesMod] compsg postspawnssetup: sgactive={StargateIsActive} connectgate={_connectedStargate} connectaddress={_connectedAddress}, mapparent={parent.Map.Parent}");
        }

        public string GetInspectString()
        {
            WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
            
            string displayedAddress = GetStargateDesignation(GateAddress);
            if (IsInPocketMap) displayedAddress = "PM-" + addressComp.PocketMapAddressList.IndexOf(PocketMapGateAddress);
            
            string connectedDisplayAddress;
            if (_connectedAddress > -1)
                connectedDisplayAddress = "" + GetStargateDesignation(_connectedAddress);
            else if (_connectedAddressPocketMap > -1)
                connectedDisplayAddress = "PM-" + addressComp.PocketMapAddressList.IndexOf(_connectedAddressPocketMap);
            else connectedDisplayAddress = "SGM.Unknown".Translate();
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(!IsHibernating
                ? "SGM.GateAddress".Translate(displayedAddress)
                : "SGM.GateHibernating".Translate());
            switch (StargateIsActive)
            {
                case false when TicksUntilOpen <= -1:
                    sb.AppendLine("SGM.StargateIdle".Translate());
                    break;
                case true:
                    sb.AppendLine("SGM.ConnectedToGate".Translate(connectedDisplayAddress,
                        (IsReceivingGate ? "SGM.Incoming" : "SGM.Outgoing").Translate()));
                    break;
            }
            if (HasIris) sb.AppendLine("SGM.IrisStatus".Translate((IrisIsActivated ? "SGM.IrisClosed" : "SGM.IrisOpen").Translate()));
            if (TicksUntilOpen > 0) sb.AppendLine("SGM.TimeUntilGateLock".Translate(TicksUntilOpen.ToStringTicksToPeriod()));

            
            if (!_settings.DebugMode) return sb.ToString().TrimEndNewlines();
            sb.AppendLine("=== DebugInfo ===");
            sb.AppendLine($"TicksSinceOpened = {TicksSinceOpened}");
            sb.AppendLine($"TicksUntilOpen = {TicksUntilOpen}");
            sb.AppendLine($"ticksSinceBufferUnloaded = {TicksSinceBufferUnloaded}");

            sb.AppendLine($"connectedAddress = {_connectedAddress}");
            sb.AppendLine($"connectedAddressPocketMap = {_connectedAddressPocketMap}");
            
            var string1 = _pawnsWatchingStargate == null ? "null" : "notNull";
            sb.AppendLine($"_PawnsWatchingStargate = {string1}");
            if (_pawnsWatchingStargate != null && _pawnsWatchingStargate.Any()) sb.AppendLine($"_PawnsWatchingStargate0 = {_pawnsWatchingStargate[0]}");

            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            
            if (StargateIsActive && (_connectedAddress > -1 || _connectedAddressPocketMap > -1))
            {
                Command_Action selectConnectedGate = new Command_Action
                {
                    defaultLabel = "SGM.SelectConnectedGate".Translate(),
                    defaultDesc = "SGM.SelectConnectedGateDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/SelectStargate"),
                    action = delegate
                    {
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(_connectedStargate));
                    }
                };
                yield return selectConnectedGate;
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
            
            
            if (!IsReceivingGate && Faction.OfPlayer.def.techLevel >= TechLevel.Industrial)
            {
                CompStargate connectedSGComp = _connectedStargate.TryGetComp<CompStargate>();
                
                if (connectedSGComp != null && _connectedStargate.Faction == Faction.OfPlayer
                                            && connectedSGComp.Props.canHaveIris && connectedSGComp.HasIris)
                {
                    Command_Action remoteIrisControl = new Command_Action
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
                    if (!connectedSGComp.IrisIsActivated) remoteIrisControl.Disable("SGM.CannotGDO".Translate());
                    yield return remoteIrisControl;
                }
            }
            
            if (IsHibernating)
            {
                Command_Action makeActiveGate = new Command_Action
                {
                    defaultLabel = "SGM.WakeHibernation".Translate(),
                    defaultDesc = "SGM.WakeHibernationDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/StargateUnHibernate"),
                    action = InitGate
                };
                yield return makeActiveGate;

                if (_conflictingGate != null)
                {
                    Command_Action selectConflictingGate = new Command_Action
                    {
                        defaultLabel = "SGM.SelectGateConflict".Translate(),
                        defaultDesc = "SGM.SelectGateConflictDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmos/SelectStargate"),
                        action = delegate
                        {
                            CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(_conflictingGate));
                        }
                    };
                    yield return selectConflictingGate;
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
                    Log.Message($"[StargatesMod] Stargate {parent.ThingID} was force-closed.");
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

        public List<IntVec3> vortexPattern = new List<IntVec3>
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
    }
}

