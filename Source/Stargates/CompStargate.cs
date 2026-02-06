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
    const string alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private List<BufferItem> _sendBuffer = [];
    private List<BufferItem> _recvBuffer = [];
    private int TicksSinceBufferUnloaded;
    private int TicksSinceOpened;
    private DialMode DialMode;
    public PlanetTile GateAddress;
    public bool IsInPocketMap = false;
    public bool StargateIsActive;
    public bool IsReceivingGate;
    public bool IsHibernating;
    private Thing ConflictingGate;
    public bool HasIris = false;
    public int TicksUntilOpen = -1;
    public bool IrisIsActivated = false;
    private int _prevRingSoundQueue = 0;
    private int _chevronSoundCounter = 0;
    private PlanetTile _queuedAddress = -1;
    private PlanetTile _connectedAddress = -1;
    private Thing ConnectedStargate;

    private int _checkVortexPawnsTick = 120;
    private const int _checkVortexPawnsDelayTick = 10;
    private List<Pawn> _pawnsWatchingStargate;
        
    private Sustainer _puddleSustainer;

    private readonly StargatesModSettings _modSettings = LoadedModManager.GetMod<StargatesMod>().GetSettings<StargatesModSettings>();
        
    public CompProperties_Stargate Props => (CompProperties_Stargate)props;

    private Graphic StargatePuddle =>
        field ??= GraphicDatabase.Get<Graphic_Single>(Props.puddleTexture,
            ShaderDatabase.Mote, Props.puddleDrawSize, Color.white);

    private Graphic StargateIris =>
        field ??= GraphicDatabase.Get<Graphic_Single>(Props.irisTexture,
            ShaderDatabase.Mote, Props.puddleDrawSize, Color.white);

    private bool GateIsLoadingTransporter => parent.GetComp<CompTransporter>() is { LoadingInProgressOrReadyToLaunch: true, AnyInGroupHasAnythingLeftToLoad: true };

    public IEnumerable<IntVec3> VortexCells
    {
        get { return Props.vortexPattern.Select(offset => parent.Position + offset.RotatedBy(parent.Rotation)); }
    }

    #region DHD Controls

    public void OpenStargateDelayed(PlanetTile address, int delay, DialMode dialMode)
    {
        _queuedAddress = address;
        DialMode = dialMode;

        TicksUntilOpen = delay;
        _checkVortexPawnsTick = 120;
    }


    private void OpenStargate(PlanetTile address)
    {
        var connectedMapParent = (MapParent)null;
        switch (DialMode)
        {
            case DialMode.None:
                Log.Error("[StargatesMod] Dial failed: No dial mode set.");
                DialFail();
                return;
            case DialMode.IncomingRaid:
                IsReceivingGate = true;
                TicksSinceBufferUnloaded = -150;
                StargateIsActive = true;
                break;
            case DialMode.Map:
                connectedMapParent = Find.WorldObjects.MapParentAt(address);
                break;
            case DialMode.PocketMap:
                int pocketMapIndex = _queuedAddress.tileId;
                connectedMapParent = Find.Maps.ElementAt(pocketMapIndex).PocketMapParent;
                break;
        }
             
        if (DialMode != DialMode.IncomingRaid && connectedMapParent == null)
        {
            Log.Error($"[StargatesMod] Failed to find MapParent at {_queuedAddress} with dial mode {DialMode}");
            DialFail();
            return;
        }
                 
        if (DialMode == DialMode.Map && connectedMapParent is { HasMap: false })
        {
            if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] generating map for {connectedMapParent}");
                
            LongEventHandler.QueueLongEvent(delegate
            {
                GetOrGenerateMapUtility.GetOrGenerateMap(connectedMapParent.Tile, connectedMapParent is WorldObject_PermSGSite ? new IntVec3(75, 1, 75) : Find.World.info.initialMapSize, connectedMapParent.def);
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

        if (DialMode != DialMode.IncomingRaid)
        {
            Thing connectedGate = GetActiveStargateOnMap(connectedMapParent.Map);
                
            if (connectedGate == null || connectedGate.TryGetComp<CompStargate>().StargateIsActive)
            {
                if (connectedGate == null) Log.Error($"[StargatesMod] Failed to find target stargate");
                else if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] failed to dial stargate; target stargate was already active");
                DialFail();
                return;
            }
                
            ConnectedStargate = connectedGate;
                
            CompStargate connectedSgComp = ConnectedStargate.TryGetComp<CompStargate>();
            connectedSgComp.StargateIsActive = true;
            connectedSgComp.IsReceivingGate = true;
            connectedSgComp.ConnectedStargate = parent;
                
            switch (DialMode)
            {
                case DialMode.Map:
                    _connectedAddress = address;
                    break;
                case DialMode.PocketMap:
                    _connectedAddress = address.tileId;
                    break;
            }

            connectedSgComp._connectedAddress = GateAddress;

            connectedSgComp._puddleSustainer = SgSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(connectedSgComp.parent));
            SgSoundDefOf.StargateMod_SGOpen.PlayOneShot(SoundInfo.InMap(connectedSgComp.parent));
                    
            CompGlower otherGlowComp = connectedSgComp.parent.GetComp<CompGlower>();
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
        CompTransporter transComp = parent.GetComp<CompTransporter>();
        transComp?.CancelLoad();

        //clear buffers just in case
        ClearBuffers();

        CompStargate connectedGateComp = null;
        if (closeOtherGate)
        {
            connectedGateComp = ConnectedStargate.TryGetComp<CompStargate>();
                
            if (ConnectedStargate == null || connectedGateComp == null)
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

        StargateIsActive = false;
        TicksSinceBufferUnloaded = 0;
        TicksSinceOpened = 0;
        _connectedAddress = -1;
        ConnectedStargate = null;
        IsReceivingGate = false;
    }

    private void DialFail()
    {
        Messages.Message("SGM.GateDialFailed".Translate(), MessageTypeDefOf.NegativeEvent);
        SgSoundDefOf.StargateMod_SGFailDial.PlayOneShot(SoundInfo.InMap(parent));

        _queuedAddress = -1;
        ConnectedStargate = null;

        DialMode = DialMode.None;
        
        //In case buffers contain anything before gate opens (like gate raids)
        ClearBuffers();
    }
        
    #endregion

    public static Thing GetActiveStargateOnMap(Map map, Thing thingToIgnore = null)
    {
        Thing gateOnMap = map.listerBuildings.allBuildingsColonist.Where(t => t.def.thingClass == typeof(Building_Stargate) && !t.TryGetComp<CompStargate>().IsHibernating && t != thingToIgnore).FirstOrFallback() ??
                          map.listerBuildings.allBuildingsNonColonist.Where(t => t.def.thingClass == typeof(Building_Stargate) && !t.TryGetComp<CompStargate>().IsHibernating && t != thingToIgnore).FirstOrFallback();
            
        return gateOnMap;
    }

    public static List<Thing> GetAllStargatesOnMap(Map map, List<Thing> thingsToIgnore = null, bool excludeHibernating = true, bool includeLinkedMaps = false)
    {
        List<Thing> gates = [];
            
        gates.AddRange(map.listerBuildings.allBuildingsColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)));
        gates.AddRange(map.listerBuildings.allBuildingsNonColonist.Where(t => t.def.thingClass == typeof(Building_Stargate)));
            
        if (excludeHibernating) gates.RemoveWhere(t => t.TryGetComp<CompStargate>().IsHibernating);
        if (thingsToIgnore != null) gates.RemoveWhere(thingsToIgnore.Contains);

        if (!includeLinkedMaps) return gates;
            
        if (map.IsPocketMap && map.PocketMapParent.sourceMap != null) gates.AddRange(GetAllStargatesOnMap(map.PocketMapParent.sourceMap, excludeHibernating: false));
        if (map.ChildPocketMaps.Any())
        {
            foreach (Map childMap in map.ChildPocketMaps)
            {
                gates.AddRange(GetAllStargatesOnMap(childMap, excludeHibernating: false));
            }
        }

        return gates;
    }

    public static string GetStargateDesignation(PlanetTile address)
    {
        if (address.tileId < 0) return "UnknownLower".Translate();
            
        Rand.PushState(address.tileId);
        //pattern: (prefix)(num)(char)-(num)(num)(num)
        string prefix = address.Layer.Def.isSpace ? "O" : "P"; //Planet layer designation: O for orbit / space, P for planetary / other
        string designation = $"{prefix}{Rand.RangeInclusive(0, 9)}{alpha[Rand.RangeInclusive(0, 25)]}-{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}{Rand.RangeInclusive(0, 9)}"; 
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
            
        if (IrisIsActivated) SgSoundDefOf.StargateMod_IrisOpen.PlayOneShot(SoundInfo.InMap(parent));
        else SgSoundDefOf.StargateMod_IrisClose.PlayOneShot(SoundInfo.InMap(parent));
    }

    private void DoUnstableVortex()
    {
        List<Thing> excludedThings = [parent];
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
            DamageDef damType = SgDamageDefOf.StargatesMod_KawooshExplosion;

            Explosion explosion = (Explosion)GenSpawn.Spawn(ThingDefOf.Explosion, parent.Position, parent.Map);
            explosion.damageFalloff = false;
            explosion.damAmount = damType.defaultDamage;
            explosion.Position = pos;
            explosion.radius = 0.5f;
            explosion.damType = damType;
            explosion.StartExplosion(null, excludedThings);
                
                
            foreach (var thing in destroySpecial)
            {
                if(Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] destroying specialThing {thing}");
                thing.Destroy();
            }
            destroySpecial.Clear();
        }
    }

    private void InitGate()
    {
        bool isHibernatingAlready = IsHibernating;
            
        if (GetAllStargatesOnMap(parent.Map, includeLinkedMaps: true).Any())
        {
            string cannotWakeMessage = "SGM.Notif.CannotWake";
            string gateHibernatingMessage = "SGM.Notif.GateHibernating";
                
            ConflictingGate = GetAllStargatesOnMap(parent.Map, includeLinkedMaps: true).First();
                
            if (isHibernatingAlready) Messages.Message(cannotWakeMessage.Translate(), MessageTypeDefOf.RejectInput);
            else Messages.Message(gateHibernatingMessage.Translate(), MessageTypeDefOf.CautionInput);

            IsHibernating = true;
        }
        else
        {
            var addressWorldComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
                
            IsInPocketMap = parent.Map.IsPocketMap;
            if (IsInPocketMap)
            {
                GateAddress = parent.Map.Index;
                addressWorldComp.AddPocketMapAddress(GateAddress);
            }
            else
            {
                GateAddress = parent.Map.Tile;
                addressWorldComp.AddAddress(GateAddress);
            }
            IsHibernating = false;
            ConflictingGate = null;
                
                
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
            
        foreach (Thing thing in cells.SelectMany(pos => map.thingGrid.ThingsAt(pos)).Where(thing => thing is Pawn { DeadOrDowned: false, Drafted: false } pawn && !_pawnsWatchingStargate.Contains(pawn)))
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
            pawn.jobs.ClearQueuedJobs();
            Job job = JobMaker.MakeJob(SgJobDefOf.StargatesMod_WatchStargate, parent, destPos);
            pawn.jobs.StartJob(job);
            _pawnsWatchingStargate.Add(pawn);
        }
    }

    private void EndStargateWatching()
    {
        if (!_pawnsWatchingStargate.Any()) return;
        foreach (Pawn pawn in _pawnsWatchingStargate.ToList().Where(pawn => !pawn.DeadOrDowned && !pawn.Drafted && pawn.CurJob.def == SgJobDefOf.StargatesMod_WatchStargate))
        {
            pawn.jobs.StopAll();
        }
        _pawnsWatchingStargate.Clear();
    }
        
    private void WormholeContentsDisposal(bool isRecvBuffer)
    {
        BufferItem bufferItem = isRecvBuffer ? _recvBuffer[0] : _sendBuffer[0];
            
        //Remove deathRefusal hediff to avoid error
        Pawn pawn = bufferItem.Pawn;
        if (pawn != null && pawn.health.hediffSet.HasHediff(HediffDefOf.DeathRefusal))
            pawn.health.RemoveHediff(pawn.health.hediffSet.hediffs.Find(hediff  => hediff is Hediff_DeathRefusal));

        Pawn carriedPawn = bufferItem.CarriedPawn;
        if (carriedPawn != null && carriedPawn.health.hediffSet.HasHediff(HediffDefOf.DeathRefusal))
            carriedPawn.health.RemoveHediff(carriedPawn.health.hediffSet.hediffs.Find(hediff  => hediff is Hediff_DeathRefusal));
        
        DamageInfo disintDeathInfo = new(SgDamageDefOf.StargatesMod_DisintegrationDeath, 99999f, 999f);

        bufferItem.CarriedPawn?.Kill(disintDeathInfo);
        bufferItem.Thing.Kill(disintDeathInfo);

        if (!isRecvBuffer)
        {
            _sendBuffer.Remove(bufferItem);
        }
        else
        {
            _recvBuffer.Remove(bufferItem);
            SgSoundDefOf.StargateMod_IrisHit.PlayOneShot(SoundInfo.InMap(parent));
        }
    }
        
    public void AddToSendBuffer(BufferItem bufferItem)
    {
        _sendBuffer.Add(bufferItem);
        PlayTeleportSound();
    }

    public void AddToReceiveBuffer(BufferItem bufferItem)
    {
        _recvBuffer.Add(bufferItem);
    }

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
            
        if (!StargateIsActive && !IrisIsActivated)
        {
            if (TicksUntilOpen > -1)
            {
                if (_checkVortexPawnsTick < 0) _checkVortexPawnsTick = _checkVortexPawnsDelayTick;
                if (TicksUntilOpen == _checkVortexPawnsTick)
                {
                    CheckVortexPawns();
                    _checkVortexPawnsTick = TicksUntilOpen - _checkVortexPawnsDelayTick;
                }
            }
        }

        if (!StargateIsActive) return;
            
        if (!IrisIsActivated && TicksSinceOpened < 150 && TicksSinceOpened % 10 == 0)
            DoUnstableVortex();
            
        if (IsReceivingGate && TicksSinceOpened < 60 && parent.Fogged()) FloodFillerFog.FloodUnfog(parent.Position, parent.Map);
            
        if (_pawnsWatchingStargate != null && _pawnsWatchingStargate.Any() && TicksSinceOpened == 210 ) EndStargateWatching();
            
            
        CompStargate connectedStargateComp = ConnectedStargate.TryGetComp<CompStargate>();
        CompTransporter transComp = parent.GetComp<CompTransporter>();
            
        if (transComp != null)
        {
            Thing transportThing = transComp.innerContainer.FirstOrFallback();
            if (transportThing != null)
            {
                if (transportThing.Spawned) transportThing.DeSpawn();
                AddToSendBuffer(new BufferItem(transportThing));
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
            else WormholeContentsDisposal(false);
        }

        if (_recvBuffer.Any() && TicksSinceBufferUnloaded > Rand.Range(10, 80))
        {
            TicksSinceBufferUnloaded = 0;
            if (!IrisIsActivated)
            {
                BufferItem bufferItem = _recvBuffer[0];
                
                if (bufferItem.Pawn == null)
                    GenSpawn.Spawn(bufferItem.Thing, parent.InteractionCell, parent.Map);
                else
                {
                    GenSpawn.Spawn(bufferItem.Pawn, parent.InteractionCell, parent.Map);
                        
                    if (bufferItem.Pawn.Faction == Faction.OfPlayer) bufferItem.Pawn.drafter.Drafted = bufferItem.Drafted;

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

        TicksSinceBufferUnloaded++;
        TicksSinceOpened++;
            
        if (DialMode == DialMode.IncomingRaid && !_recvBuffer.Any())
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
            if (ConnectedStargate == null && DialMode <= DialMode.PocketMap)
                ConnectedStargate = GetActiveStargateOnMap(DialMode == DialMode.Map ? Find.WorldObjects.MapParentAt(_connectedAddress).Map : Find.Maps[_connectedAddress.tileId]);
                
            _puddleSustainer = SgSoundDefOf.StargateMod_SGIdle.TrySpawnSustainer(SoundInfo.InMap(parent));
        }

        //fix nullreferenceexception that happens when the innercontainer disappears for some reason, hopefully this doesn't end up causing a bug that will take hours to track down ;)
        CompTransporter transComp = parent.GetComp<CompTransporter>();
        if (transComp is { innerContainer: null })
            transComp.innerContainer = new ThingOwner<Thing>(transComp);
            
        if (Prefs.LogVerbose || _modSettings.DebugMode) Log.Message($"[StargatesMod] compsg postspawnssetup: sgactive={StargateIsActive} connectgate={ConnectedStargate} connectaddress={_connectedAddress}, mapparent={parent.Map.Parent}");
    }

    public string GetInspectString()
    {
        if (!parent.Spawned) return "";
        
        string displayedAddress = "" + GetStargateDesignation(!IsInPocketMap ? GateAddress : parent.Map.PocketMapParent.sourceMap.Tile);

        string connectedDisplayAddress = DialMode switch
        {
            DialMode.Map => "" + GetStargateDesignation(_connectedAddress),
            DialMode.PocketMap when (ConnectedStargate?.Map.PocketMapParent?.sourceMap?.Tile != null) => "" + GetStargateDesignation(ConnectedStargate.Map.PocketMapParent.sourceMap.Tile),
            _ => "SGM.Unknown".Translate()
        };

        StringBuilder sb = new();
        sb.AppendLine(!IsHibernating
            ? "SGM.GateAddress".Translate(displayedAddress)
            : "SGM.GateHibernating".Translate());
        switch (StargateIsActive)
        {
            case false when TicksUntilOpen <= -1 && !IsHibernating:
                sb.AppendLine("SGM.StargateIdle".Translate());
                break;
            case true:
                sb.AppendLine("SGM.ConnectedToGate".Translate(connectedDisplayAddress,
                    (IsReceivingGate ? "SGM.Incoming" : "SGM.Outgoing").Translate()));
                break;
        }
        if (HasIris) sb.AppendLine("SGM.IrisStatus".Translate((IrisIsActivated ? "SGM.IrisClosed" : "SGM.IrisOpen").Translate()));
        if (TicksUntilOpen > 0) sb.AppendLine("SGM.TimeUntilGateLock".Translate(TicksUntilOpen.ToStringTicksToPeriod()));

            
        if (!_modSettings.DebugMode) return sb.ToString().TrimEndNewlines();
        sb.AppendLine("=== DebugInfo ===");
        sb.AppendLine($"TicksSinceOpened = {TicksSinceOpened}");
        sb.AppendLine($"TicksUntilOpen = {TicksUntilOpen}");
        sb.AppendLine($"ticksSinceBufferUnloaded = {TicksSinceBufferUnloaded}");
        sb.AppendLine($"IsInPocketMap = {IsInPocketMap}");
        
        sb.AppendLine($"connectedAddress = {_connectedAddress}");
        sb.AppendLine($"DialMode = {DialMode}");
            
        string conflictingGateStr = ConflictingGate == null ? "null" : ConflictingGate.ToString();
        sb.AppendLine($"_conflictingGate = " + conflictingGateStr);
            
        string pawnsWatchingStargateStr = _pawnsWatchingStargate == null || !_pawnsWatchingStargate.Any() 
            ? "null" : _pawnsWatchingStargate[0].ToString();
        sb.AppendLine($"_PawnsWatchingStargate0 = {pawnsWatchingStargateStr}");

        return sb.ToString().TrimEndNewlines();
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            
        if (StargateIsActive && ConnectedStargate != null)
        {
            Command_Action selectConnectedGate = new()
            {
                defaultLabel = "SGM.SelectConnectedGate".Translate(),
                defaultDesc = "SGM.SelectConnectedGateDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/SelectStargate"),
                action = delegate
                {
                    CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(ConnectedStargate));
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
            
        if (!IsReceivingGate && ConnectedStargate != null && Faction.OfPlayer.def.techLevel >= TechLevel.Industrial)
        {
            CompStargate connectedSgComp = ConnectedStargate.TryGetComp<CompStargate>();
                
            if (ConnectedStargate.Faction == Faction.OfPlayer && connectedSgComp.Props.canHaveIris && connectedSgComp.HasIris)
            {
                Command_Action remoteIrisControl = new()
                {
                    defaultLabel = "SGM.TransmitGDO".Translate(),
                    defaultDesc = "SGM.TransmitGDODesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/StargateTransmitGDO"),
                    action = delegate
                    {
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(ConnectedStargate));
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

            if (ConflictingGate != null)
            {
                Command_Action selectConflictingGate = new()
                {
                    defaultLabel = "SGM.SelectGateConflict".Translate(),
                    defaultDesc = "SGM.SelectGateConflictDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/SelectStargate"),
                    action = delegate
                    {
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(ConflictingGate));
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
                Log.Message($"[StargatesMod] Stargate {parent.ThingID} was force-closed.");
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
        if (ConnectedStargate != null) CloseStargate(true);

        if (IsInPocketMap) Find.World.GetComponent<WorldComp_StargateAddresses>().RemovePocketMapAddress(GateAddress);
        else Find.World.GetComponent<WorldComp_StargateAddresses>().RemoveAddress(GateAddress);
            
        List<Thing> gates = GetAllStargatesOnMap(map, excludeHibernating: false, includeLinkedMaps: true);
        if (!gates.Any()) return;
        foreach (Thing gate in gates.Where(g => g.TryGetComp<CompStargate>().ConflictingGate == parent))
        {
            gate.TryGetComp<CompStargate>().ConflictingGate = null;
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
        Scribe_Values.Look(ref TicksSinceOpened, "TicksSinceOpened");
        Scribe_Values.Look(ref IsHibernating, "IsHibernating");
        Scribe_Values.Look(ref _connectedAddress, "_connectedAddress");
        Scribe_References.Look(ref ConnectedStargate, "_connectedStargate");
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