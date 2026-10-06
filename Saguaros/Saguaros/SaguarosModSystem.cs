using HarmonyLib;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Saguaros
{
    public class SaguarosModSystem : ModSystem
    {
        private const string HarmonyId = "saguaro";
        private Harmony _harmony;

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            api.RegisterBlockEntityClass("SaguaroCactus",typeof(BlockEntitySaguaro));
            api.RegisterBlockBehaviorClass("SaguaroHarvestable",typeof(SaguaroHarvestableBehavior));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
        }
        public override void Dispose()
        {
            base.Dispose();

            _harmony?.UnpatchAll(HarmonyId);
        }
    }
}
