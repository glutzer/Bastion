using System.Text;
using Vintagestory.API.Common;

public class ItemShackle : Item
{
    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        dsc.AppendLine("Defeat a player in combat to imprison them")
            .AppendLine("Holds for 10 minutes");

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}