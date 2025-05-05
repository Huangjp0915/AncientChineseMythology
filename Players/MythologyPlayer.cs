using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using AncientChineseMythology.Content;
using System;
using Microsoft.Xna.Framework;
using AncientChineseMythology.Buffs;
using AncientChineseMythology.Items.Summons;
using AncientChineseMythology.Items;

namespace AncientChineseMythology.Players;

public class MythologyPlayer : ModPlayer
{
    public int Major;
    public int Minor;
    public int StageExp;
    public int KillsThisMajor;
    public bool GotRenXianRewards;

    public int GetResourceTier() {
        int lifeMax = Player.statLifeMax2;  // 或你的自定义血量字段
        return lifeMax switch {
            <= 1000      => 0,
            <= 10000     => 1,
            <= 100000    => 2,
            _            => 3
        };
    }

    public override void PostUpdate()
    {
        TryGiveRenXianRewards();
    }

    public void RecordKill(NPC npc)
    {
        if (npc.boss || npc.friendly) return;

        // ------ 处理经验（只到达本阶段需求即停止） ------
        int expNeed = CultivationProgression.ExpFor(Major, Minor);
        if (StageExp < expNeed)
        {
            int gained = (int)(npc.lifeMax * 0.3f);
            // 累加但不超过阈值
            StageExp = Math.Min(StageExp + gained, expNeed);
            TryMinorAdvance();
        }

        // ------ 处理击杀数（只到达本大境界阈值即停止） ------
        int killNeed = CultivationProgression.KillsForMajorUp[Major];
        if (KillsThisMajor < killNeed)
        {
            KillsThisMajor++;
            //TryMajorAdvance();
        }
    }

    // 判定是否符合大境界突破条件
    public bool CanMajorAdvance() {
        // 大境界已封顶？
        int maxMajor = CultivationProgression.MajorNames.Length - 1;
        if (Major >= maxMajor) return false;

        // 必须小境界大圆满
        if (Minor != CultivationProgression.MinorPerMajor - 1) return false;

        // 经验与击杀必须达标
        if (StageExp < CultivationProgression.ExpFor(Major, Minor))                return false;
        if (KillsThisMajor < CultivationProgression.KillsForMajorUp[Major])        return false;

        return true;
    }

    public void AdvanceMajor(Player player) {
        if (!CanMajorAdvance()) return;

        Major++;
        Minor = 0;
        StageExp = 0;
        KillsThisMajor = 0;
        ApplyMajorBonus();                           // 原有奖励逻辑
        TryGiveRenXianRewards();
        player.statLife = player.statLifeMax2;      
        CombatText.NewText(player.getRect(), Color.Gold, "突破成功!");
    }

    private void TryMinorAdvance()
    {
        // 只要还没到“3”（大圆满），并且经验足够，就升一级
        while (Minor < CultivationProgression.MinorPerMajor - 1)
        {
            int needExp = CultivationProgression.ExpFor(Major, Minor);
            if (StageExp < needExp)
                break;

            // 完全清空本级经验
            StageExp = 0;
            Minor++;
            ApplyMinorBonus();
        }
    }

    public void AddStageExp(int amount)
    {
        int need = CultivationProgression.ExpFor(Major, Minor);
        StageExp = Math.Min(StageExp + amount, need);
        TryMinorAdvance();
    }

    public bool ForceMajorAdvance()
    {
        // 1. 若已到最高大境界，提示并返回
        int maxMajor = CultivationProgression.MajorNames.Length - 1;
        if (Major >= maxMajor)
        {
            Main.NewText("已经达到最高大境界，无法再破境。", 200, 50, 50);
            return false;
        }

        // 2. 直接把击杀数补满（突破丹效果）
        int killsRequired = CultivationProgression.KillsForMajorUp[Major];
        KillsThisMajor = killsRequired;
        return true;
    }

    private void TryGiveRenXianRewards()
    {
        // Major==4 就是“人仙”
        if (Major == 4 && !GotRenXianRewards)
        {
            Player.QuickSpawnItem(
                Player.GetSource_GiftOrReward(),
                ModContent.ItemType<ShenxianGuanglunItem>());

            Player.QuickSpawnItem(
                Player.GetSource_GiftOrReward(),
                ModContent.ItemType<CloudMountItem>());

            GotRenXianRewards = true;
        }
    }

    private void ApplyMinorBonus()
    {
        var baseBonus = CultivationProgression.GetMinorBonusBase(Major);

        // 小境界每升一级，就叠加一次基准值
        Player.statDefense        += baseBonus.def;
        Player.GetDamage(DamageClass.Generic) += baseBonus.dmg;
    }

    public void ApplyMajorBonus()
    {
        // 确保 Major 在合法区间 0～15
        if (Major < 0 || Major >= CultivationProgression.MajorHealthBonusTable.Length)
            return;


        Player.statDefense        += CultivationProgression.MajorDefenseBonusTable[Major];
        Player.GetDamage(DamageClass.Generic) += CultivationProgression.MajorDamageBonusTable[Major];
    }

    public override void ResetEffects()
    {
        // 持续生效：小境界总加成 = Minor * 基准
        var mb = CultivationProgression.GetMinorBonusBase(Major);
        Player.statDefense        += Minor * mb.def;
        Player.GetDamage(DamageClass.Generic) += Minor * mb.dmg;

        // 持续生效：大境界本级加成（但不叠加 previous major）
        if (Major >= 0 && Major < CultivationProgression.MajorHealthBonusTable.Length)
        {
            Player.statDefense        += CultivationProgression.MajorDefenseBonusTable[Major];
            Player.GetDamage(DamageClass.Generic) += CultivationProgression.MajorDamageBonusTable[Major];
        }
    }

    public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
    {
        // 从默认无加成开始（乘算=1，Flat=0）
        health = StatModifier.Default;
        mana   = StatModifier.Default;

        // —— 小境界加成 ——
        var minorBase = CultivationProgression.GetMinorBonusBase(Major);
        health.Flat += minorBase.hp   * Minor;
        mana.Flat   += minorBase.mana * Minor;

        // —— 大境界加成 ——
        if (Major >= 0 && Major < CultivationProgression.MajorHealthBonusTable.Length)
        {
            health.Flat += CultivationProgression.MajorHealthBonusTable[Major];
            mana.Flat   += CultivationProgression.MajorManaBonusTable[Major];
        }
    }

    public override void SaveData(TagCompound tag)
    {
        tag["Major"]         = Major;
        tag["Minor"]         = Minor;
        tag["StageExp"]      = StageExp;
        tag["KillsThisMajor"]= KillsThisMajor;
        tag["GotRenXianRewards"] = GotRenXianRewards;
    }

    public override void LoadData(TagCompound tag)
    {
        Major          = tag.GetInt("Major");
        Minor          = tag.GetInt("Minor");
        StageExp       = tag.GetInt("StageExp");
        KillsThisMajor = tag.GetInt("KillsThisMajor");
        GotRenXianRewards = tag.GetBool("GotRenXianRewards"); 
    }
}
