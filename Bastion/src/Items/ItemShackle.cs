using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class ItemShackle : Item
{
    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        dsc.AppendLine("Defeat a player in combat to imprison them")
            .AppendLine("Holds for 10 minutes");

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }

    public override void InGuiIdle(IWorldAccessor world, ItemStack stack)
    {
        GuiTransform.Rotation.Y = GameMath.Mod((float)world.ElapsedMilliseconds / 50f, 360f);
    }
}