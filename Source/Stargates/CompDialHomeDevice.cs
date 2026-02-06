using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod;

public class CompDialHomeDevice : ThingComp
{
    CompFacility compFacility;
    public PlanetTile queuedAddress = -1;
    public DialMode DialMode;

    public CompProperties_DialHomeDevice Props => (CompProperties_DialHomeDevice)props;

    public CompStargate GetLinkedStargateComp()
    {
        if (Props.selfDialler) return parent.TryGetComp<CompStargate>(); 
        switch (compFacility.LinkedBuildings.Count)
        {
            case 0:
                return null;
            case > 1:
            {
                foreach (Thing thing in compFacility.LinkedBuildings.Where(t => !t.TryGetComp<CompStargate>().IsHibernating))
                {
                    return thing.TryGetComp<CompStargate>();
                }
                break;
            }
        }

        return compFacility.LinkedBuildings[0].TryGetComp<CompStargate>();
    }

    public static Thing GetDHDOnMap(Map map)
    {
        Thing dhdOnMap = map.listerBuildings.allBuildingsColonist.Where(t => t.TryGetComp<CompDialHomeDevice>() != null  && t.def.thingClass != typeof(Building_Stargate)).FirstOrFallback() ??
                         map.listerBuildings.allBuildingsNonColonist.Where(t => t.TryGetComp<CompDialHomeDevice>() != null  && t.def.thingClass != typeof(Building_Stargate)).FirstOrFallback();
            
        return dhdOnMap;
    }

    public bool IsConnectedToStargate
    {
        get
        {
            if (Props.selfDialler) return true;
            return compFacility.LinkedBuildings.Count != 0;
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        compFacility = parent.GetComp<CompFacility>();
    }
        
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra()) 
            yield return gizmo;

        CompStargate compStargate = GetLinkedStargateComp();
            
        if (compStargate == null) yield break;
            
        Command_Action commandCloseGate = new()
        {
            defaultLabel = "SGM.CloseStargate".Translate(),
            defaultDesc = "SGM.CloseStargateDesc".Translate(),
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
            action = delegate
            {
                compStargate.CloseStargate(true);
            }
        };
        if (!compStargate.StargateIsActive) commandCloseGate.Disable("SGM.GateIsNotActive".Translate());
        else if (compStargate.IsReceivingGate) commandCloseGate.Disable("SGM.CannotCloseIncoming".Translate());
        yield return commandCloseGate;
    }
}
public class CompProperties_DialHomeDevice : CompProperties
{
    public CompProperties_DialHomeDevice()
    {
        compClass = typeof(CompDialHomeDevice);
    }
    public bool selfDialler = false;
    public bool requiresPower = false;
}