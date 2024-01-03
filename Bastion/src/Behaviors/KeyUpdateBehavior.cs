using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

public class KeyUpdateBehavior : EntityBehavior
{
    public BlockPos Pos => entity.Pos.AsBlockPos;
    public Bastion Bastion => entity.Api.ModLoader.GetModSystem<Bastion>();
    public long listener;
    public EntityPlayer entityPlayer;

    public KeyUpdateBehavior(Entity entity) : base(entity)
    {

    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        if (!entity.World.Side.IsServer()) return;

        entityPlayer = entity as EntityPlayer;

        listener = entity.World.RegisterGameTickListener(dt =>
        {
            (entity as EntityPlayer).WalkInventory(slot =>
            {
                if (slot is not ItemSlotCreative && slot.Itemstack?.Item is ItemShackleKey)
                {
                    // Update shackle key to have current time.
                    ((ItemShackleKey)slot.Itemstack.Item).UpdateState(entity.World, slot, entityPlayer);
                }

                return true;
            });
        }, 1000);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        entity.World.UnregisterGameTickListener(listener);
    }

    public override string PropertyName() => "keyupdater";
}