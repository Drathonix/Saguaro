using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Saguaros.Patches;

[HarmonyPatch(typeof(BlockCactus), "TryPlaceBlockForWorldGen")]
public class PatchBlockCactus
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Transpilers.MethodReplacer(instructions,
            AccessTools.Method(typeof(IBlockAccessor), "SetBlock",[typeof(int),typeof(BlockPos)]),
            AccessTools.Method(typeof(PatchBlockCactus), "SetBlockRedirect"));
    }
    public static void SetBlockRedirect(IBlockAccessor blockAccessor, int id, BlockPos pos)
    {
        blockAccessor.SetBlock(id, pos);
        blockAccessor.SpawnBlockEntity("SaguaroCactus", pos);
    }
}
