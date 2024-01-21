using Verse;
using RimWorld;

namespace StargatesMod
{
    [DefOf]
    public static class SGSoundDefOf
    {
        static SGSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoundDefOf));
        }

        //re-add the dialling sound effect. 
        //please not CatCraftYT that you will obviously need to amplify the dialling sound because its much quieter than the KAWOOSH.
        
        public static SoundDef StargateMod_SGDial;
        public static SoundDef StargateMod_SGOpen;
        public static SoundDef StargateMod_SGFailDial;
        public static SoundDef StargateMod_SGKawooshExplosion;
        public static SoundDef StargateMod_SGIdle;
        public static SoundDef StargateMod_SGClose;
        public static SoundDef StargateMod_IrisOpen;
        public static SoundDef StargateMod_IrisClose;
        public static SoundDef StargateMod_IrisHit;
    }
}
