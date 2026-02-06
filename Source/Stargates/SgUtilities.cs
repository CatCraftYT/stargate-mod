using System;
using Verse;

namespace StargatesMod;

public enum DialMode
{
    Map,
    PocketMap,
    IncomingRaid,
    None
}

public readonly struct BufferItem(Thing thing, bool drafted = false, Pawn carriedPawn = null) : IEquatable<BufferItem>
{
    public readonly Thing Thing = thing;
    public readonly bool Drafted = drafted;
    public readonly Pawn CarriedPawn = carriedPawn;

    public Pawn Pawn
    {
        get
        {
            if (Thing is Pawn pawn) return pawn;
            return null;
        }
    }
    
    public bool Equals(BufferItem other)
    {
        return Equals(Thing, other.Thing) && Drafted == other.Drafted && Equals(CarriedPawn, other.CarriedPawn);
    }

    public override bool Equals(object obj)
    {
        return obj is BufferItem other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Thing, Drafted, CarriedPawn);
    }
}