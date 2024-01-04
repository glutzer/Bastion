using HarmonyLib;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

public class Bastion : ModSystem
{
    public List<string> scheduledForRemoval = new();

    public Dictionary<string, ShackleInfo> activeShackles = new();
    public Dictionary<string, string> nameToUid = new();
    public Dictionary<string, ShackleInfo> offlinePlayerData = new();

    public ICoreAPI api;
    public ICoreServerAPI sapi;

    public bool currentlyShackled = false;

    public override void Start(ICoreAPI api)
    {
        this.api = api;

        api.RegisterItemClass("ItemShackle", typeof(ItemShackle));
        api.RegisterItemClass("ItemShackleKey", typeof(ItemShackleKey));
        api.RegisterEntityBehaviorClass("keyupdater", typeof(KeyUpdateBehavior));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        sapi.Event.RegisterGameTickListener(Update, 1000);

        sapi.Event.PlayerDeath += Event_PlayerDeath;

        sapi.Event.SaveGameLoaded += LoadShackleInfo;
        sapi.Event.GameWorldSave += SaveShackleInfo;

        sapi.Event.PlayerJoin += Event_PlayerJoin;
    }

    private void Event_PlayerJoin(IServerPlayer player)
    {
        string uid = player.PlayerUID;

        if (offlinePlayerData.ContainsKey(uid))
        {
            ShackleInfo info = offlinePlayerData[uid];

            sapi.SendIngameDiscovery(player, "freedom");

            // Reset role and spawn position to original values.
            player.SetSpawnPosition(new PlayerSpawnPos()
            {
                x = info.orgSpawnX,
                y = info.orgSpawnY,
                z = info.orgSpawnZ
            });
            UpdatePrivileges(player, false);

            offlinePlayerData.Remove(uid);
        }
    }

    private void Event_PlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
    {
        // Can't shackle already shackled player.
        if (activeShackles.ContainsKey(byPlayer.PlayerUID)) return;

        if (damageSource?.SourceEntity is EntityPlayer entityPlayer)
        {
            IServerPlayer killer = entityPlayer.Player as IServerPlayer;

            entityPlayer.WalkInventory(slot =>
            {
                if (slot?.Itemstack?.Item is ItemShackle)
                {
                    AddShackle(byPlayer, BConfig.Loaded.initialShackleSeconds);

                    slot.TakeOutWhole();

                    slot.Itemstack = new ItemStack(sapi.World.GetItem(new AssetLocation("bastionmod:shacklekey")), 1);
                    slot.Itemstack.Attributes.SetString("prisonerName", byPlayer.PlayerName);
                    slot.Itemstack.Attributes.SetLong("msRemaining", activeShackles.Get(byPlayer.PlayerUID).msRemaining);

                    slot.MarkDirty();

                    sapi.BroadcastMessageToAllGroups($"{byPlayer.PlayerName} has been imprisoned by {killer.PlayerName}!", EnumChatType.Notification);
                    byPlayer.Entity.World.PlaySoundAt(new AssetLocation("bastionmod:sounds/lock"), byPlayer, null, false);

                    return false;
                }

                return true;
            });
        }
    }

    public void Update(float dt)
    {
        if (activeShackles.Count == 0) return;

        long delta = (long)(dt * 1000);

        foreach (KeyValuePair<string, ShackleInfo> shackle in activeShackles)
        {
            shackle.Value.msRemaining -= delta;
            if (shackle.Value.msRemaining <= 0)
            {
                scheduledForRemoval.Add(shackle.Key);
            }
            else
            {
                IServerPlayer player = (IServerPlayer)sapi.World.PlayerByUid(shackle.Key);

                if (player != null)
                {
                    TimeSpan t = TimeSpan.FromMilliseconds(shackle.Value.msRemaining);

                    if (player.ConnectionState == EnumClientState.Playing) player.SendIngameError("timeremaining", $"{t.Hours}:{t.Minutes}:{t.Seconds}");
                }
            }
        }

        foreach (string uid in scheduledForRemoval)
        {
            RemoveShackle(uid);
        }

        scheduledForRemoval.Clear();
    }

    public void AddShackle(IServerPlayer player, int seconds)
    {
        ShackleInfo info = new(player, seconds, sapi);

        activeShackles.Add(player.PlayerUID, info);
        nameToUid.Add(player.PlayerName, player.PlayerUID);

        EntityPos playerPos = player.Entity.ServerPos;

        player.SetSpawnPosition(new PlayerSpawnPos()
        {
            x = (int)playerPos.X,
            y = (int)playerPos.Y,
            z = (int)playerPos.Z
        });

        UpdatePrivileges(player, true);
    }

    public void UpdatePrivileges(IServerPlayer player, bool deny)
    {
        string uid = player.PlayerUID;

        if (deny)
        {
            sapi.Permissions.DenyPrivilege(uid, "areamodify");
            sapi.Permissions.DenyPrivilege(uid, "build");
            sapi.Permissions.DenyPrivilege(uid, "useblock");
            sapi.Permissions.DenyPrivilege(uid, "selfkill");
        }
        else
        {
            sapi.Permissions.RemovePrivilegeDenial(uid, "areamodify");
            sapi.Permissions.RemovePrivilegeDenial(uid, "build");
            sapi.Permissions.RemovePrivilegeDenial(uid, "useblock");
            sapi.Permissions.RemovePrivilegeDenial(uid, "selfkill");
        }
    }

    public void RemoveShackle(string uid)
    {
        IServerPlayer player = (IServerPlayer)sapi.World.PlayerByUid(uid);

        ShackleInfo shackleInfo = activeShackles.Get(uid);

        if (player != null)
        {
            sapi.SendIngameDiscovery(player, "freedom", "You have been freed.");

            // Reset role and spawn position to original values.
            player.SetSpawnPosition(new PlayerSpawnPos()
            {
                x = shackleInfo.orgSpawnX,
                y = shackleInfo.orgSpawnY,
                z = shackleInfo.orgSpawnZ
            });

            UpdatePrivileges(player, false);
        }
        else // Add to be removed when player joins again.
        {
            offlinePlayerData.Add(uid, shackleInfo);
        }

        // Remove shackle.
        activeShackles.Remove(uid);
        nameToUid.Remove(player.PlayerName);
    }

    public void LoadShackleInfo()
    {
        byte[] activeShacklesData = sapi.WorldManager.SaveGame.GetData("activeShackles");
        byte[] nameToUidData = sapi.WorldManager.SaveGame.GetData("nameToUid");
        byte[] offlinePlayerDataData = sapi.WorldManager.SaveGame.GetData("offlinePlayerData");

        try
        {
            activeShackles = SerializerUtil.Deserialize<Dictionary<string, ShackleInfo>>(activeShacklesData);
            nameToUid = SerializerUtil.Deserialize<Dictionary<string, string>>(nameToUidData);
            offlinePlayerData = SerializerUtil.Deserialize<Dictionary<string, ShackleInfo>>(offlinePlayerDataData);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void SaveShackleInfo()
    {
        sapi.WorldManager.SaveGame.StoreData("activeShackles", SerializerUtil.Serialize(activeShackles));
        sapi.WorldManager.SaveGame.StoreData("nameToUid", SerializerUtil.Serialize(nameToUid));
        sapi.WorldManager.SaveGame.StoreData("offlinePlayerData", SerializerUtil.Serialize(offlinePlayerData));
    }

    public override void StartPre(ICoreAPI api)
    {
        string cfgFileName = "bastion.json";
        try
        {
            BConfig fromDisk;
            if ((fromDisk = api.LoadModConfig<BConfig>(cfgFileName)) == null)
            {
                api.StoreModConfig(BConfig.Loaded, cfgFileName);
            }
            else
            {
                BConfig.Loaded = fromDisk;
            }
        }
        catch
        {
            api.StoreModConfig(BConfig.Loaded, cfgFileName);
        }
    }
}