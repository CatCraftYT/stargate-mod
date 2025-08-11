using Verse;

namespace StargatesMod
{
    public class Dialog_RenameSGSite : Dialog_Rename<WorldObject_PermSGSite>
    {
        WorldObject_PermSGSite sgSite;

        public Dialog_RenameSGSite(WorldObject_PermSGSite sgSite) : base(sgSite)
        {
            this.sgSite = sgSite;
        }
    }
}
