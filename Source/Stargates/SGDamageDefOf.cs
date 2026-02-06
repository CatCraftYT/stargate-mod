using RimWorld;
using Verse;

namespace StargatesMod;

[DefOf]
public static class SGDamageDefOf
{
    static SGDamageDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(DamageDefOf));
    }
    
    public static DamageDef StargatesMod_KawooshExplosion;

    public static DamageDef StargatesMod_DisintegrationDeath;
}