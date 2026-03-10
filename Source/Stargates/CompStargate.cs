using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace StargatesMod;

public class CompStargate : ThingComp
{
    const int glowRadius = 10;

    private List<BufferItem> _sendBuffer = [];
    private List<BufferItem> _recvBuffer = [];
    private int _ticksSinceBufferUnloaded;
    private int _ticksSinceOpened;
    public int TicksUntilOpen = -1;
    private DialMode _dialMode;
    public PlanetTile GateAddress;
    private string _gateDesignation;
    public bool IsInPocketMap;
    public bool StargateIsActive;
    public bool IsReceivingGate;
    public bool IsHibernating;
    public bool HasIris;
    public bool IrisIsActivated;
    private int _prevRingSoundQueue;
    private int _chevronSoundCounter;
    private PlanetTile _queuedAddress = -1;
    private PlanetTile _connectedAddress = -1;
    
    private Thing _connectedStargate;
    private CompStargate _connectedStargateComp;
    
    private CompTransporter _transComp;
    private Thing _conflictingGate;
    
    private int _checkVortexPawnsTick = 120;
    private const int _checkVortexPawnsDelayTick = 10;
    private List<Pawn> _pawnsWatchingStargate;
    
    private Sustainer _puddleSustainer;

    private readonly StargatesModSettings _modSettings = LoadedModManager.GetMod<StargatesMod>().GetSettings<StargatesModSettings>();
    
    
    public bool IsExpectingIncomingWormhole => !StargateIsActive && _connectedStargate != null && _connectedStargateComp != null && IsReceivingGate;

    private WorldComp_StargateAddresses AddressComp => field ??= Find.World.GetComponent<WorldComp_StargateAddresses>();
    
    private string GateDesignation
    {
        get => !AddressComp.IsRegistered(GateAddress) ? "INVALID" : _gateDesignation;
        set => _gateDesignation = value;
    }

    private bool GateIsLoadingTransporter => _transComp is { LoadingInProgressOrReadyToLaunch: true, AnyInGroupHasAnythingLeftToLoad: true };

    public IEnumerable<IntVec3> VortexCells => Props.vortexPattern.Select(offset => parent.Position + offset.RotatedBy(parent.Rotation));
    
    
    public CompProperties_Stargate Props => (CompProperties_Stargate)props;

    private Graphic StargatePuddle =>
        field ??= GraphicDatabase.Get<Graphic_Single>(Props.puddleTexture,
            ShaderDatabase.Mote, Props.puddleDrawSize, Color.white);

    private Graphic StargateIris =>
        field ??= GraphicDatabase.Get<Graphic_Single>(Props.irisTexture,
            ShaderDatabase.Mote, Props.puddleDrawSize, Color.white);

    #region DHD Controls
    
    public void OpenStargateDelayed(PlanetTile address, int delay, DialMode dialMode)
    {
        _queuedAddress = address;
        _dialMode = dialMode;

        TicksUntilOpen = delay;
        _checkVortexPawnsTick = 120;

        if (address <= -1) return;
        
        Map map = _dialMode switch
        {
            DialMode.Map => Find.WorldObjects.MapParentAt(address)?.Map,
            DialMode.PocketMap => Find.Maps[address.tileId].PocketMapParent.Map,
            _ => null
        };
        if (map != null) _connectedStargate = SgUtilities.GetAllStargatesOnMap(map).FirstOrFallback();
        if (_connectedStargate == null) return;
        _connectedStargateComp = _connectedStargate.TryGetComp<CompStargate>();

        if (_connectedStargateComp.StargateIsActive) return;
        
        _connectedStargateComp._connectedStargate = parent;
        _connectedStargateComp._connectedStargateComp = this;
        _connectedStargateComp.IsReceivingGate = true;

        _connectedStargateComp.TicksUntilOpen = TicksUntilOpen;
    }


    private void OpenStargate(PlanetTile address)
    {
        if (StargateIsActive) {Log.Error($"[StargatesMod] Tried to open an outgoing connection from {parent} while it was already active."); DialFail(); return;}
        
        var connectedMapParent = (MapParent)null;
        switch (_dialMode)
        {
            case DialMode.None:
                Log.Error("[StargatesMod] Dial failed: No dial mode set.");
                DialFail();
                return;
            case DialMode.IncomingRaid:
                IsReceivingGate = true;
                _ticksSinceBufferUnloaded = -150;
                break;
            case DialMode.Map:
                connectedMapParent = Find.WorldObjects.MapParentAt(address);
                break;
            case DialMode.PocketMap:
                connectedMapParent = Find.Maps.ElementAt(address.tileId).PocketMapParent;
                break;
        }
             
        if (_dialMode != DialMode.IncomingRaid && connectedMapParent == null)
        {
            Log.Error($"[StargatesMod] Failed to find MapParent at {address} with dial mode {_dialMode}");
            DialFail("SGM.GateDialFailed_NotFound");
            return;
        }
        
        if (_dialMode == DialMode.Map && connectedMapParent is { HasMap: false })
        {
            if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] generating map for {connectedMapParent}");
                
            LongEventHandler.QueueLongEvent(delegate
            {
                GetOrGenerateMapUtility.GetOrGenerateMap(connectedMapParent.Tile, connectedMapParent is WorldObject_PermSgSite ? new IntVec3(75, 1, 75) : Find.World.info.initialMapSize, connectedMapParent.def);
            }, "SGM.GeneratingStargateSite", doAsynchronously: false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap, callback: delegate
            {
                if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message("[StargatesMod] finished generating map");

                FinishDiallingStargate(address, connectedMapParent);
            }); 
        }
        else FinishDiallingStargate(address, connectedMapParent);
    }

    private void FinishDiallingStargate(PlanetTile address, MapParent connectedMapParent)
    {
        StargateIsActive = true;

        if (_dialMode != DialMode.IncomingRaid)
        {
            Thing connectedGate = SgUtilities.GetAllStargatesOnMap(connectedMapParent.Map).FirstOrFallback();
            _connectedStargateComp = connectedGate?.TryGetComp<CompStargate>();
                
            if (connectedGate == null || _connectedStargateComp == null || _connectedStargateComp.StargateIsActive || (_connectedStargateComp.TicksUntilOpen > -1 && _connectedStargateComp._connectedStargateComp != this))
            {
                string failReason = "";
                if (connectedGate == null || _connectedStargateComp == null)
                {
                    Log.Error($"[StargatesMod] Failed to connect to stargate stargate = {connectedGate}, sgComp = {_connectedStargateComp}:");
                    failReason = "SGM.GateDialFailed_NotFound";
                }
                else if (Prefs.LogVerbose || _modSettings.DebugMode)
                {
                    Log.Warning("[StargatesMod] failed to dial stargate: target stargate was already active");
                    failReason = "SGM.GateDialFailed_IsInUse";
                }
                
                DialFail(failReason);
                return;
            }
                
            _connectedStargate = connectedGate;
            
            _connectedStargateComp.StargateIsActive = true;
            _connectedStargateComp.IsReceivingGate = true;
            _connectedStargateComp._connectedStargate = parent;
            _connectedStargateComp._connectedStargateComp = this;
                
            switch (_dialMode)
            {
                case DialMode.Map:
                    _connectedAddress = address;
                    break;
                case DialMode.PocketMap:
                    _connectedAddress = address.tileId;
                    break;
            }

            _connectedStargateComp._connectedAddress = GateAddress;

            _connectedStargateComp._puddleSustainer = SgSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(_connectedStargateComp.parent));
            SgSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(_connectedStargateComp.parent));
                    
            CompGlower otherGlowComp = _connectedStargateComp.parent.GetComp<CompGlower>();
            otherGlowComp.Props.glowRadius = glowRadius;
            otherGlowComp.PostSpawnSetup(false);
        }
            
        _puddleSustainer = SgSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
        SgSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(parent));

        CompGlower glowComp = parent.GetComp<CompGlower>();
        glowComp.Props.glowRadius = glowRadius;
        glowComp.PostSpawnSetup(false);
        if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] finished opening gate {parent}");
    }
        
    public void CloseStargate(bool closeOtherGate)
    {
        _transComp?.CancelLoad();

        //clear buffers just in case
        ClearBuffers();

        CompStargate connectedGateComp = null;
        if (closeOtherGate)
        {
            connectedGateComp = _connectedStargate.TryGetComp<CompStargate>();
                
            if (_connectedStargate == null || connectedGateComp == null)
                Log.Warning($"Receiving stargate connected to stargate {parent.ThingID} didn't have CompStargate, but this stargate wanted it closed.");
            else connectedGateComp.CloseStargate(false);
        }

        SoundDef puddleCloseDef = SgSoundDefOf.StargateMod_SGClose;
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

        EndStargateWatching();
        
        ResetDialState();
    }

    private void DialFail(string failReasonKey = null)
    {
        if (!string.IsNullOrEmpty(failReasonKey)) Messages.Message(failReasonKey.Translate(), MessageTypeDefOf.NegativeEvent);
        SgSoundDefOf.StargateMod_SGFailDial.PlayOneShot(SoundInfo.InMap(parent));

        ResetDialState();
        
        //In case buffers contain anything before gate opens (like gate raids)
        ClearBuffers();
    }

    private void ResetDialState()
    {
        _dialMode = DialMode.None;
        _ticksSinceBufferUnloaded = -1;
        _ticksSinceOpened = -1;
        TicksUntilOpen = -1;
        StargateIsActive = false;
        IsReceivingGate = false;
        _queuedAddress = -1;
        _connectedAddress = -1;
        _connectedStargate = null;
        _connectedStargateComp = null;
    }
        
    #endregion

    private void PlayTeleportSound() => DefDatabase<SoundDef>.GetNamed($"StargateMod_teleport_{Rand.RangeInclusive(1, 4)}").PlayOneShot(SoundInfo.InMap(parent));

    private void ChangeIrisState(bool checkValid = false)
    {
        if (checkValid && (!Props.canHaveIris || !HasIris)) return;
        IrisIsActivated = !IrisIsActivated;
            
        if (IrisIsActivated) SgSoundDefOf.StargateMod_IrisOpen.PlayOneShot(SoundInfo.InMap(parent));
        else SgSoundDefOf.StargateMod_IrisClose.PlayOneShot(SoundInfo.InMap(parent));
    }

    private void DoUnstableVortex()
    {
        List<Thing> excludedThings = [parent];
        List<IntVec3> vortexPattern = VortexCells.ToList();
            
        excludedThings.AddRange(from pos in vortexPattern
            from thing in parent.Map.thingGrid.ThingsAt(pos)
            where thing.def.category == ThingCategory.Building &&
                  thing.def.passability == Traversability.Standable && !thing.def.IsDoor
            select thing);
            
        List<Thing> destroySpecial = [];
        destroySpecial.AddRange(from pos in vortexPattern
            from thing in parent.Map.thingGrid.ThingsAt(pos)
            where thing.def.IsMetal && !thing.def.useHitPoints
            select thing);

        foreach (IntVec3 pos in vortexPattern)
        {
            DamageDef damType = SgDamageDefOf.StargatesMod_KawooshExplosion;

            Explosion explosion = (Explosion)GenSpawn.Spawn(ThingDefOf.Explosion, parent.Position, parent.Map);
            explosion.damageFalloff = false;
            explosion.damAmount = damType.defaultDamage;
            explosion.Position = pos;
            explosion.radius = 0.5f;
            explosion.damType = damType;
            explosion.StartExplosion(null, excludedThings);
                
                
            foreach (Thing thing in destroySpecial)
            {
                thing.Destroy();
            }
            destroySpecial.Clear();
        }
    }

    private void InitGate()
    {
        bool isHibernatingAlready = IsHibernating;
            
        if (SgUtilities.GetAllStargatesOnMap(parent.Map, [parent], includeLinkedMaps: true).Any())
        {
            _conflictingGate = SgUtilities.GetAllStargatesOnMap(parent.Map, [parent], includeLinkedMaps: true).First();
                
            if (isHibernatingAlready) Messages.Message("SGM.Notif.CannotWake".Translate(), MessageTypeDefOf.RejectInput);
            else Messages.Message("SGM.Notif.GateHibernating".Translate(), MessageTypeDefOf.CautionInput);

            IsHibernating = true;
        }
        else
        {
            IsInPocketMap = parent.Map.IsPocketMap;
            if (IsInPocketMap)
            {
                GateAddress = parent.Map.Index;
                AddressComp.AddPocketMapAddress(GateAddress);
            }
            else
            {
                GateAddress = parent.Map.Tile;
                AddressComp.AddAddress(GateAddress);
            }
            IsHibernating = false;
            _conflictingGate = null;
            
            GateDesignation = SgUtilities.GetStargateDesignation(!IsInPocketMap || parent.Map.PocketMapParent.sourceMap == null ? GateAddress : parent.Map.PocketMapParent.sourceMap.Tile);

            _transComp ??= parent.GetComp<CompTransporter>();
                
            if (isHibernatingAlready) SgSoundDefOf.StargateMod_Steam.PlayOneShot(SoundInfo.InMap(parent));
        }
    }

    private void GateDialTick()
    {
        if (!_modSettings.ShortenGateDialSeq)
        {
            switch (TicksUntilOpen)
            {
                case 900:
                case 600:
                case 300:
                    SgSoundDefOf.StargateMod_RingUsualStart.PlayOneShot(SoundInfo.InMap(parent));
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
            SgSoundDefOf.StargateMod_RingUsualStart.PlayOneShot(SoundInfo.InMap(parent));
        
        
        
        TicksUntilOpen--;
            
        if (TicksUntilOpen != 0) return;
            
        TicksUntilOpen = -1;
        
        if (_dialMode != DialMode.None)
            OpenStargate(_queuedAddress);
                    
        _queuedAddress = -1;
        _checkVortexPawnsTick = -1;
        
        _prevRingSoundQueue = 0;
        _chevronSoundCounter = 0;
    }
        
    private void CheckVortexPawns()
    {
        _pawnsWatchingStargate ??= [];
        IntVec3 radialCenter = parent.Position + new IntVec3(0, 0, -1).RotatedBy(parent.Rotation);
        List<IntVec3> cells = GenRadial.RadialCellsAround(radialCenter, 6, true).ToList();
        Map map = parent.Map;
            
        if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] Checking on pawns in stargate danger zone.. (on TicksUntilOpen {TicksUntilOpen})");
        if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] check radius center cell = {radialCenter}");
            
        foreach (Thing thing in cells.SelectMany(pos => map.thingGrid.ThingsAt(pos)).Where(thing => thing is Pawn { DeadOrDowned: false, Drafted: false } pawn && pawn.Faction == Faction.OfPlayer && !_pawnsWatchingStargate.Contains(pawn)))
        {
            Pawn pawn = (Pawn)thing;
                
            Room pawnRoom = pawn.Position.GetRoom(pawn.Map);
            List<IntVec3> pawnCells = GenRadial.RadialCellsAround(pawn.Position, 6, true).Where(c => c.InBounds(map) && c.Walkable(map) && c.GetRoom(map) == pawnRoom && !VortexCells.Contains(c)).ToList();
            if (!pawnCells.Any())
            {
                Log.Warning($"[StargatesMod] Could not find any valid cells to send {pawn} to while directing them away from the vortex zone.");
                continue;
            }
                    
            pawn.jobs.StopAll();
            pawn.pather.StopDead();
            IntVec3 destPos = pawnCells.RandomElement();
            if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] Directing {pawn} away from vortex to position {destPos}");
            Job job = JobMaker.MakeJob(SgJobDefOf.StargatesMod_WatchStargate, parent, destPos);
            pawn.jobs.StartJob(job, resumeCurJobAfterwards: true, canReturnCurJobToPool: true, keepCarryingThingOverride: true);
            _pawnsWatchingStargate.Add(pawn);
        }
    }

    private void EndStargateWatching()
    {
        if (_pawnsWatchingStargate.NullOrEmpty()) return;
        
        foreach (Pawn pawn in _pawnsWatchingStargate.ToList().Where(pawn => !pawn.DeadOrDowned && !pawn.Drafted && pawn.CurJob.def == SgJobDefOf.StargatesMod_WatchStargate))
            pawn.jobs.StopAll();
        
        _pawnsWatchingStargate.Clear();
    }
        
    private void WormholeContentsDisposal(bool isRecvBuffer)
    {
        BufferItem bufferItem = isRecvBuffer ? _recvBuffer[0] : _sendBuffer[0];
        
        List<Pawn> pawns = [bufferItem.Pawn];
        if (bufferItem.CarriedPawn != null) pawns.Add(bufferItem.CarriedPawn);
        
        DamageInfo deathInfo = new(isRecvBuffer ? SgDamageDefOf.StargatesMod_IrisCollisionDeath : SgDamageDefOf.StargatesMod_DisintegrationDeath, 99999f, 999f);

        foreach (Pawn p in pawns)
        {
            //Remove deathRefusal hediff to avoid error
            if (p.health.hediffSet.HasHediff(HediffDefOf.DeathRefusal))
                p.health.RemoveHediff(p.health.hediffSet.hediffs.Find(hediff => hediff is Hediff_DeathRefusal));
            
            p.Kill(deathInfo);
        }

        if (isRecvBuffer)
        {
            _recvBuffer.Remove(bufferItem);
            SgSoundDefOf.StargateMod_IrisHit.PlayOneShot(SoundInfo.InMap(parent));
        }
        else _sendBuffer.Remove(bufferItem);
    }
        
    public void AddToSendBuffer(BufferItem bufferItem)
    {
        _sendBuffer.Add(bufferItem);
        PlayTeleportSound();
    }

    public void AddToReceiveBuffer(BufferItem bufferItem) => _recvBuffer.Add(bufferItem);


    private void ClearBuffers()
    {
        List<BufferItem> bufferList = [];
        bufferList.AddRange(_sendBuffer);
        bufferList.AddRange(_recvBuffer);
        if (bufferList.NullOrEmpty()) return;
        
        foreach (BufferItem bufferItem in bufferList)
        {
            GenSpawn.Spawn(bufferItem.Thing, parent.InteractionCell, parent.Map);
            
            bufferItem.Pawn?.drafter?.Drafted = bufferItem.Drafted;
            if (bufferItem.CarriedPawn != null) bufferItem.Pawn?.carryTracker.innerContainer.TryAdd(bufferItem.CarriedPawn, false);
        }
        
        _sendBuffer.Clear();
        _recvBuffer.Clear();
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
            
        if (!StargateIsActive)
        {
            if (TicksUntilOpen > -1)
            {
                if (_checkVortexPawnsTick < 0) _checkVortexPawnsTick = _checkVortexPawnsDelayTick;
                if (TicksUntilOpen == _checkVortexPawnsTick)
                {
                    if (!IrisIsActivated) CheckVortexPawns();
                    _checkVortexPawnsTick = TicksUntilOpen - _checkVortexPawnsDelayTick;
                }
            }
        }

        if (!StargateIsActive) return;
            
        if (!IrisIsActivated && _ticksSinceOpened < 150 && _ticksSinceOpened % 10 == 0)
            DoUnstableVortex();
            
        if (IsReceivingGate && _ticksSinceOpened < 60 && parent.Fogged()) 
            FloodFillerFog.FloodUnfog(parent.Position, parent.Map);
            
        if (_ticksSinceOpened == 210) 
            EndStargateWatching();

        _connectedStargateComp ??= _connectedStargate?.TryGetComp<CompStargate>();
        _transComp ??= parent.GetComp<CompTransporter>();
            
        if (_transComp != null)
        {
            Thing transportThing = _transComp.innerContainer.FirstOrFallback();
            if (transportThing != null)
            {
                if (transportThing.Spawned) transportThing.DeSpawn();
                AddToSendBuffer(new BufferItem(transportThing));
                _transComp.innerContainer.Remove(transportThing);
            }
            else if (_transComp.LoadingInProgressOrReadyToLaunch && !_transComp.AnyInGroupHasAnythingLeftToLoad)
                _transComp.CancelLoad();
        }

        if (_sendBuffer.Any())
        {
            if (!IsReceivingGate)
            {
                if (_connectedStargateComp != null)
                {
                    _connectedStargateComp.AddToReceiveBuffer(_sendBuffer[0]);
                    _sendBuffer.Remove(_sendBuffer[0]);
                }
                else
                {
                    Log.Error("[StargatesMod] Connected CompStargate was null while trying to send Thing(s) through gate");
                    CloseStargate(true);
                }
            }
            else WormholeContentsDisposal(false);
        }

        if (_recvBuffer.Any() && _ticksSinceBufferUnloaded > Rand.Range(10, 80))
        {
            _ticksSinceBufferUnloaded = 0;
            if (!IrisIsActivated)
            {
                BufferItem bufferItem = _recvBuffer[0];
                
                GenSpawn.Spawn(bufferItem.Thing, parent.InteractionCell, parent.Map);
                if (bufferItem.Pawn != null)
                {
                    if (bufferItem.Pawn.Faction == Faction.OfPlayer) bufferItem.Pawn.drafter?.Drafted = bufferItem.Drafted;

                    if (bufferItem.CarriedPawn != null)
                    {
                        if (!bufferItem.Pawn.carryTracker.innerContainer.TryAdd(bufferItem.CarriedPawn, false))
                            GenSpawn.Spawn(bufferItem.Pawn, parent.InteractionCell, parent.Map);
                    }
                }

                _recvBuffer.Remove(bufferItem);
                PlayTeleportSound();
            }
            else WormholeContentsDisposal(true);
        }

        _ticksSinceBufferUnloaded++;
        _ticksSinceOpened++;
            
        if (_dialMode == DialMode.IncomingRaid && !_recvBuffer.Any())
            CloseStargate(false);
        
        if (IsReceivingGate && _ticksSinceBufferUnloaded > 2500 && !_connectedStargateComp.GateIsLoadingTransporter && _sendBuffer.Empty())
            CloseStargate(true);
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        if (!IsHibernating && !AddressComp.IsRegistered(GateAddress)) InitGate();
        
        if (StargateIsActive)
        {
            if (_connectedStargate == null && _dialMode <= DialMode.PocketMap)
                _connectedStargate = SgUtilities.GetAllStargatesOnMap(_dialMode == DialMode.Map ? Find.WorldObjects.MapParentAt(_connectedAddress).Map : Find.Maps[_connectedAddress.tileId]).FirstOrFallback();
                
            _puddleSustainer = SgSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
        }

        //fix nullreferenceexception that happens when the innercontainer disappears for some reason, hopefully this doesn't end up causing a bug that will take hours to track down ;)
        if (_transComp is { innerContainer: null })
            _transComp.innerContainer = new ThingOwner<Thing>(_transComp);
            
        if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] compsg postspawnssetup: sgactive={StargateIsActive} connectgate={_connectedStargate} connectaddress={_connectedAddress}, mapparent={parent.Map.Parent}");
    }

    public string GetInspectString()
    {
        if (!parent.Spawned) return "";

        StringBuilder sb = new();
        sb.AppendLine(!IsHibernating
            ? "SGM.GateAddress".Translate(GateDesignation)
            : "SGM.GateHibernating".Translate());
        switch (StargateIsActive)
        {
            case false when TicksUntilOpen <= -1 && !IsHibernating && !IsExpectingIncomingWormhole:
                sb.AppendLine("SGM.StargateIdle".Translate());
                break;
            case false when TicksUntilOpen > -1:
                if (IsExpectingIncomingWormhole) sb.AppendLine("SGM.IncomingWormhole".Translate(_connectedStargateComp.GateDesignation));
                else if (_dialMode == DialMode.IncomingRaid) sb.AppendLine("SGM.IncomingWormhole".Translate("SGM.UnknownAddress".Translate()));
                break;
            case true:
                sb.AppendLine("SGM.ConnectedToGate".Translate(_connectedStargateComp != null ? _connectedStargateComp.GateDesignation : "SGM.Unknown".Translate(),  
                    (IsReceivingGate ? "SGM.Incoming" : "SGM.Outgoing").Translate()));
                break;
        }
        if (HasIris) sb.AppendLine("SGM.IrisStatus".Translate((IrisIsActivated ? "SGM.IrisClosed" : "SGM.IrisOpen").Translate()));
        if (TicksUntilOpen > 0) sb.AppendLine("SGM.TimeUntilGateLock".Translate(TicksUntilOpen.ToStringTicksToPeriod()));

            
        if (!_modSettings.DebugMode) return sb.ToString().TrimEndNewlines();
        sb.AppendLine("=== DebugInfo ===");
        sb.AppendLine($"TicksSinceOpened = {_ticksSinceOpened}");
        sb.AppendLine($"TicksUntilOpen = {TicksUntilOpen}");
        sb.AppendLine($"ticksSinceBufferUnloaded = {_ticksSinceBufferUnloaded}");
        sb.AppendLine($"IsInPocketMap = {IsInPocketMap}");

        sb.AppendLine($"_queuedAddress = {_queuedAddress}");
        sb.AppendLine($"connectedAddress = {_connectedAddress}");
        sb.AppendLine($"DialMode = {_dialMode}");
        sb.AppendLine($"IsReceivingGate = {IsReceivingGate}");

        sb.AppendLine($"_gateAddress = {GateAddress}");
        sb.AppendLine($"_gateDesignation = {_gateDesignation}");
        
        string conflictingGateStr = _conflictingGate == null ? "null" : _conflictingGate.ToString();
        sb.AppendLine($"_conflictingGate = " + conflictingGateStr);
        
        sb.AppendLine($"_transComp = {_transComp}");
        sb.AppendLine($"_connectedStargateComp = {_connectedStargateComp}");
        
        string pawnsWatchingStargateStr = _pawnsWatchingStargate == null || !_pawnsWatchingStargate.Any() 
            ? "null" : _pawnsWatchingStargate[0].ToString();
        sb.AppendLine($"_PawnsWatchingStargate0 = {pawnsWatchingStargateStr}");
        
        return sb.ToString().TrimEndNewlines();
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            
        if (StargateIsActive && _connectedStargate != null)
        {
            Command_Action selectConnectedGate = new()
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
            Command_Action irisControl = new()
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
            
        if (!IsReceivingGate && _connectedStargate != null && Faction.OfPlayer.def.techLevel >= TechLevel.Industrial)
        {
            CompStargate connectedSgComp = _connectedStargate.TryGetComp<CompStargate>();
                
            if (_connectedStargate.Faction == Faction.OfPlayer && connectedSgComp.Props.canHaveIris && connectedSgComp.HasIris)
            {
                Command_Action remoteIrisControl = new()
                {
                    defaultLabel = "SGM.TransmitGDO".Translate(),
                    defaultDesc = "SGM.TransmitGDODesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/StargateTransmitGDO"),
                    action = delegate
                    {
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(_connectedStargate));
                        connectedSgComp.ChangeIrisState();
                    }
                };
                if (!connectedSgComp.IrisIsActivated) remoteIrisControl.Disable("SGM.CannotGDO".Translate());
                yield return remoteIrisControl;
            }
        }
            
        if (IsHibernating)
        {
            Command_Action makeActiveGate = new()
            {
                defaultLabel = "SGM.WakeHibernation".Translate(),
                defaultDesc = "SGM.WakeHibernationDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/StargateUnHibernate"),
                action = InitGate
            };
            yield return makeActiveGate;

            if (_conflictingGate != null)
            {
                Command_Action selectConflictingGate = new()
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
            
        Command_Action devForceClose = new()
        {
            defaultLabel = "Force close",
            defaultDesc = "Force close this gate to hopefully remove strange behaviours (this will not close gate at the other end).",
            action = delegate
            {
                CloseStargate(false);
                Log.Message($"[StargatesMod] Stargate {parent} was force-closed.");
            }
        };
        yield return devForceClose;
            
        if (!Props.canHaveIris) yield break;
        Command_Action devAddRemoveIris = new()
        {
            defaultLabel = "Add/remove iris",
            action = delegate 
            { 
                HasIris = !HasIris;
                IrisIsActivated = false;
            }
        };
        yield return devAddRemoveIris;
    }

    private void CleanupGate(Map map)
    {
        if (_connectedStargate != null) CloseStargate(true);

        if (IsInPocketMap) AddressComp.RemovePocketMapAddress(GateAddress);
        else AddressComp.RemoveAddress(GateAddress);
            
        List<Thing> gates = SgUtilities.GetAllStargatesOnMap(map, excludeHibernating: false, includeLinkedMaps: true);
        if (!gates.Any()) return;
        foreach (Thing gate in gates.Where(g => g.TryGetComp<CompStargate>()._conflictingGate == parent))
        {
            gate.TryGetComp<CompStargate>()._conflictingGate = null;
        }
    }
    
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        CleanupGate(map);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        CleanupGate(previousMap);
    }
        
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref StargateIsActive, "StargateIsActive");
        Scribe_Values.Look(ref IsReceivingGate, "IsReceivingGate");
        Scribe_Values.Look(ref IsInPocketMap, "IsInPocketMap");
        Scribe_Values.Look(ref HasIris, "HasIris");
        Scribe_Values.Look(ref IrisIsActivated, "IrisIsActivated");
        Scribe_Values.Look(ref _ticksSinceOpened, "TicksSinceOpened");
        Scribe_Values.Look(ref IsHibernating, "IsHibernating");
        Scribe_Values.Look(ref _connectedAddress, "_connectedAddress");
        Scribe_References.Look(ref _connectedStargate, "_connectedStargate");
        Scribe_Collections.Look(ref _recvBuffer, "_recvBuffer", LookMode.GlobalTargetInfo);
        Scribe_Collections.Look(ref _sendBuffer, "_sendBuffer", LookMode.GlobalTargetInfo);
    }

    public override string CompInspectStringExtra() => base.CompInspectStringExtra() + "SGM.RespawnGateString".Translate();


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

    public List<IntVec3> vortexPattern =
    [
        new(0, 0, 1),
        new(1, 0, 1),
        new(-1, 0, 1),
        new(0, 0, 0),
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 0, -1),
        new(1, 0, -1),
        new(-1, 0, -1),
        new(0, 0, -2),
        new(1, 0, -2),
        new(-1, 0, -2),
        new(0, 0, -3)
    ];
}