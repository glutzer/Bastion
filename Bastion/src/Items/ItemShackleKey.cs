using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

public class ItemShackleKey : Item
{
    public ICoreClientAPI capi;
    public ICoreServerAPI sapi;
    public int maxMs;

    public Bastion Bastion => api.ModLoader.GetModSystem<Bastion>();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        capi = api as ICoreClientAPI;
        sapi = api as ICoreServerAPI;

        //maxMs = Attributes["maxSeconds"].AsInt(1000) * 1000;
        maxMs = BConfig.Loaded.maxSeconds * 1000;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

        handling = EnumHandHandling.PreventDefault; // Prevent default right-click behavior.

        ITreeAttribute attribs = slot?.Itemstack?.Attributes;

        if (sapi != null)
        {
            string name = attribs.GetString("prisonerName");
            if (name == null) return;

            string uid = Bastion.nameToUid.Get(name);
            if (uid == null) return;

            ShackleInfo info = Bastion.activeShackles.Get(uid);

            IServerPlayer player = (IServerPlayer)sapi.World.PlayerByUid(uid);
            if (player == null) return;
            if (byEntity.ServerPos.DistanceTo(player.Entity.ServerPos) > 100) return; // Can only reinforce nearby players.

            if (byEntity.Controls.Sneak)
            {
                // Free player here.
                Bastion.RemoveShackle(uid);
            }
            else if (byEntity.Controls.Sprint)
            {

            }
            else // Try to fuel it.
            {
                _ = slot.Inventory.Any(s =>
                {
                    if (s?.Itemstack?.Item is ItemTemporalGear)
                    {
                        long currentMs = info.msRemaining;

                        if (currentMs >= maxMs) return true;

                        currentMs += 1000 * BConfig.Loaded.temporalGearSeconds; // 10 minutes, set configuration somewhere?

                        if (currentMs > maxMs) currentMs = maxMs;

                        info.msRemaining = currentMs;

                        s.TakeOut(1);
                        s.MarkDirty();
                        slot.MarkDirty();
                        byEntity.World.PlaySoundAt(new AssetLocation("sounds/effect/portal.ogg"), byEntity, null, true);

                        return true;
                    }

                    return false;
                });
            }
        }
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        ITreeAttribute attribs = inSlot?.Itemstack?.Attributes;
        string prisonerName = attribs?.GetString("prisonerName") ?? "Nobody";
        long msRemaining = attribs?.GetLong("msRemaining", 0) ?? 0;

        TimeSpan time = TimeSpan.FromMilliseconds(msRemaining);

        dsc.AppendLine("Shackled: " + prisonerName).AppendLine("Remaining Time: " + time.ToString(@"dd\:hh\:mm\:ss")).AppendLine("Sneak-click to free prisoners").AppendLine("Use a gear to extend duration");

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }

    public void UpdateState(IWorldAccessor world, ItemSlot slot, EntityPlayer player)
    {
        if (slot == null) return;

        if (world.Side.IsServer() && slot is not ItemSlotCreative)
        {
            // Don't update if no attributes assigned to key.
            ITreeAttribute attribs = slot?.Itemstack?.Attributes;
            if (attribs?.GetString("prisonerName") != null)
            {
                ShackleInfo info = Bastion.activeShackles.Get(Bastion.nameToUid.Get(attribs.GetString("prisonerName"), " "));

                if (info == null)
                {
                    // Break.
                    slot.TakeOutWhole();
                    world.PlaySoundAt(new AssetLocation("bastionmod:sounds/break"), player, null, false);
                }
                else
                {
                    attribs.SetLong("msRemaining", info.msRemaining);

                    if (info.msRemaining <= 0)
                    {
                        // Break.
                        slot.TakeOutWhole();
                        world.PlaySoundAt(new AssetLocation("bastionmod:sounds/break"), player, null, false);
                    }
                }

                slot.MarkDirty();
            }
        }
    }

    public override void InGuiIdle(IWorldAccessor world, ItemStack stack)
    {
        GuiTransform.Rotation.Y = GameMath.Mod((float)world.ElapsedMilliseconds / 50f, 360f);
    }
}