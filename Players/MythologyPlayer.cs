using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using AncientChineseMythology.Content;
using System;

namespace AncientChineseMythology.Players;

public class MythologyPlayer : ModPlayer
{
    public int Major;
    public int Minor;
    public int StageExp;
    public int KillsThisMajor;

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
            TryMajorAdvance();
        }
    }

    private void TryMinorAdvance()
    {
        // 只要还没到“3”（大圆满），并且经验足够，就升一级
        while (Minor < CultivationProgression.MinorPerMajor - 1)
        {
            int needExp = CultivationProgression.ExpFor(Major, Minor);
            if (StageExp < needExp)
                break;

            // 完全清空本级经验（不保留溢出）
            StageExp = 0;
            Minor++;
            ApplyMinorBonus();
        }
    }

    private void TryMajorAdvance()
    {
        // 必须先达到大圆满
        if (Minor != CultivationProgression.MinorPerMajor - 1) return;

        int needExp = CultivationProgression.ExpFor(Major, Minor);
        if (StageExp < needExp) return;                             // 阶段经验要满
        if (KillsThisMajor < CultivationProgression.KillsForMajorUp[Major]) return; // 击杀要够

        // 晋升
        Major++;
        Minor           = 0;
        StageExp        = 0;  // <- 小境界经验归零
        KillsThisMajor  = 0;

        ApplyMajorBonus();
    }

    private void ApplyMinorBonus()
    {
        var baseBonus = CultivationProgression.GetMinorBonusBase(Major);

        // 小境界每升一级，就叠加一次基准值
        Player.statLifeMax2       += baseBonus.hp;
        Player.statManaMax2       += baseBonus.mana;
        Player.statDefense        += baseBonus.def;
        Player.GetDamage(DamageClass.Generic) += baseBonus.dmg;
    }

    private void ApplyMajorBonus()
    {
        // 确保 Major 在合法区间 0～15
        if (Major < 0 || Major >= CultivationProgression.MajorHealthBonusTable.Length)
            return;

        Player.statLifeMax2       += CultivationProgression.MajorHealthBonusTable[Major];
        Player.statManaMax2       += CultivationProgression.MajorManaBonusTable[Major];
        Player.statDefense        += CultivationProgression.MajorDefenseBonusTable[Major];
        Player.GetDamage(DamageClass.Generic) += CultivationProgression.MajorDamageBonusTable[Major];
    }

    public override void ResetEffects()
    {
        // 持续生效：小境界总加成 = Minor * 基准
        var mb = CultivationProgression.GetMinorBonusBase(Major);
        Player.statLifeMax2       += Minor * mb.hp;
        Player.statManaMax2       += Minor * mb.mana;
        Player.statDefense        += Minor * mb.def;
        Player.GetDamage(DamageClass.Generic) += Minor * mb.dmg;

        // 持续生效：大境界本级加成（但不叠加 previous major）
        if (Major >= 0 && Major < CultivationProgression.MajorHealthBonusTable.Length)
        {
            Player.statLifeMax2       += CultivationProgression.MajorHealthBonusTable[Major];
            Player.statManaMax2       += CultivationProgression.MajorManaBonusTable[Major];
            Player.statDefense        += CultivationProgression.MajorDefenseBonusTable[Major];
            Player.GetDamage(DamageClass.Generic) += CultivationProgression.MajorDamageBonusTable[Major];
        }
    }

    public override void SaveData(TagCompound tag)
    {
        tag["Major"]         = Major;
        tag["Minor"]         = Minor;
        tag["StageExp"]      = StageExp;
        tag["KillsThisMajor"]= KillsThisMajor;
    }

    public override void LoadData(TagCompound tag)
    {
        Major          = tag.GetInt("Major");
        Minor          = tag.GetInt("Minor");
        StageExp       = tag.GetInt("StageExp");
        KillsThisMajor = tag.GetInt("KillsThisMajor");
    }
}
