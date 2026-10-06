using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Saguaros;

// I Based this off of the BlockEntitySapling code, lots of it is directly copied and only slightly modified.

public class BlockEntitySaguaro: BlockEntity
{
    private double totalHoursTillGrowth = -1;
    private NormalRandom normalRandom;
    private long growListenerId;
    private long ageYears = 0;
    private bool isTip = false;
    private double nextStuntTime = 0;
    private EnumSaguaroGrowthStage stage = EnumSaguaroGrowthStage.UNDETERMINED;

    private static Block? topEmpty;
    private static Block? topFlowering;
    private static Block? topRipe;
    private static Block? segment;
    private static Block? branching;


    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (!(api is ICoreServerAPI))
        {
            return;
        }
        if (topEmpty == null)
        {
            IBlockAccessor blockAccessor = api.World.BlockAccessor;
            topFlowering = blockAccessor.GetBlock(Block.CodeWithParts("topflowering"));
            topRipe = blockAccessor.GetBlock(Block.CodeWithParts("topripe"));
            segment = blockAccessor.GetBlock(Block.CodeWithParts("segment"));
            branching = blockAccessor.GetBlock(Block.CodeWithParts("branchysegment"));
            topEmpty = blockAccessor.GetBlock(Block.CodeWithParts("topempty"));
        }
        normalRandom = new NormalRandom(api.World.Seed-(Pos.X*Pos.Y*Pos.Z)-(int)api.World.Calendar.TotalHours);
        growListenerId = this.RegisterGameTickListener(new Action<IWorldAccessor,BlockPos,float>(this.CheckGrow), 2000);


    }

    private float GrowthRateMod => this.Api.World.Config.GetString("saplingGrowthRate").ToFloat(1f);
    private NatFloat nextStageDaysRnd
    {
        get
        {
            NatFloat defaultValue;
            if (stage == EnumSaguaroGrowthStage.BUD)
            {
                defaultValue = NatFloat.create(EnumDistribution.UNIFORM, 7.5f, 1.5f);
                return Block?.Attributes != null ? Block.Attributes["budDays"].AsObject<NatFloat>(defaultValue) : defaultValue;
            } else if (stage == EnumSaguaroGrowthStage.YOUNG)
            {
                defaultValue = NatFloat.create(EnumDistribution.UNIFORM, 22.5F, 1f);
                return Block?.Attributes != null ? Block.Attributes["youngDays"].AsObject<NatFloat>(defaultValue) : defaultValue;
            } else if (stage == EnumSaguaroGrowthStage.JUVENILE)
            {
                defaultValue = NatFloat.create(EnumDistribution.UNIFORM, 22.5F , 0.9f);
                return Block?.Attributes != null ? Block.Attributes["juvenileDays"].AsObject<NatFloat>(defaultValue) : defaultValue;
            }
            defaultValue = NatFloat.create(EnumDistribution.UNIFORM, 7.5F, 0.5f);
            return Block?.Attributes != null ? Block.Attributes["floweringDays"].AsObject<NatFloat>(defaultValue) : defaultValue;
        }
    }

    private void DetermineGrowthStage(IWorldAccessor world)
    {
        int n = 1;
        while(world.BlockAccessor.GetBlockBelow(Pos.Copy(), n) is BlockCactus)
        {
            n++;
        }
        
        if (n == 1)
        {
            stage = EnumSaguaroGrowthStage.BUD;
        }
        else if (n == 2)
        {
            stage = EnumSaguaroGrowthStage.JUVENILE;
        }
        else if (n >= 3)
        {
            stage = EnumSaguaroGrowthStage.ADULT;
        }
        

        while(n > 0)
        {
            var be = world.BlockAccessor.GetBlockEntity(Pos.Copy().Down(n));
            if (be is BlockEntitySaguaro saguaro)
            {
                saguaro.stage = stage;
                saguaro.totalHoursTillGrowth = -1;
                saguaro.isTip = false;
            }
            n--;
        }
        var above = world.BlockAccessor.GetBlockAbove(Pos.Copy());
        if (above is not BlockCactus)
        {
            isTip = true;
        }
        else
        {
            totalHoursTillGrowth = -1;
        }
    }

    private bool TryPlace(IBlockAccessor blockAccessor, Block block, BlockPos pos)
    {
        Block b = blockAccessor.GetBlock(pos);
        if (b.IsReplacableBy(block))
        {
            blockAccessor.SetBlock(block.Id, pos);
            return true;
        }
        return false;
    }
    private bool Grow(IWorldAccessor world)
    {
        IBlockAccessor blockAccessor = world.BlockAccessor;
        if (stage == EnumSaguaroGrowthStage.BUD)
        {
            stage = EnumSaguaroGrowthStage.YOUNG;
        } else if(stage == EnumSaguaroGrowthStage.YOUNG)
        {
            if (TryPlace(blockAccessor, topEmpty!, Pos.Copy().Up()))
            {
                stage = EnumSaguaroGrowthStage.JUVENILE;
                blockAccessor.ExchangeBlock(segment!.Id,Pos.Copy());
            }
        }
        else if (stage == EnumSaguaroGrowthStage.JUVENILE)
        {
            stage = EnumSaguaroGrowthStage.ADULT;
            // 20% chance to not grow up at all.
            if (normalRandom.NextFloat() <= 0.8)
            {
                // Grows at least 1 block, Chance to grow an additional 1-2 blocks.
                // TODO: make this affected by environment and soil.
                int size = (int)Math.Floor(normalRandom.NextFloat() * 1.25F) + (int)Math.Floor(normalRandom.NextFloat() * 1.1F) + 1;
                for(int i = 1; i <= size; i++)
                {
                    if (TryPlace(blockAccessor,segment!,Pos.Copy().Up(i))){
                        if (i == size)
                        {
                            blockAccessor.ExchangeBlock(topEmpty!.Id, Pos.Copy().Up(i));
                        }
                    }
                    else
                    {
                        if (i == 1)
                        {
                            stage = EnumSaguaroGrowthStage.BRANCHY;
                        }
                        break;
                    }
                }
                if(stage != EnumSaguaroGrowthStage.BRANCHY)
                {
                    blockAccessor.ExchangeBlock(segment!.Id, Pos.Copy());
                }
            }
            else
            {
                stage = EnumSaguaroGrowthStage.BRANCHY;
            }
        }
        else if(stage == EnumSaguaroGrowthStage.ADULT || stage == EnumSaguaroGrowthStage.BRANCHY)
        {
            if (blockAccessor.GetBlockId(Pos.Copy()) == topFlowering!.Id)
            {
                totalHoursTillGrowth = world.Calendar.TotalHours + (normalRandom.NextInt(8)+16) * GrowthRateMod;
                blockAccessor.ExchangeBlock(topRipe!.Id, Pos.Copy());
            }
            else if (blockAccessor.GetBlockId(Pos.Copy()) != topRipe!.Id)
            {
                blockAccessor.ExchangeBlock(topFlowering!.Id, Pos.Copy());
                if (stage != EnumSaguaroGrowthStage.BRANCHY)
                {
                    // 30% chance to not grow branches
                    if (normalRandom.NextFloat() < 0.7F)
                    {
                        blockAccessor.ExchangeBlock(branching!.Id, Pos.Copy().Down());
                    }
                    stage = EnumSaguaroGrowthStage.BRANCHY;
                }
                return false;
            }
        }
        return true;
    }

    public void onHarvested(IWorldAccessor world)
    {
        totalHoursTillGrowth = world.Calendar.TotalHours + nextStageDaysRnd.nextFloat(1.5f, Api.World.Rand) * 24.0D * GrowthRateMod;
        MarkDirty();
    }

    private IWorldAccessor? world;
    private void CheckGrow(IWorldAccessor world, BlockPos pos, float dt)
    {
        this.world = world;
        if(stage == EnumSaguaroGrowthStage.UNDETERMINED)
        {
            DetermineGrowthStage(world);
            MarkDirty();
        }
        if (!isTip)
        {
            UnregisterGameTickListener(growListenerId);
            return;
        }
        var timeHours = world.Calendar.TotalHours;
        var climate = world.BlockAccessor.GetClimateAt(pos);
        // According to my very brief google research saguaros can survive above ~-13 degrees celcius, below this they struggle.
        // https://cactiguide.com/forum/viewtopic.php?t=20353
        if (climate.Temperature > -13)
        {
            if(totalHoursTillGrowth == -1)
            {
                totalHoursTillGrowth = timeHours + nextStageDaysRnd.nextFloat(1f, Api.World.Rand) * 24.0D * GrowthRateMod;
                MarkDirty();
            }
            else if(timeHours >= totalHoursTillGrowth)
            {
                timeHours -= totalHoursTillGrowth;
                while (timeHours >= 0)
                {
                    if (Grow(world))
                    {
                        totalHoursTillGrowth = timeHours + nextStageDaysRnd.nextFloat(1f, Api.World.Rand) * 24.0D * GrowthRateMod;
                    }
                    timeHours -= totalHoursTillGrowth;
                }
                MarkDirty();
            }
        } else if(timeHours >= nextStuntTime) {
            totalHoursTillGrowth += 1;
            nextStuntTime = timeHours + 1;
            MarkDirty();
        }
    }
    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetDouble("totalHoursTillGrowth", totalHoursTillGrowth);
        tree.SetDouble("nextStuntTime", nextStuntTime);
        tree.SetInt("growthStage", (int)stage);
        tree.SetBool("isTip", isTip);
    }
    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        totalHoursTillGrowth = tree.GetDouble("totalHoursTillGrowth");
        nextStuntTime = tree.GetDouble("nextStuntTime");
        stage = (EnumSaguaroGrowthStage)tree.GetInt("growthStage", 1);
        isTip = tree.GetBool("isTip");
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        if(world?.BlockAccessor.GetBlockEntity(Pos.Copy().Down()) is BlockEntitySaguaro saguaro){
            saguaro.stage = EnumSaguaroGrowthStage.UNDETERMINED;
            saguaro.RegisterGameTickListener(saguaro.CheckGrow,10000);
        }
    }
}

// Comments based on https://www.nps.gov/sagu/learn/nature/saguaro-growth.htm
enum EnumSaguaroGrowthStage
{
    // a few inches tall, usually 5-10 years old. Less than a block in size
    // Not yet used
    BUD,
    // 2-4 feet tall, 20-40 years old. One block in size.
    YOUNG,
    // 2 blocks in size
    JUVENILE,
    // 3 blocks in size. Starts flowering ~30-70 yrs old.
    ADULT,
    BRANCHY,
    UNDETERMINED
}