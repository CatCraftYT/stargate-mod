using RimWorld;
using Verse;

namespace StargatesMod;

[DefOf]
public static class SgDamageDefOf
{
    static SgDamageDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DamageDefOf));
    
    public static DamageDef StargatesMod_KawooshExplosion;

    public static DamageDef StargatesMod_DisintegrationDeath;

    public static DamageDef StargatesMod_IrisCollisionDeath;
}