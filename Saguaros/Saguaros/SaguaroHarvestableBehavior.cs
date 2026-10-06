using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Saguaros;

public class SaguaroHarvestableBehavior : BlockBehaviorHarvestable
{
    private float harvestTime;
    private bool exchangeBlock;
    private AssetLocation harvestedBlockCode;
    private Block? harvestedBlock;

    public SaguaroHarvestableBehavior(Block block) : base(block)
    {

    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        harvestTime = properties["harvestTime"].AsFloat();

        exchangeBlock = properties["exchangeBlock"].AsBool();
        string text = properties["harvestingSound"].AsString("game:sounds/block/leafy-picking");
        if (text != null)
        {
            harvestingSound = AssetLocation.Create(text, block.Code.Domain);
        }

        if (text != null)
        {
            harvestedBlockCode = AssetLocation.Create(text, block.Code.Domain);
        }
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        harvestedBlock = api.World.GetBlock(harvestedBlockCode);
        if (harvestedBlock == null)
        {
            api.World.Logger.Warning("Unable to resolve harvested block code '{0}' for block {1}. Will ignore.", harvestedBlockCode, block.Code);
        }
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
    {
        if (Tool.HasValue && byPlayer.InventoryManager.ActiveTool != Tool)
        {
            return;
        }

        handled = EnumHandling.PreventDefault;
        if (!(secondsUsed > harvestTime - 0.05f) || harvestedStacks == null || world.Side != EnumAppSide.Server)
        {
            return;
        }

        float dropRate = 1f;
        JsonObject attributes = block.Attributes;
        if (attributes != null && attributes.IsTrue("forageStatAffected"))
        {
            dropRate *= byPlayer.Entity.Stats.GetBlended("forageDropRate");
        }

        harvestedStacks.Foreach(delegate (BlockDropItemStack harvestedStack)
        {
            ItemStack nextItemStack = harvestedStack.GetNextItemStack(dropRate);
            if (nextItemStack != null)
            {
                ItemStack itemStack = nextItemStack.Clone();
                int stackSize = nextItemStack.StackSize;
                if (!byPlayer.InventoryManager.TryGiveItemstack(nextItemStack))
                {
                    world.SpawnItemEntity(nextItemStack, blockSel.Position);
                }

                world.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.", byPlayer.PlayerName, stackSize, nextItemStack.Collectible.Code, block.Code, blockSel.Position);
                TreeAttribute data = new TreeAttribute
                {
                    ["itemstack"] = new ItemstackAttribute(itemStack.Clone()),
                    ["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId)
                };
                world.Api.Event.PushEvent("onitemcollected", data);
            }
        });
        if (harvestedBlock != null)
        {
            if (!exchangeBlock)
            {
                world.BlockAccessor.SetBlock(harvestedBlock.BlockId, blockSel.Position);
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntitySaguaro saguaro)
                {
                    saguaro.onHarvested(world);
                }
            }
            else
            {
                world.BlockAccessor.ExchangeBlock(harvestedBlock.BlockId, blockSel.Position);
                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (be == null)
                {
                    world.BlockAccessor.SpawnBlockEntity("SaguaroCactus", blockSel.Position);
                    be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                }
                if (be is BlockEntitySaguaro saguaro)
                {
                    saguaro.onHarvested(world);
                }
            }
        }

        if (Tool.HasValue)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            activeHotbarSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, activeHotbarSlot);
        }

        world.PlaySoundAt(harvestingSound, blockSel.Position, 0.0, byPlayer);
    }
}
