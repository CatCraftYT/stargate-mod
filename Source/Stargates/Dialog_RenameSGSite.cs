using Verse;

namespace StargatesMod;

public class Dialog_RenameSgSite(WorldObject_PermSgSite sgSite) : Dialog_Rename<WorldObject_PermSgSite>(sgSite)
{
    WorldObject_PermSgSite _sgSite = sgSite;
}