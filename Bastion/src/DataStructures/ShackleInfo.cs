using ProtoBuf;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ShackleInfo
{
    public string name;
    public string uid;
    public long msRemaining;
    public int orgSpawnX;
    public int orgSpawnY;
    public int orgSpawnZ;

    public ShackleInfo(IServerPlayer player, int seconds, ICoreServerAPI sapi)
    {
        name = player.PlayerName;
        uid = player.PlayerUID;
        msRemaining = seconds * 1000;

        FuzzyEntityPos spawn = player.GetSpawnPosition(false);

        orgSpawnX = (int)spawn.X;
        orgSpawnY = (int)spawn.Y;
        orgSpawnZ = (int)spawn.Z;
    }
}