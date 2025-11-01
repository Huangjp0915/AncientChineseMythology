using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static AncientChineseMythology.Systems.AncientChineseMythologyNetwork;

namespace AncientChineseMythology.Players
{
    public class BaGuaPlayer : ModPlayer
    {
        //常量：倍率
        private const float Scale = 0.10f;
        public const int SlotCount = 8;
        public Item[] BaGuaItems = new Item[SlotCount];
        public string CurrentName = "";
        public string CurrentDesc = "";
        private const int WearInterval = 60 * 60 * 30;      //30
        private int[] wearCounter = new int[SlotCount];

        /*  ----------------- 阵法内定义 ----------------- */
        private bool phoenixActive;
        private int phoenixCD;
        private const int PhoenixCDMax = 60 * 60 * 10; //10 min冷却
        private static readonly int[] BoomerangIDs = {
            ProjectileID.Flamarang,
            ProjectileID.EnchantedBoomerang,
            ProjectileID.WoodenBoomerang,
            ProjectileID.IceBoomerang,
            ProjectileID.Shroomerang
        };
        
        // 高级剑列表（用于万剑归宗阵）
        private static readonly int[] AdvancedSwords = [
            ItemID.Excalibur,
            ItemID.TrueExcalibur,
            ItemID.TerraBlade,
            ItemID.InfluxWaver,
            ItemID.TheHorsemansBlade,
            ItemID.Meowmere,
            ItemID.StarWrath,
            ItemID.Zenith,
            ItemID.Keybrand,
            ItemID.FlyingKnife,
            ItemID.ChlorophyteSaber,
            ItemID.CrystalVileShard
        ];
        
        // 精金/钛金盔甲部件列表（用于防御类阵法）
        private static readonly int[] AdamantiteTitaniumArmor = [
            ItemID.AdamantiteHeadgear,
            ItemID.AdamantiteHelmet,
            ItemID.AdamantiteMask,
            ItemID.AdamantiteBreastplate,
            ItemID.AdamantiteLeggings,
            ItemID.TitaniumHeadgear,
            ItemID.TitaniumHelmet,
            ItemID.TitaniumMask,
            ItemID.TitaniumBreastplate,
            ItemID.TitaniumLeggings
        ];
        
        // 忍者大师装备列表（用于移动速度类阵法）
        private static readonly int[] NinjaGearItems = [
            ItemID.NinjaHood,
            ItemID.NinjaShirt,
            ItemID.NinjaPants
        ];
        
        // 翅膀列表（用于移动速度类阵法）
        // 注意：使用更通用的方法检测翅膀，通过检查物品是否为翅膀类型
        private static bool IsWings(int itemType) {
            if (itemType <= 0 || itemType >= ItemLoader.ItemCount) return false;
            Item item = new Item();
            item.SetDefaults(itemType);
            // 检查物品是否为翅膀（通过accessory和wingSlot属性）
            return item.accessory && item.wingSlot > 0;
        }
        
        // 高级翅膀列表（用于咫尺天涯阵）
        // 高级翅膀：血肉之墙（困难模式）后的翅膀
        private static bool IsAdvancedWings(int itemType) {
            if (!IsWings(itemType)) return false;
            try {
                Item item = new Item();
                item.SetDefaults(itemType);
                // 困难模式（血肉之墙后）的翅膀通常稀有度为橙色或更高
                // 困难模式前的翅膀（天使翅膀、恶魔翅膀）稀有度通常是蓝色或白色
                // 所以判断稀有度 >= 橙色（ItemRarityID.Orange = 5）即为困难模式后的翅膀
                return item.rare >= ItemRarityID.Orange;
            }
            catch {
                return false;
            }
        }
        
        private int FallingStarTimer;
        private const int FallingStarCD = 15;   //每 15 tick ≈ 0.25 s 召唤 1 颗
        
        // 甘露阵：每15秒回复最大生命值的1%
        private int GanLuTimer;
        private const int GanLuCD = 60 * 15; // 15秒 = 900 ticks
        
        // 长生阵：每12秒回复最大生命值的2%
        private int ChangShengTimer;
        private const int ChangShengCD = 60 * 12; // 12秒 = 720 ticks
        
        // 回春阵：每10秒回复最大生命值的3%
        private int HuiChunTimer;
        private const int HuiChunCD = 60 * 10; // 10秒 = 600 ticks
        
        // 太乙还魂阵：每8秒回复最大生命值的5%
        private int TaiYiHuanHunTimer;
        private const int TaiYiHuanHunCD = 60 * 8; // 8秒 = 480 ticks
        
        // 生生不息阵：每10秒回复最大生命值的1% + 固定回复2点
        private int ShengShengBuXiTimer;
        private const int ShengShengBuXiCD = 60 * 10; // 10秒 = 600 ticks
        
        // 九转金丹阵：每8秒回复最大生命值的6%
        private int JiuZhuanJinDanTimer;
        private const int JiuZhuanJinDanCD = 60 * 8; // 8秒 = 480 ticks
        
        // 阴阳调和阵：每6秒回复最大生命值的8%
        private int YinYangTiaoHeTimer;
        private const int YinYangTiaoHeCD = 60 * 6; // 6秒 = 360 ticks
        
        // 万象回春阵：每10秒回复最大生命值的2%，生命值低于50%时提升至每6秒回复5%
        private int WanXiangHuiChunTimer;
        private const int WanXiangHuiChunCDNormal = 60 * 10; // 10秒 = 600 ticks（正常状态）
        private const int WanXiangHuiChunCDLow = 60 * 6; // 6秒 = 360 ticks（低血量状态）
        
        // 神农护体阵：每10秒回复最大生命值的4%，并免疫中毒、流血、着火状态
        private int ShenNongHuTiTimer;
        private const int ShenNongHuTiCD = 60 * 10; // 10秒 = 600 ticks
        
        // 聚灵阵：每15秒回复最大魔力值的2%
        private int JuLingTimer;
        private const int JuLingCD = 60 * 15; // 15秒 = 900 ticks
        
        // 灵泉阵：每12秒回复最大魔力值的4%
        private int LingQuanTimer;
        private const int LingQuanCD = 60 * 12; // 12秒 = 720 ticks
        
        // 星辰聚灵阵：每10秒回复最大魔力值的6%
        private int XingChenJuLingTimer;
        private const int XingChenJuLingCD = 60 * 10; // 10秒 = 600 ticks
        
        // 太乙聚灵阵：每8秒回复最大魔力值的8%
        private int TaiYiJuLingTimer;
        private const int TaiYiJuLingCD = 60 * 8; // 8秒 = 480 ticks
        
        // 法力潮汐阵：每10秒回复最大魔力值的4%，使用魔法武器时额外回复每15秒1%
        private int FaLiChaoXiTimer;
        private int FaLiChaoXiExtraTimer; // 额外回复计时器（使用魔法武器时）
        private const int FaLiChaoXiCD = 60 * 10; // 10秒 = 600 ticks
        private const int FaLiChaoXiExtraCD = 60 * 15; // 15秒 = 900 ticks（额外回复）
        
        // 鸿蒙灵源阵：每6秒回复最大魔力值的10%
        private int HongMengLingYuanTimer;
        private const int HongMengLingYuanCD = 60 * 6; // 6秒 = 360 ticks
        
        // 先天不败阵：致命伤害保护冷却（10分钟）
        private int XianTianBuBaiCD;
        private const int XianTianBuBaiCDMax = 60 * 60 * 10; // 10分钟 = 36000 ticks
        private bool XianTianBuBaiActive;
        
        // 混元无极阵：每10秒回复3点生命值
        private int HunYuanWuJiTimer;
        private const int HunYuanWuJiCD = 60 * 10; // 10秒 = 600 ticks
        
        // 缩地成寸阵：冲刺冷却
        private int SuoDiChengCunDashCD;
        private const int SuoDiChengCunDashCDMax = 60 * 15; // 15秒 = 900 ticks
        private bool SuoDiChengCunDashActive;
        private int SuoDiChengCunDashDuration;
        private const int SuoDiChengCunDashDurationMax = 60; // 1秒冲刺持续时间
        
        // 腾云驾雾阵：空中停留
        private int TengYunJiaWuHoverTimer;
        private const int TengYunJiaWuHoverMax = 60 * 2; // 2秒 = 120 ticks
        
        // 咫尺天涯阵：连续冲刺
        private int ChiZhiTianYaDashCD;
        private const int ChiZhiTianYaDashCDMax = 60 * 2; // 2秒冷却
        private bool ChiZhiTianYaDashActive;
        private int ChiZhiTianYaDashDuration;
        private const int ChiZhiTianYaDashDurationMax = 60; // 1秒冲刺持续时间
        
        // 万兽朝宗阵：召唤物持续伤害计时器
        private int WanShouChaoZongTimer;
        private const int WanShouChaoZongCD = 60; // 1秒 = 60 ticks
        
        // 神魔降世阵：特殊攻击概率
        private const float ShenMoJiangShiSpecialChance = 0.15f; // 15%概率触发特殊攻击
        
        // 混沌万灵阵：范围伤害计时器
        private int HunDunWanLingTimer;
        private const int HunDunWanLingCD = 60 * 2; // 2秒 = 120 ticks
        
        // ───────── 特殊效果类阵法计时器和变量 ─────────
        // 隐身遁形阵：隐身状态和冷却
        private int YinShenDunXingTimer;
        private int YinShenDunXingCD;
        private const int YinShenDunXingDuration = 60 * 8; // 8秒 = 480 ticks
        private const int YinShenDunXingCDMax = 60 * 45; // 45秒 = 2700 ticks
        private bool YinShenDunXingActive;
        
        // 烈焰焚天阵：持续火焰伤害计时器
        private int LieYanFenTianTimer;
        private const int LieYanFenTianCD = 60; // 1秒 = 60 ticks
        
        // 寒冰封天阵：冰冻效果在OnHitNPCWithItem中处理
        
        // 雷霆万钧阵：连锁闪电效果在OnHitNPCWithItem中处理
        
        // 八卦推演阵：地图显示效果（需要持续激活）
        
        // 时空扭曲阵：使用时间缩短和魔力消耗减少在GlobalItem中处理
        
        // 吞噬万物阵：击败敌人回复（在OnKillNPC中处理）
        
        // ───────── 综合强化类阵法计时器 ─────────
        // 三才合一阵：无计时器，在ResetEffects中处理
        
        // 五行相生阵：回复计时器
        private int WuXingXiangShengTimer;
        private const int WuXingXiangShengCD = 60 * 15; // 15秒 = 900 ticks
        
        // 六合归一阵：回复计时器
        private int LiuHeGuiYiTimer;
        private const int LiuHeGuiYiCD = 60 * 12; // 12秒 = 720 ticks
        
        // 太极混元阵：回复计时器
        private int TaiJiHunYuanTimer;
        private const int TaiJiHunYuanCD = 60 * 10; // 10秒 = 600 ticks

        
        public override void PostUpdateEquips() {
            //若拥有八卦 Buff，则缩放最终生命 / 魔力
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                //1. 缩放最大值
                Player.statLifeMax2 = (int)(Player.statLifeMax2 * Scale);
                Player.statManaMax2 = (int)(Player.statManaMax2 * Scale);

                //2. 防止当前值溢出新上限
                if (Player.statLife > Player.statLifeMax2)
                    Player.statLife = Player.statLifeMax2;

                if (Player.statMana > Player.statManaMax2)
                    Player.statMana = Player.statManaMax2;
            }
        }

        // ───────── 攻击类阵法效果 ─────────
        public override void ModifyWeaponDamage(Item item, ref StatModifier damage) {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) return;
            
            int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            if (cur == null) return;
            
            // ───────── 召唤类阵法效果：召唤伤害加成 ─────────
            if (item.DamageType == DamageClass.Summon) {
                // 百兽召唤阵：召唤伤害+8%
                if (CheckBaiShouZhaoHuanFormation(cur)) {
                    damage *= 1.08f;
                    return;
                }
                // 天兵天将阵：召唤伤害+15%
                else if (CheckTianBingTianJiangFormation(cur)) {
                    damage *= 1.15f;
                    return;
                }
                // 万兽朝宗阵：召唤伤害+22%
                else if (CheckWanShouChaoZongFormation(cur)) {
                    damage *= 1.22f;
                    return;
                }
                // 神魔降世阵：召唤伤害+35%
                else if (CheckShenMoJiangShiFormation(cur)) {
                    damage *= 1.35f;
                    return;
                }
                // 混沌万灵阵：召唤伤害+50%
                else if (CheckHunDunWanLingFormation(cur)) {
                    damage *= 1.50f;
                    return;
                }
            }
            
            // ───────── 攻击类阵法效果（非召唤类） ─────────
            // 锋芒阵：近战伤害+3%
            if (CheckFengMangFormation(cur)) {
                if (item.DamageType == DamageClass.Melee) {
                    damage *= 1.03f;
                }
            }
            // 破军阵：近战伤害+8%，近战速度+5%
            else if (CheckPoJunFormation(cur)) {
                if (item.DamageType == DamageClass.Melee) {
                    damage *= 1.08f;
                }
            }
            // 诛邪阵：所有伤害+8%，暴击率+3%
            else if (CheckZhuXieFormation(cur)) {
                damage *= 1.08f;
            }
            // 万剑归宗阵：近战伤害+15%
            else if (CheckWanJianGuiZongFormation(cur)) {
                if (item.DamageType == DamageClass.Melee) {
                    damage *= 1.15f;
                }
            }
            // 天罡破阵：所有伤害+12%，暴击率+5%，攻击速度+8%
            else if (CheckTianGangPoFormation(cur)) {
                damage *= 1.12f;
            }
            // 混沌灭世阵：所有伤害+18%，暴击率+8%
            else if (CheckHunDunMieShiFormation(cur)) {
                damage *= 1.18f;
            }
            // 盘古开天阵：所有伤害+22%，暴击率+12%
            else if (CheckPanGuKaiTianFormation(cur)) {
                damage *= 1.22f;
            }
            // 弑神诛魔阵：所有伤害+30%，暴击率+15%
            else if (CheckShiShenZhuMoFormation(cur)) {
                damage *= 1.30f;
            }
            // 三才合一阵：所有伤害+10%
            else if (CheckSanCaiHeYiFormation(cur)) {
                damage *= 1.10f;
            }
            // 五行相生阵：所有伤害+12%
            else if (CheckWuXingXiangShengFormation(cur)) {
                damage *= 1.12f;
            }
            // 六合归一阵：所有伤害+15%
            else if (CheckLiuHeGuiYiFormation(cur)) {
                damage *= 1.15f;
            }
            // 太极混元阵：所有伤害+20%
            else if (CheckTaiJiHunYuanFormation(cur)) {
                damage *= 1.20f;
            }
        }
        
        // ───────── 召唤类阵法效果：召唤物上限增加（在ResetEffects中处理） ─────────

        // ───────── 时空扭曲阵：使用时间缩短15% ─────────
        public override float UseTimeMultiplier(Item item) {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) return 1f;
            
            int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            if (cur == null) return 1f;
            
            // 时空扭曲阵：使用时间缩短15%（乘以0.85）
            if (CheckShiKongNiuQuFormation(cur)) {
                return 0.85f;
            }
            
            return 1f;
        }

        public override void ModifyWeaponCrit(Item item, ref float crit) {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) return;
            
            int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            if (cur == null) return;
            
            // 诛邪阵：暴击率+3%
            if (CheckZhuXieFormation(cur)) {
                crit += 3f;
            }
            // 天罡破阵：暴击率+5%
            else if (CheckTianGangPoFormation(cur)) {
                crit += 5f;
            }
            // 混沌灭世阵：暴击率+8%
            else if (CheckHunDunMieShiFormation(cur)) {
                crit += 8f;
            }
            // 盘古开天阵：暴击率+12%
            else if (CheckPanGuKaiTianFormation(cur)) {
                crit += 12f;
            }
            // 弑神诛魔阵：暴击率+15%
            else if (CheckShiShenZhuMoFormation(cur)) {
                crit += 15f;
            }
        }

        // 注意：攻击速度修改在Global/BaGuaAttackSpeedGlobalItem.cs中处理

        // 混沌灭世阵的效果在OnHitNPCWithItem中处理
        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) return;
            
            int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            if (cur == null) return;
            
            // 弑神诛魔阵的对boss额外伤害在ModifyHitNPCWithProj中处理
            
            // 寒冰封天阵：攻击有15%概率冰冻敌人
            if (CheckHanBingFengTianFormation(cur)) {
                if (Main.rand.NextFloat() < 0.15f) {
                    target.AddBuff(BuffID.Frozen, 60 * 3); // 冰冻3秒
                }
            }
            
            // 雷霆万钧阵：攻击有12%概率触发连锁闪电
            if (CheckLeiTingWanJunFormation(cur)) {
                if (Main.rand.NextFloat() < 0.12f) {
                    // 创建连锁闪电效果
                    // 寻找附近的敌人
                    float chainDistance = 200f;
                    NPC chainTarget = null;
                    float closestDistance = chainDistance;
                    
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC npc = Main.npc[i];
                        if (npc != null && npc.active && !npc.friendly && npc != target) {
                            float distance = (npc.Center - target.Center).Length();
                            if (distance < closestDistance && distance > 0) {
                                closestDistance = distance;
                                chainTarget = npc;
                            }
                        }
                    }
                    
                    if (chainTarget != null) {
                        // 创建闪电伤害
                        int lightningDamage = (int)(damageDone * 0.5f); // 连锁闪电造成50%原伤害
                        chainTarget.StrikeNPC(new NPC.HitInfo {
                            Damage = lightningDamage,
                            HitDirection = Player.Center.X > chainTarget.Center.X ? 1 : -1,
                            Knockback = 2f
                        }, false, false);
                    }
                }
            }
            if (CheckHunDunMieShiFormation(cur)) {
                if (Main.rand.NextFloat() < 0.08f) {
                    // 创建爆炸效果（使用爆炸弹幕）
                    Projectile.NewProjectile(Player.GetSource_FromThis(), target.Center, Vector2.Zero, 
                        ProjectileID.Grenade, (int)(damageDone * 0.5f), 0f, Player.whoAmI);
                    // 添加视觉效果
                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                        for (int i = 0; i < 20; i++) {
                            Dust.NewDust(target.position, target.width, target.height, DustID.Torch, Scale: 2f);
                        }
                    }
                }
            }
            
            // 万剑归宗阵：效果只在伤害加成中处理（已移除剑气效果）
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) return;
            
            int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            if (cur == null) return;
            
            // 弑神诛魔阵：对boss伤害额外+15%
            if (CheckShiShenZhuMoFormation(cur)) {
                if (target.boss || NPCID.Sets.ShouldBeCountedAsBoss[target.type]) {
                    modifiers.FinalDamage *= 1.15f;
                }
            }
        }
        
        // ───────── 防御类阵法效果：减少所受伤害 ─────────
        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) return;
            
            int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            if (cur == null) return;
            
            // 玄武护体阵：减少8%所受伤害
            if (CheckXuanWuHuTiFormation(cur)) {
                modifiers.FinalDamage *= 0.92f;
            }
            // 不灭金身阵：减少12%所受伤害
            else if (CheckBuMieJinShenFormation(cur)) {
                modifiers.FinalDamage *= 0.88f;
            }
            // 九转玄功阵：减少18%所受伤害
            else if (CheckJiuZhuanXuanGongFormation(cur)) {
                modifiers.FinalDamage *= 0.82f;
            }
            // 先天不败阵：减少25%所受伤害
            else if (CheckXianTianBuBaiFormation(cur)) {
                modifiers.FinalDamage *= 0.75f;
            }
            // 混元无极阵：减少30%所受伤害
            else if (CheckHunYuanWuJiFormation(cur)) {
                modifiers.FinalDamage *= 0.70f;
            }
        }
        
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) return;
            
            int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            if (cur == null) return;
            
            // 玄武护体阵：减少8%所受伤害
            if (CheckXuanWuHuTiFormation(cur)) {
                modifiers.FinalDamage *= 0.92f;
            }
            // 不灭金身阵：减少12%所受伤害
            else if (CheckBuMieJinShenFormation(cur)) {
                modifiers.FinalDamage *= 0.88f;
            }
            // 九转玄功阵：减少18%所受伤害
            else if (CheckJiuZhuanXuanGongFormation(cur)) {
                modifiers.FinalDamage *= 0.82f;
            }
            // 先天不败阵：减少25%所受伤害
            else if (CheckXianTianBuBaiFormation(cur)) {
                modifiers.FinalDamage *= 0.75f;
            }
            // 混元无极阵：减少30%所受伤害
            else if (CheckHunYuanWuJiFormation(cur)) {
                modifiers.FinalDamage *= 0.70f;
            }
        }

        /* 初始化数组 */
        public override void Initialize() {
            for (int i = 0; i < SlotCount; i++) {
                BaGuaItems[i] = new Item();
                BaGuaItems[i].TurnToAir();
                wearCounter[i] = 0;
            }
        }

        public void ResetWear(int idx) => wearCounter[idx] = 0;

        public override void PostUpdate() {
            //只在玩家带着 BaGuaBuff 时消耗材料；去掉这行就永久计时
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                GanLuTimer = 0;
                SuoDiChengCunDashCD = 0;
                SuoDiChengCunDashActive = false;
                SuoDiChengCunDashDuration = 0;
                TengYunJiaWuHoverTimer = 0;
                ChiZhiTianYaDashCD = 0;
                ChiZhiTianYaDashActive = false;
                ChiZhiTianYaDashDuration = 0;
                WanShouChaoZongTimer = 0;
                HunDunWanLingTimer = 0;
                return;
            }

            // 安全检查：确保Player有效
            if (Player == null || !Player.active || Player.dead)
                return;

            // 安全检查：确保BaGuaItems有效
            if (BaGuaItems == null)
                return;
            
            // 获取当前阵法物品
            int[] cur = BaGuaItems.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
            
            // 甘露阵效果更新：每15秒回复最大生命值的1%
            if (cur != null && cur.Length == 1 && cur[0] == ItemID.LifeFruit) {
                try {
                    GanLuTimer++;
                    if (GanLuTimer >= GanLuCD) {
                        GanLuTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.01f)); // 回复最大生命值的1%，至少1点
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 长生阵效果更新：每12秒回复最大生命值的2%
            if (cur != null && CheckChangShengFormation(cur)) {
                try {
                    ChangShengTimer++;
                    if (ChangShengTimer >= ChangShengCD) {
                        ChangShengTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.02f)); // 回复最大生命值的2%，至少1点
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 太乙还魂阵效果更新：每8秒回复最大生命值的5%
            if (cur != null && CheckTaiYiHuanHunFormation(cur)) {
                try {
                    TaiYiHuanHunTimer++;
                    if (TaiYiHuanHunTimer >= TaiYiHuanHunCD) {
                        TaiYiHuanHunTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.05f)); // 回复最大生命值的5%，至少1点
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            // 九转玄功阵效果更新：免疫大部分debuff
            if (cur != null && CheckJiuZhuanXuanGongFormation(cur)) {
                try {
                    if (Player != null && Player.active && !Player.dead) {
                        // 移除大部分debuff（类似十字章护盾的效果）
                        if (Player.HasBuff(BuffID.Poisoned)) Player.DelBuff(Player.FindBuffIndex(BuffID.Poisoned));
                        if (Player.HasBuff(BuffID.Bleeding)) Player.DelBuff(Player.FindBuffIndex(BuffID.Bleeding));
                        if (Player.HasBuff(BuffID.OnFire)) Player.DelBuff(Player.FindBuffIndex(BuffID.OnFire));
                        if (Player.HasBuff(BuffID.Cursed)) Player.DelBuff(Player.FindBuffIndex(BuffID.Cursed));
                        if (Player.HasBuff(BuffID.Darkness)) Player.DelBuff(Player.FindBuffIndex(BuffID.Darkness));
                        if (Player.HasBuff(BuffID.Slow)) Player.DelBuff(Player.FindBuffIndex(BuffID.Slow));
                        if (Player.HasBuff(BuffID.Weak)) Player.DelBuff(Player.FindBuffIndex(BuffID.Weak));
                        if (Player.HasBuff(BuffID.Silenced)) Player.DelBuff(Player.FindBuffIndex(BuffID.Silenced));
                        if (Player.HasBuff(BuffID.BrokenArmor)) Player.DelBuff(Player.FindBuffIndex(BuffID.BrokenArmor));
                        if (Player.HasBuff(BuffID.Confused)) Player.DelBuff(Player.FindBuffIndex(BuffID.Confused));
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 神农护体阵效果更新：每10秒回复最大生命值的4%，并免疫中毒、流血、着火状态
            if (cur != null && CheckShenNongHuTiFormation(cur)) {
                try {
                    // 免疫debuff：中毒、流血、着火
                    if (Player != null && Player.active) {
                        Player.buffImmune[BuffID.Poisoned] = true;
                        Player.buffImmune[BuffID.Bleeding] = true;
                        Player.buffImmune[BuffID.OnFire] = true;
                        
                        // 如果已经中了这些debuff，立即移除
                        if (Player.HasBuff(BuffID.Poisoned)) {
                            Player.DelBuff(Player.FindBuffIndex(BuffID.Poisoned));
                        }
                        if (Player.HasBuff(BuffID.Bleeding)) {
                            Player.DelBuff(Player.FindBuffIndex(BuffID.Bleeding));
                        }
                        if (Player.HasBuff(BuffID.OnFire)) {
                            Player.DelBuff(Player.FindBuffIndex(BuffID.OnFire));
                        }
                    }
                    
                    // 回血逻辑
                    ShenNongHuTiTimer++;
                    if (ShenNongHuTiTimer >= ShenNongHuTiCD) {
                        ShenNongHuTiTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.04f)); // 回复最大生命值的4%，至少1点
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 万象回春阵效果更新：每10秒回复最大生命值的2%，生命值低于50%时提升至每6秒回复5%
            if (cur != null && CheckWanXiangHuiChunFormation(cur)) {
                try {
                    bool isLowHealth = Player != null && Player.statLifeMax2 > 0 && 
                                      (float)Player.statLife / Player.statLifeMax2 < 0.5f; // 生命值低于50%
                    
                    int currentCD = isLowHealth ? WanXiangHuiChunCDLow : WanXiangHuiChunCDNormal;
                    int healPercent = isLowHealth ? 5 : 2; // 低血量时5%，正常时2%
                    
                    WanXiangHuiChunTimer++;
                    if (WanXiangHuiChunTimer >= currentCD) {
                        WanXiangHuiChunTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * (healPercent / 100f))); // 回复最大生命值的百分比
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 聚灵阵效果更新：每15秒回复最大魔力值的2%
            if (cur != null && CheckJuLingFormation(cur)) {
                try {
                    JuLingTimer++;
                    if (JuLingTimer >= JuLingCD) {
                        JuLingTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statManaMax2 > 0 && Player.statManaMax2 >= Player.statMana) {
                            int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.02f)); // 回复最大魔力值的2%，至少1点
                            if (manaAmount > 0 && Player.statMana < Player.statManaMax2) {
                                int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                if (newMana > Player.statMana) {
                                    int actualMana = newMana - Player.statMana;
                                    Player.statMana = newMana;
                                    // 显示魔力回复效果（客户端）
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 灵泉阵效果更新：每12秒回复最大魔力值的4%
            if (cur != null && CheckLingQuanFormation(cur)) {
                try {
                    LingQuanTimer++;
                    if (LingQuanTimer >= LingQuanCD) {
                        LingQuanTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statManaMax2 > 0 && Player.statManaMax2 >= Player.statMana) {
                            int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.04f)); // 回复最大魔力值的4%，至少1点
                            if (manaAmount > 0 && Player.statMana < Player.statManaMax2) {
                                int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                if (newMana > Player.statMana) {
                                    int actualMana = newMana - Player.statMana;
                                    Player.statMana = newMana;
                                    // 显示魔力回复效果（客户端）
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 星辰聚灵阵效果更新：每10秒回复最大魔力值的6%
            if (cur != null && CheckXingChenJuLingFormation(cur)) {
                try {
                    XingChenJuLingTimer++;
                    if (XingChenJuLingTimer >= XingChenJuLingCD) {
                        XingChenJuLingTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statManaMax2 > 0 && Player.statManaMax2 >= Player.statMana) {
                            int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.06f)); // 回复最大魔力值的6%，至少1点
                            if (manaAmount > 0 && Player.statMana < Player.statManaMax2) {
                                int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                if (newMana > Player.statMana) {
                                    int actualMana = newMana - Player.statMana;
                                    Player.statMana = newMana;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 太乙聚灵阵效果更新：每8秒回复最大魔力值的8%
            if (cur != null && CheckTaiYiJuLingFormation(cur)) {
                try {
                    TaiYiJuLingTimer++;
                    if (TaiYiJuLingTimer >= TaiYiJuLingCD) {
                        TaiYiJuLingTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statManaMax2 > 0 && Player.statManaMax2 >= Player.statMana) {
                            int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.08f)); // 回复最大魔力值的8%，至少1点
                            if (manaAmount > 0 && Player.statMana < Player.statManaMax2) {
                                int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                if (newMana > Player.statMana) {
                                    int actualMana = newMana - Player.statMana;
                                    Player.statMana = newMana;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 法力潮汐阵效果更新：每10秒回复最大魔力值的4%，使用魔法武器时额外回复每15秒1%
            if (cur != null && CheckFaLiChaoXiFormation(cur)) {
                try {
                    // 基础回复：每10秒回复4%
                    FaLiChaoXiTimer++;
                    if (FaLiChaoXiTimer >= FaLiChaoXiCD) {
                        FaLiChaoXiTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statManaMax2 > 0 && Player.statManaMax2 >= Player.statMana) {
                            int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.04f)); // 回复最大魔力值的4%
                            if (manaAmount > 0 && Player.statMana < Player.statManaMax2) {
                                int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                if (newMana > Player.statMana) {
                                    int actualMana = newMana - Player.statMana;
                                    Player.statMana = newMana;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                    }
                                }
                            }
                        }
                    }
                    
                    // 额外回复：使用魔法武器时每15秒回复1%
                    if (Player != null && Player.active && Player.HeldItem != null && !Player.HeldItem.IsAir && Player.HeldItem.DamageType == DamageClass.Magic) {
                        FaLiChaoXiExtraTimer++;
                        if (FaLiChaoXiExtraTimer >= FaLiChaoXiExtraCD) {
                            FaLiChaoXiExtraTimer = 0;
                            if (Player.statManaMax2 > 0 && Player.statManaMax2 >= Player.statMana) {
                                int extraManaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.01f)); // 额外回复最大魔力值的1%
                                if (extraManaAmount > 0 && Player.statMana < Player.statManaMax2) {
                                    int newMana = Math.Min(Player.statMana + extraManaAmount, Player.statManaMax2);
                                    if (newMana > Player.statMana) {
                                        int actualMana = newMana - Player.statMana;
                                        Player.statMana = newMana;
                                        if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                            CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else {
                        // 不使用魔法武器时重置额外计时器
                        FaLiChaoXiExtraTimer = 0;
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 鸿蒙灵源阵效果更新：每6秒回复最大魔力值的10%
            if (cur != null && CheckHongMengLingYuanFormation(cur)) {
                try {
                    HongMengLingYuanTimer++;
                    if (HongMengLingYuanTimer >= HongMengLingYuanCD) {
                        HongMengLingYuanTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statManaMax2 > 0 && Player.statManaMax2 >= Player.statMana) {
                            int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.10f)); // 回复最大魔力值的10%，至少1点
                            if (manaAmount > 0 && Player.statMana < Player.statManaMax2) {
                                int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                if (newMana > Player.statMana) {
                                    int actualMana = newMana - Player.statMana;
                                    Player.statMana = newMana;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 阴阳调和阵效果更新：每6秒回复最大生命值的8%
            if (cur != null && CheckYinYangTiaoHeFormation(cur)) {
                try {
                    YinYangTiaoHeTimer++;
                    if (YinYangTiaoHeTimer >= YinYangTiaoHeCD) {
                        YinYangTiaoHeTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.08f)); // 回复最大生命值的8%，至少1点
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 九转金丹阵效果更新：每8秒回复最大生命值的6%
            if (cur != null && CheckJiuZhuanJinDanFormation(cur)) {
                try {
                    JiuZhuanJinDanTimer++;
                    if (JiuZhuanJinDanTimer >= JiuZhuanJinDanCD) {
                        JiuZhuanJinDanTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.06f)); // 回复最大生命值的6%，至少1点
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 生生不息阵效果更新：每10秒回复最大生命值的1% + 固定回复2点
            if (cur != null && CheckShengShengBuXiFormation(cur)) {
                try {
                    ShengShengBuXiTimer++;
                    if (ShengShengBuXiTimer >= ShengShengBuXiCD) {
                        ShengShengBuXiTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int percentHeal = Math.Max(1, (int)(Player.statLifeMax2 * 0.01f)); // 回复最大生命值的1%，至少1点
                            int fixedHeal = 2; // 固定回复2点
                            int totalHeal = percentHeal + fixedHeal; // 总回复量
                            
                            if (totalHeal > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + totalHeal, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // 回春阵效果更新：每10秒回复最大生命值的3%
            if (cur != null && CheckHuiChunFormation(cur)) {
                try {
                    HuiChunTimer++;
                    if (HuiChunTimer >= HuiChunCD) {
                        HuiChunTimer = 0;
                        if (Player != null && Player.active && !Player.dead && 
                            Player.statLifeMax2 > 0 && Player.statLifeMax2 >= Player.statLife) {
                            int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.03f)); // 回复最大生命值的3%，至少1点
                            if (healAmount > 0 && Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                if (newLife > Player.statLife) {
                                    int actualHeal = newLife - Player.statLife;
                                    Player.statLife = newLife;
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }

            for (int i = 0; i < SlotCount; i++) {
                if (BaGuaItems[i].IsAir) { wearCounter[i] = 0; continue; }

                if (++wearCounter[i] >= WearInterval) {
                    wearCounter[i] = 0;

                    if (BaGuaItems[i].stack > 1)
                        BaGuaItems[i].stack--;       //掉 1 个
                    else
                        BaGuaItems[i].TurnToAir();   //没了就清空

                    //多人联机同步
                    if (Main.netMode == NetmodeID.Server) {
                        ModPacket p = Mod.GetPacket();
                        p.Write((byte)MessageType.SyncBaGuaSlot);
                        p.Write((byte)Player.whoAmI);
                        p.Write((byte)i);
                        ItemIO.Send(BaGuaItems[i], p, true);
                        p.Send();
                    }
                }
            }
        }

        /* ----------------- 配方判定 ----------------- */
        private static readonly Formation[] Formations = {
            new Formation {
                RequiredTypes = [ItemID.LifeFruit],
                Name          = "甘露阵",
                Desc          = "每15秒回复最大生命值的1%",
                ApplyEffect = p => p.DoGanLu()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.LifeFruit, ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.HallowedBar],
                Name          = "长生阵",
                Desc          = "每12秒回复最大生命值的2%",
                ApplyEffect = p => p.DoChangSheng()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.LifeFruit, ItemID.LifeFruit, ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight],
                Name          = "回春阵",
                Desc          = "每10秒回复最大生命值的3%",
                ApplyEffect = p => p.DoHuiChun()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.LifeFruit, ItemID.LifeFruit, ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar],
                Name          = "太乙还魂阵",
                Desc          = "每8秒回复最大生命值的5%",
                ApplyEffect = p => p.DoTaiYiHuanHun()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofNight],
                Name          = "生生不息阵",
                Desc          = "每10秒回复最大生命值的1% + 固定回复2点",
                ApplyEffect = p => p.DoShengShengBuXi()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.LifeFruit, ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.LunarBar, ItemID.LunarBar],
                Name          = "九转金丹阵",
                Desc          = "每8秒回复最大生命值的6%",
                ApplyEffect = p => p.DoJiuZhuanJinDan()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.LifeFruit, ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentNebula, ItemID.FragmentNebula],
                Name          = "阴阳调和阵",
                Desc          = "每6秒回复最大生命值的8%",
                ApplyEffect = p => p.DoYinYangTiaoHe()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.LifeFruit, ItemID.AnkhShield, ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight],
                Name          = "万象回春阵",
                Desc          = "每10秒回复最大生命值的2%，生命值低于50%时提升至每6秒回复5%",
                ApplyEffect = p => p.DoWanXiangHuiChun()
            },
            new Formation {
                RequiredTypes = [ItemID.LifeFruit, ItemID.LifeFruit, ItemID.AnkhShield, ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.SoulofLight],
                Name          = "神农护体阵",
                Desc          = "每10秒回复最大生命值的4%，并免疫中毒、流血、着火状态",
                ApplyEffect = p => p.DoShenNongHuTi()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteOre],
                Name          = "聚灵阵",
                Desc          = "每15秒回复最大魔力值的2%",
                ApplyEffect = p => p.DoJuLing()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.SoulofNight, ItemID.SoulofNight],
                Name          = "灵泉阵",
                Desc          = "每12秒回复最大魔力值的4%",
                ApplyEffect = p => p.DoLingQuan()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.SoulofNight, ItemID.SoulofNight],
                Name          = "星辰聚灵阵",
                Desc          = "每10秒回复最大魔力值的6%",
                ApplyEffect = p => p.DoXingChenJuLing()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.LunarBar, ItemID.LunarBar, ItemID.SoulofLight, ItemID.SoulofNight],
                Name          = "太乙聚灵阵",
                Desc          = "每8秒回复最大魔力值的8%",
                ApplyEffect = p => p.DoTaiYiJuLing()
            },
            new Formation {
                RequiredTypes = [ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.AdamantiteBar, ItemID.AdamantiteBar],
                Name          = "法力潮汐阵",
                Desc          = "每10秒回复最大魔力值的4%，使用魔法武器时额外回复每15秒1%",
                ApplyEffect = p => p.DoFaLiChaoXi()
            },
            new Formation {
                RequiredTypes = [ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.FragmentNebula, ItemID.FragmentVortex],
                Name          = "鸿蒙灵源阵",
                Desc          = "每6秒回复最大魔力值的10%",
                ApplyEffect = p => p.DoHongMengLingYuan()
            },
            new Formation {
                RequiredTypes = [ItemID.TurtleShell, ItemID.TurtleShell, ItemID.TurtleShell, ItemID.Coral, ItemID.Coral, ItemID.Coral, ItemID.Coral, ItemID.WaterBucket ],
                Name          = "镇海阵",
                Desc          = "防御 +15，附带荆棘反伤 100%，但移动速度 -15%",
                ApplyEffect = p => p.DoZhenHai()
            },
            new Formation {
                RequiredTypes = [ ItemID.FireFeather, ItemID.LivingFireBlock, ItemID.LivingFireBlock, ItemID.LivingFireBlock, ItemID.LivingFireBlock, ItemID.LivingFireBlock, ModContent.ItemType<LingShiOre>(), ModContent.ItemType<LingShiOre>() ],
                Name          = "涅槃阵",
                Desc          = "受到致命伤，立即凤凰涅槃",
                ApplyEffect = p => p.DoPhoenix()
            },
            new Formation {
                RequiredTypes = [ ItemID.Flamarang, ItemID.EnchantedBoomerang, ItemID.WoodenBoomerang, ItemID.IceBoomerang, ItemID.Shroomerang, ModContent.ItemType<LingShiOre>()],
                Name          = "回旋镖阵",
                Desc          = "一直飞出回旋镖",
                ApplyEffect = p => p.DoBoomerang()
            },
            new Formation {
                RequiredTypes = [
                    ItemID.Starfury,ItemID.ManaCrystal, ItemID.ManaCrystal, ItemID.ManaCrystal, ItemID.ManaCrystal,ItemID.ManaCrystal, ModContent.ItemType<LingShiOre>(), ModContent.ItemType<LingShiOre>()],
                Name = "落星阵",
                Desc = "召唤流星自动砸向敌人",
                ApplyEffect = p => p.DoFallingStar()
            },
            // ───────── 攻击类阵法 ─────────
            new Formation {
                RequiredTypes = [ItemID.AdamantiteOre],
                Name          = "锋芒阵",
                Desc          = "近战伤害+3%",
                ApplyEffect = p => p.DoFengMang()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.SoulofNight, ItemID.SoulofNight],
                Name          = "破军阵",
                Desc          = "近战伤害+8%，近战速度+5%",
                ApplyEffect = p => p.DoPoJun()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.SoulofNight, ItemID.SoulofNight],
                Name          = "诛邪阵",
                Desc          = "所有伤害+8%，暴击率+3%",
                ApplyEffect = p => p.DoZhuXie()
            },
            // 注意：万剑归宗阵需要特殊检查（任意两把高级剑），所以不在Formations数组中，而是在ResetEffects中特殊处理
            new Formation {
                RequiredTypes = [ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.LunarBar, ItemID.LunarBar, ItemID.SoulofLight, ItemID.SoulofNight],
                Name          = "天罡破阵",
                Desc          = "所有伤害+12%，暴击率+5%，攻击速度+8%",
                ApplyEffect = p => p.DoTianGangPo()
            },
            new Formation {
                RequiredTypes = [ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.SoulofLight, ItemID.SoulofNight],
                Name          = "混沌灭世阵",
                Desc          = "所有伤害+18%，暴击率+8%，攻击有8%概率触发范围爆炸",
                ApplyEffect = p => p.DoHunDunMieShi()
            },
            new Formation {
                RequiredTypes = [ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.FragmentNebula, ItemID.FragmentNebula, ItemID.FragmentVortex],
                Name          = "盘古开天阵",
                Desc          = "所有伤害+22%，暴击率+12%",
                ApplyEffect = p => p.DoPanGuKaiTian()
            },
            new Formation {
                RequiredTypes = [ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.FragmentNebula, ItemID.FragmentNebula, ItemID.FragmentVortex, ItemID.FragmentVortex],
                Name          = "弑神诛魔阵",
                Desc          = "所有伤害+30%，暴击率+15%，对boss伤害额外+15%",
                ApplyEffect = p => p.DoShiShenZhuMo()
            },
            // ───────── 防御类阵法 ─────────
            // 注意：防御类阵法需要特殊检查（支持任意精金/钛金盔甲部件），所以都在ResetEffects中特殊处理，Formations数组仅作为占位符
            new Formation {
                RequiredTypes = [ItemID.AdamantiteHeadgear, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.SoulofNight, ItemID.SoulofNight, ItemID.LifeFruit], // 精金/钛金盔甲部件 × 1，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2，生命果 × 1
                Name          = "玄武护体阵",
                Desc          = "防御+8，减少8%所受伤害",
                ApplyEffect = p => p.DoXuanWuHuTi()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteHeadgear, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.LunarBar, ItemID.LunarBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.LifeFruit], // 精金/钛金盔甲部件 × 1，神圣锭 × 2，夜明锭 × 2，光明之魂 × 2，生命果 × 1
                Name          = "不灭金身阵",
                Desc          = "防御+15，减少12%所受伤害，免疫击退",
                ApplyEffect = p => p.DoBuMieJinShen()
            },
            new Formation {
                RequiredTypes = [ItemID.AnkhShield, ItemID.AdamantiteHeadgear, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.LunarBar, ItemID.LunarBar, ItemID.LifeFruit, ItemID.LifeFruit], // 十字章护盾 × 1，精金/钛金盔甲部件 × 1，神圣锭 × 2，夜明锭 × 2，生命果 × 2
                Name          = "九转玄功阵",
                Desc          = "防御+22，减少18%所受伤害，免疫大部分debuff",
                ApplyEffect = p => p.DoJiuZhuanXuanGong()
            },
            new Formation {
                RequiredTypes = [ItemID.AnkhShield, ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.LifeFruit], // 十字章护盾 × 1，夜明锭 × 2，日耀碎片 × 2，星尘碎片 × 2，生命果 × 1
                Name          = "先天不败阵",
                Desc          = "防御+30，减少25%所受伤害，受到致命伤害时保留1点生命（冷却10分钟）",
                ApplyEffect = p => p.DoXianTianBuBai()
            },
            new Formation {
                RequiredTypes = [ItemID.AnkhShield, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.FragmentNebula, ItemID.FragmentNebula, ItemID.FragmentVortex], // 十字章护盾 × 1，日耀碎片 × 2，星尘碎片 × 2，星云碎片 × 2，星旋碎片 × 1
                Name          = "混元无极阵",
                Desc          = "防御+35，减少30%所受伤害，每10秒回复3点生命值",
                ApplyEffect = p => p.DoHunYuanWuJi()
            },
            // ───────── 移动速度类阵法 ─────────
            // 注意：移动速度类阵法需要特殊检查（支持任意忍者装备和翅膀），所以都在ResetEffects中特殊处理，Formations数组仅作为占位符
            new Formation {
                RequiredTypes = [ItemID.NinjaHood], // 任意忍者大师装备
                Name          = "神行阵",
                Desc          = "移动速度+12%，奔跑速度+8%",
                ApplyEffect = p => p.DoShenXing()
            },
            new Formation {
                // 注意：此阵法在ResetEffects中特殊处理，RequiredTypes仅作占位符
                // 实际检测使用IsWings方法，支持任意翅膀
                RequiredTypes = [ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.AdamantiteOre], // 占位符：翅膀（任意）× 1，精金/钛金矿 × 3，神圣锭 × 3，光明之魂 × 1
                Name          = "御风阵",
                Desc          = "移动速度+20%，飞行时间+30%，跳跃高度+12%",
                ApplyEffect = p => p.DoYuFeng()
            },
            new Formation {
                RequiredTypes = [ItemID.NinjaHood, ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.SoulofNight], // 忍者大师装备 × 1，精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 1
                Name          = "缩地成寸阵",
                Desc          = "移动速度+28%，可在短时间内冲刺（冷却15秒）",
                ApplyEffect = p => p.DoSuoDiChengCun()
            },
            new Formation {
                // 注意：此阵法在ResetEffects中特殊处理，RequiredTypes仅作占位符
                // 实际检测使用IsWings方法，支持任意翅膀
                RequiredTypes = [ItemID.NinjaHood, ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.LunarBar, ItemID.LunarBar, ItemID.AdamantiteBar], // 占位符：翅膀（任意）× 1，忍者大师装备 × 1，精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2
                Name          = "腾云驾雾阵",
                Desc          = "移动速度+35%，飞行时间+60%，可在空中停留2秒",
                ApplyEffect = p => p.DoTengYunJiaWu()
            },
            new Formation {
                // 注意：此阵法在ResetEffects中特殊处理，RequiredTypes仅作占位符
                // 实际检测使用IsAdvancedWings方法，支持任意高级翅膀
                RequiredTypes = [ItemID.FragmentVortex, ItemID.FragmentVortex, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.NinjaHood, ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar], // 占位符：星旋碎片 × 2，日耀碎片 × 2，翅膀（高级）× 1，忍者大师装备 × 1，夜明锭 × 2
                Name          = "咫尺天涯阵",
                Desc          = "移动速度+45%，飞行时间+80%，连续冲刺能力",
                ApplyEffect = p => p.DoChiZhiTianYa()
            },
            // ───────── 召唤类阵法 ─────────
            // 注意：召唤类阵法需要特殊检查（支持精金/钛金），所以都在ResetEffects中特殊处理，Formations数组仅作为占位符
            new Formation {
                RequiredTypes = [ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.AdamantiteOre, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight], // 精金/钛金矿 × 4，神圣锭 × 3，光明之魂 × 1
                Name          = "百兽召唤阵",
                Desc          = "召唤物上限+1，召唤伤害+8%",
                ApplyEffect = p => p.DoBaiShouZhaoHuan()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.SoulofLight, ItemID.SoulofLight, ItemID.SoulofNight, ItemID.SoulofNight], // 精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2
                Name          = "天兵天将阵",
                Desc          = "召唤物上限+2，召唤伤害+15%，召唤物移动速度+10%",
                ApplyEffect = p => p.DoTianBingTianJiang()
            },
            new Formation {
                RequiredTypes = [ItemID.AdamantiteBar, ItemID.AdamantiteBar, ItemID.HallowedBar, ItemID.HallowedBar, ItemID.LunarBar, ItemID.LunarBar, ItemID.SoulofLight, ItemID.SoulofNight], // 精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2，光明之魂 × 1，暗影之魂 × 1
                Name          = "万兽朝宗阵",
                Desc          = "召唤物上限+3，召唤伤害+22%，召唤物持续对周围敌人造成伤害",
                ApplyEffect = p => p.DoWanShouChaoZong()
            },
            new Formation {
                RequiredTypes = [ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.LunarBar, ItemID.LunarBar, ItemID.FragmentSolar, ItemID.FragmentSolar, ItemID.HallowedBar], // 星尘碎片 × 3，夜明锭 × 2，日耀碎片 × 2，神圣锭 × 1
                Name          = "神魔降世阵",
                Desc          = "召唤物上限+4，召唤伤害+35%，召唤物有概率触发特殊攻击",
                ApplyEffect = p => p.DoShenMoJiangShi()
            },
            new Formation {
                RequiredTypes = [ItemID.FragmentStardust, ItemID.FragmentStardust, ItemID.FragmentNebula, ItemID.FragmentNebula, ItemID.FragmentVortex, ItemID.FragmentVortex, ItemID.MoonLordTrophy, ItemID.LunarBar], // 星尘碎片 × 2，星云碎片 × 2，星旋碎片 × 2，月亮领主奖章 × 1，夜明锭 × 1
                Name          = "混沌万灵阵",
                Desc          = "召唤物上限+5，召唤伤害+50%，召唤物自动追踪敌人并造成范围伤害",
                ApplyEffect = p => p.DoHunDunWanLing()
            },
            /* ... 可继续追加 ... */
        };

        public override void ResetEffects() {
            //默认清空显示文本
            CurrentName = "";
            CurrentDesc = "";

            phoenixActive = false;
            if (phoenixCD > 0) phoenixCD--;

            // 重置免疫击退（默认关闭，如果不灭金身阵激活会重新设置）
            Player.noKnockback = false;

            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                // 失去Buff时重置所有阵法计时器
                GanLuTimer = 0;
                ChangShengTimer = 0;
                HuiChunTimer = 0;
                TaiYiHuanHunTimer = 0;
                ShengShengBuXiTimer = 0;
                JiuZhuanJinDanTimer = 0;
                YinYangTiaoHeTimer = 0;
                WanXiangHuiChunTimer = 0;
                ShenNongHuTiTimer = 0;
                JuLingTimer = 0;
                LingQuanTimer = 0;
                XingChenJuLingTimer = 0;
                TaiYiJuLingTimer = 0;
                FaLiChaoXiTimer = 0;
                FaLiChaoXiExtraTimer = 0;
                HongMengLingYuanTimer = 0;
                HunYuanWuJiTimer = 0;
                XianTianBuBaiCD = 0;
                XianTianBuBaiActive = false;
                SuoDiChengCunDashCD = 0;
                SuoDiChengCunDashActive = false;
                SuoDiChengCunDashDuration = 0;
                TengYunJiaWuHoverTimer = 0;
                ChiZhiTianYaDashCD = 0;
                ChiZhiTianYaDashActive = false;
                ChiZhiTianYaDashDuration = 0;
                WanShouChaoZongTimer = 0;
                HunDunWanLingTimer = 0;
                YinShenDunXingTimer = 0;
                YinShenDunXingCD = 0;
                YinShenDunXingActive = false;
                LieYanFenTianTimer = 0;
                WuXingXiangShengTimer = 0;
                LiuHeGuiYiTimer = 0;
                TaiJiHunYuanTimer = 0;
                return;
            }

            int[] cur = BaGuaItems.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();

            // 特殊处理：先检查需要特殊匹配的阵法（支持精金/钛金或使用天柱碎片）
            // 注意：优先级从高到低，包含天柱碎片和夜明锭的阵法优先
            if (CheckShenNongHuTiFormation(cur)) {
                CurrentName = "神农护体阵";
                CurrentDesc = "每10秒回复最大生命值的4%，并免疫中毒、流血、着火状态";
                DoShenNongHuTi();
            }
            else if (CheckYinYangTiaoHeFormation(cur)) {
                CurrentName = "阴阳调和阵";
                CurrentDesc = "每6秒回复最大生命值的8%";
                DoYinYangTiaoHe();
            }
            else if (CheckWanXiangHuiChunFormation(cur)) {
                CurrentName = "万象回春阵";
                CurrentDesc = "每10秒回复最大生命值的2%，生命值低于50%时提升至每6秒回复5%";
                DoWanXiangHuiChun();
            }
            else if (CheckJiuZhuanJinDanFormation(cur)) {
                CurrentName = "九转金丹阵";
                CurrentDesc = "每8秒回复最大生命值的6%";
                DoJiuZhuanJinDan();
            }
            else if (CheckShengShengBuXiFormation(cur)) {
                CurrentName = "生生不息阵";
                CurrentDesc = "每10秒回复最大生命值的1% + 固定回复2点";
                DoShengShengBuXi();
            }
            else if (CheckTaiYiHuanHunFormation(cur)) {
                CurrentName = "太乙还魂阵";
                CurrentDesc = "每8秒回复最大生命值的5%";
                DoTaiYiHuanHun();
            }
            else if (CheckChangShengFormation(cur)) {
                CurrentName = "长生阵";
                CurrentDesc = "每12秒回复最大生命值的2%";
                DoChangSheng();
            }
            else if (CheckHuiChunFormation(cur)) {
                CurrentName = "回春阵";
                CurrentDesc = "每10秒回复最大生命值的3%";
                DoHuiChun();
            }
            else if (CheckJuLingFormation(cur)) {
                CurrentName = "聚灵阵";
                CurrentDesc = "每15秒回复最大魔力值的2%";
                DoJuLing();
            }
            else if (CheckLingQuanFormation(cur)) {
                CurrentName = "灵泉阵";
                CurrentDesc = "每12秒回复最大魔力值的4%";
                DoLingQuan();
            }
            else if (CheckHongMengLingYuanFormation(cur)) {
                CurrentName = "鸿蒙灵源阵";
                CurrentDesc = "每6秒回复最大魔力值的10%";
                DoHongMengLingYuan();
            }
            else if (CheckFaLiChaoXiFormation(cur)) {
                // 法力潮汐阵检查（使用夜明锭和部分天柱碎片）
                CurrentName = "法力潮汐阵";
                CurrentDesc = "每10秒回复最大魔力值的4%，使用魔法武器时额外回复每15秒1%";
                DoFaLiChaoXi();
            }
            else if (CheckXingChenJuLingFormation(cur)) {
                CurrentName = "星辰聚灵阵";
                CurrentDesc = "每10秒回复最大魔力值的6%";
                DoXingChenJuLing();
            }
            else if (CheckTaiYiJuLingFormation(cur)) {
                CurrentName = "太乙聚灵阵";
                CurrentDesc = "每8秒回复最大魔力值的8%";
                DoTaiYiJuLing();
            }
            // ───────── 攻击类阵法检查（按优先级，复杂阵法优先） ─────────
            else if (CheckShiShenZhuMoFormation(cur)) {
                CurrentName = "弑神诛魔阵";
                CurrentDesc = "所有伤害+30%，暴击率+15%，对boss伤害额外+15%";
                DoShiShenZhuMo();
            }
            else if (CheckPanGuKaiTianFormation(cur)) {
                CurrentName = "盘古开天阵";
                CurrentDesc = "所有伤害+22%，暴击率+12%";
                DoPanGuKaiTian();
            }
            else if (CheckHunDunMieShiFormation(cur)) {
                CurrentName = "混沌灭世阵";
                CurrentDesc = "所有伤害+18%，暴击率+8%，攻击有8%概率触发范围爆炸";
                DoHunDunMieShi();
            }
            else if (CheckTianGangPoFormation(cur)) {
                CurrentName = "天罡破阵";
                CurrentDesc = "所有伤害+12%，暴击率+5%";
                DoTianGangPo();
            }
            else if (CheckWanJianGuiZongFormation(cur)) {
                CurrentName = "万剑归宗阵";
                CurrentDesc = "近战伤害+15%";
                DoWanJianGuiZong();
            }
            else if (CheckZhuXieFormation(cur)) {
                CurrentName = "诛邪阵";
                CurrentDesc = "所有伤害+8%，暴击率+3%";
                DoZhuXie();
            }
            else if (CheckPoJunFormation(cur)) {
                CurrentName = "破军阵";
                CurrentDesc = "近战伤害+8%";
                DoPoJun();
            }
            else if (CheckFengMangFormation(cur)) {
                CurrentName = "锋芒阵";
                CurrentDesc = "近战伤害+3%";
                DoFengMang();
            }
            // ───────── 防御类阵法检查（按优先级，复杂阵法优先） ─────────
            else if (CheckHunYuanWuJiFormation(cur)) {
                CurrentName = "混元无极阵";
                CurrentDesc = "防御+35，减少30%所受伤害，每10秒回复3点生命值";
                DoHunYuanWuJi();
            }
            else if (CheckXianTianBuBaiFormation(cur)) {
                CurrentName = "先天不败阵";
                CurrentDesc = "防御+30，减少25%所受伤害，受到致命伤害时保留1点生命（冷却10分钟）";
                DoXianTianBuBai();
            }
            else if (CheckJiuZhuanXuanGongFormation(cur)) {
                CurrentName = "九转玄功阵";
                CurrentDesc = "防御+22，减少18%所受伤害，免疫大部分debuff";
                DoJiuZhuanXuanGong();
            }
            else if (CheckBuMieJinShenFormation(cur)) {
                CurrentName = "不灭金身阵";
                CurrentDesc = "防御+15，减少12%所受伤害，免疫击退";
                DoBuMieJinShen();
            }
            else if (CheckXuanWuHuTiFormation(cur)) {
                CurrentName = "玄武护体阵";
                CurrentDesc = "防御+8，减少8%所受伤害";
                DoXuanWuHuTi();
            }
            else if (CheckJinGangFormation(cur)) {
                CurrentName = "金刚阵";
                CurrentDesc = "防御+3";
                DoJinGang();
            }
            // ───────── 移动速度类阵法检查（按优先级，复杂阵法优先） ─────────
            else if (CheckChiZhiTianYaFormation(cur)) {
                CurrentName = "咫尺天涯阵";
                CurrentDesc = "移动速度+45%，飞行时间+80%，连续冲刺能力";
                DoChiZhiTianYa();
            }
            else if (CheckTengYunJiaWuFormation(cur)) {
                CurrentName = "腾云驾雾阵";
                CurrentDesc = "移动速度+35%，飞行时间+60%，可在空中停留2秒";
                DoTengYunJiaWu();
            }
            else if (CheckSuoDiChengCunFormation(cur)) {
                CurrentName = "缩地成寸阵";
                CurrentDesc = "移动速度+28%，可在短时间内冲刺（冷却15秒）";
                DoSuoDiChengCun();
            }
            else if (CheckYuFengFormation(cur)) {
                CurrentName = "御风阵";
                CurrentDesc = "移动速度+20%，飞行时间+30%，跳跃高度+12%";
                DoYuFeng();
            }
            else if (CheckShenXingFormation(cur)) {
                CurrentName = "神行阵";
                CurrentDesc = "移动速度+12%，奔跑速度+8%";
                DoShenXing();
            }
            // ───────── 召唤类阵法检查（按优先级，复杂阵法优先） ─────────
            else if (CheckHunDunWanLingFormation(cur)) {
                CurrentName = "混沌万灵阵";
                CurrentDesc = "召唤物上限+5，召唤伤害+50%，召唤物自动追踪敌人并造成范围伤害";
                DoHunDunWanLing();
            }
            else if (CheckShenMoJiangShiFormation(cur)) {
                CurrentName = "神魔降世阵";
                CurrentDesc = "召唤物上限+4，召唤伤害+35%，召唤物有概率触发特殊攻击";
                DoShenMoJiangShi();
            }
            else if (CheckWanShouChaoZongFormation(cur)) {
                CurrentName = "万兽朝宗阵";
                CurrentDesc = "召唤物上限+3，召唤伤害+22%，召唤物持续对周围敌人造成伤害";
                DoWanShouChaoZong();
            }
            else if (CheckTianBingTianJiangFormation(cur)) {
                CurrentName = "天兵天将阵";
                CurrentDesc = "召唤物上限+2，召唤伤害+15%，召唤物移动速度+10%";
                DoTianBingTianJiang();
            }
            else if (CheckBaiShouZhaoHuanFormation(cur)) {
                CurrentName = "百兽召唤阵";
                CurrentDesc = "召唤物上限+1，召唤伤害+8%";
                DoBaiShouZhaoHuan();
            }
            // ───────── 特殊效果类阵法检查（按优先级，复杂阵法优先） ─────────
            else if (CheckTaiJiHunYuanFormation(cur)) {
                CurrentName = "太极混元阵";
                CurrentDesc = "所有伤害+20%，防御+18，每10秒回复生命值5%，每10秒回复魔力值6%，移动速度+25%，召唤物上限+1";
                DoTaiJiHunYuan();
            }
            else if (CheckLiuHeGuiYiFormation(cur)) {
                CurrentName = "六合归一阵";
                CurrentDesc = "所有伤害+15%，防御+12，每12秒回复生命值3%，每12秒回复魔力值4%，移动速度+18%";
                DoLiuHeGuiYi();
            }
            else if (CheckWuXingXiangShengFormation(cur)) {
                CurrentName = "五行相生阵";
                CurrentDesc = "所有伤害+12%，防御+10，每15秒回复生命值2%，每15秒回复魔力值3%";
                DoWuXingXiangSheng();
            }
            else if (CheckSanCaiHeYiFormation(cur)) {
                CurrentName = "三才合一阵";
                CurrentDesc = "所有伤害+10%，防御+6，移动速度+12%";
                DoSanCaiHeYi();
            }
            else if (CheckShiKongNiuQuFormation(cur)) {
                CurrentName = "时空扭曲阵";
                CurrentDesc = "使用时间缩短15%，魔力消耗减少10%";
                DoShiKongNiuQu();
            }
            else if (CheckTunShiWanWuFormation(cur)) {
                CurrentName = "吞噬万物阵";
                CurrentDesc = "击败敌人时回复3点生命值和2点魔力值";
                DoTunShiWanWu();
            }
            else if (CheckBaGuaTuiYanFormation(cur)) {
                CurrentName = "八卦推演阵";
                CurrentDesc = "显示地图上的敌人、宝箱、矿石位置";
                DoBaGuaTuiYan();
            }
            else if (CheckLeiTingWanJunFormation(cur)) {
                CurrentName = "雷霆万钧阵";
                CurrentDesc = "攻击有12%概率触发连锁闪电，对多个敌人造成伤害";
                DoLeiTingWanJun();
            }
            else if (CheckHanBingFengTianFormation(cur)) {
                CurrentName = "寒冰封天阵";
                CurrentDesc = "攻击有15%概率冰冻敌人，免疫冰冻debuff";
                DoHanBingFengTian();
            }
            else if (CheckLieYanFenTianFormation(cur)) {
                CurrentName = "烈焰焚天阵";
                CurrentDesc = "对周围敌人持续造成火焰伤害（每秒5点），免疫火焰debuff";
                DoLieYanFenTian();
            }
            else if (CheckYinShenDunXingFormation(cur)) {
                CurrentName = "隐身遁形阵";
                CurrentDesc = "进入隐身状态（移动速度-30%，但完全隐身），持续8秒（冷却45秒）";
                DoYinShenDunXing();
            }
            else {
                foreach (var f in Formations) {
                    if (f.Name == "长生阵" || f.Name == "回春阵" || f.Name == "太乙还魂阵" || f.Name == "生生不息阵" || f.Name == "九转金丹阵" || f.Name == "阴阳调和阵" || f.Name == "万象回春阵" || f.Name == "神农护体阵" || f.Name == "聚灵阵" || f.Name == "灵泉阵" || f.Name == "星辰聚灵阵" || f.Name == "太乙聚灵阵" || f.Name == "法力潮汐阵" || f.Name == "鸿蒙灵源阵" || f.Name == "锋芒阵" || f.Name == "破军阵" || f.Name == "诛邪阵" || f.Name == "万剑归宗阵" || f.Name == "天罡破阵" || f.Name == "混沌灭世阵" || f.Name == "盘古开天阵" || f.Name == "弑神诛魔阵" || f.Name == "金刚阵" || f.Name == "玄武护体阵" || f.Name == "不灭金身阵" || f.Name == "九转玄功阵" || f.Name == "先天不败阵" || f.Name == "混元无极阵" || f.Name == "神行阵" || f.Name == "御风阵" || f.Name == "缩地成寸阵" || f.Name == "腾云驾雾阵" || f.Name == "咫尺天涯阵" || f.Name == "百兽召唤阵" || f.Name == "天兵天将阵" || f.Name == "万兽朝宗阵" || f.Name == "神魔降世阵" || f.Name == "混沌万灵阵" || f.Name == "隐身遁形阵" || f.Name == "烈焰焚天阵" || f.Name == "寒冰封天阵" || f.Name == "雷霆万钧阵" || f.Name == "八卦推演阵" || f.Name == "时空扭曲阵" || f.Name == "吞噬万物阵" || f.Name == "三才合一阵" || f.Name == "五行相生阵" || f.Name == "六合归一阵" || f.Name == "太极混元阵") continue; // 跳过已特殊处理的阵法
                    
                    if (cur != null && cur.Length == f.RequiredTypes.Length &&
                        cur.All(t => f.RequiredTypes.Contains(t))) {
                        CurrentName = f.Name;
                        CurrentDesc = f.Desc;
                        f.ApplyEffect(this);
                        break;          //命中一个即可
                    }
                }
            }
            
            // 如果当前激活的不是回血/回蓝/防御阵法，重置计时器
            if (CurrentName != "甘露阵" && CurrentName != "长生阵" && CurrentName != "回春阵" && CurrentName != "太乙还魂阵" && CurrentName != "生生不息阵" && CurrentName != "九转金丹阵" && CurrentName != "阴阳调和阵" && CurrentName != "万象回春阵" && CurrentName != "神农护体阵" && CurrentName != "聚灵阵" && CurrentName != "灵泉阵" && CurrentName != "星辰聚灵阵" && CurrentName != "太乙聚灵阵" && CurrentName != "法力潮汐阵" && CurrentName != "鸿蒙灵源阵" && CurrentName != "混元无极阵") {
                GanLuTimer = 0;
                ChangShengTimer = 0;
                HuiChunTimer = 0;
                TaiYiHuanHunTimer = 0;
                ShengShengBuXiTimer = 0;
                JiuZhuanJinDanTimer = 0;
                YinYangTiaoHeTimer = 0;
                WanXiangHuiChunTimer = 0;
                ShenNongHuTiTimer = 0;
                JuLingTimer = 0;
                LingQuanTimer = 0;
                XingChenJuLingTimer = 0;
                TaiYiJuLingTimer = 0;
                FaLiChaoXiTimer = 0;
                FaLiChaoXiExtraTimer = 0;
                HongMengLingYuanTimer = 0;
                HunYuanWuJiTimer = 0;
            }
            
            // ───────── 防御类阵法效果：防御加成 ─────────
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                int[] defenseCur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (defenseCur != null) {
                    // 金刚阵：防御+3
                    if (CheckJinGangFormation(defenseCur)) {
                        Player.statDefense += 3;
                    }
                    // 玄武护体阵：防御+8
                    else if (CheckXuanWuHuTiFormation(defenseCur)) {
                        Player.statDefense += 8;
                    }
                    // 不灭金身阵：防御+15，免疫击退
                    else if (CheckBuMieJinShenFormation(defenseCur)) {
                        Player.statDefense += 15;
                        Player.noKnockback = true; // 免疫击退
                    }
                    // 九转玄功阵：防御+22
                    else if (CheckJiuZhuanXuanGongFormation(defenseCur)) {
                        Player.statDefense += 22;
                    }
                    // 先天不败阵：防御+30
                    else if (CheckXianTianBuBaiFormation(defenseCur)) {
                        Player.statDefense += 30;
                        XianTianBuBaiActive = true;
                    }
                    // 混元无极阵：防御+35
                    else if (CheckHunYuanWuJiFormation(defenseCur)) {
                        Player.statDefense += 35;
                    }
                }
            }
            
            // ───────── 移动速度类阵法效果：移动速度和奔跑速度加成 ─────────
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                int[] speedCur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (speedCur != null) {
                    // 神行阵：移动速度+12%，奔跑速度+8%
                    if (CheckShenXingFormation(speedCur)) {
                        Player.moveSpeed += 0.12f;
                        Player.maxRunSpeed *= 1.08f;
                    }
                    // 御风阵：移动速度+20%
                    else if (CheckYuFengFormation(speedCur)) {
                        Player.moveSpeed += 0.20f;
                    }
                    // 缩地成寸阵：移动速度+28%
                    else if (CheckSuoDiChengCunFormation(speedCur)) {
                        Player.moveSpeed += 0.28f;
                    }
                    // 腾云驾雾阵：移动速度+35%
                    else if (CheckTengYunJiaWuFormation(speedCur)) {
                        Player.moveSpeed += 0.35f;
                    }
                    // 咫尺天涯阵：移动速度+45%
                    else if (CheckChiZhiTianYaFormation(speedCur)) {
                        Player.moveSpeed += 0.45f;
                    }
                }
            }
            
            // ───────── 召唤类阵法效果：召唤物上限增加 ─────────
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                int[] summonCur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (summonCur != null) {
                    // 百兽召唤阵：召唤物上限+1
                    if (CheckBaiShouZhaoHuanFormation(summonCur)) {
                        Player.maxMinions += 1;
                    }
                    // 天兵天将阵：召唤物上限+2
                    else if (CheckTianBingTianJiangFormation(summonCur)) {
                        Player.maxMinions += 2;
                    }
                    // 万兽朝宗阵：召唤物上限+3
                    else if (CheckWanShouChaoZongFormation(summonCur)) {
                        Player.maxMinions += 3;
                    }
                    // 神魔降世阵：召唤物上限+4
                    else if (CheckShenMoJiangShiFormation(summonCur)) {
                        Player.maxMinions += 4;
                    }
                    // 混沌万灵阵：召唤物上限+5
                    else if (CheckHunDunWanLingFormation(summonCur)) {
                        Player.maxMinions += 5;
                    }
                    // 太极混元阵：召唤物上限+1
                    else if (CheckTaiJiHunYuanFormation(summonCur)) {
                        Player.maxMinions += 1;
                    }
                }
            }
            
            // ───────── 综合强化类阵法效果：综合属性加成 ─────────
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                int[] comboCur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (comboCur != null) {
                    // 三才合一阵：所有伤害+10%，防御+6，移动速度+12%
                    if (CheckSanCaiHeYiFormation(comboCur)) {
                        Player.moveSpeed += 0.12f;
                    }
                    // 五行相生阵：所有伤害+12%，防御+10（防御在防御类处理中）
                    // 六合归一阵：所有伤害+15%，防御+12，移动速度+18%
                    else if (CheckLiuHeGuiYiFormation(comboCur)) {
                        Player.moveSpeed += 0.18f;
                    }
                    // 太极混元阵：所有伤害+20%，防御+18，移动速度+25%（防御和召唤上限在其他地方处理）
                    else if (CheckTaiJiHunYuanFormation(comboCur)) {
                        Player.moveSpeed += 0.25f;
                    }
                }
            }
            
            // ───────── 特殊效果类阵法效果：隐身、免疫等 ─────────
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                int[] specialCur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (specialCur != null) {
                    // 隐身遁形阵：隐身状态（移动速度-30%）
                    if (CheckYinShenDunXingFormation(specialCur)) {
                        if (YinShenDunXingActive) {
                            Player.invis = true;
                            Player.moveSpeed -= 0.30f; // 移动速度-30%
                        }
                    }
                    // 烈焰焚天阵：免疫火焰debuff
                    else if (CheckLieYanFenTianFormation(specialCur)) {
                        Player.buffImmune[BuffID.OnFire] = true;
                        Player.buffImmune[BuffID.OnFire3] = true;
                    }
                    // 寒冰封天阵：免疫冰冻debuff
                    else if (CheckHanBingFengTianFormation(specialCur)) {
                        Player.buffImmune[BuffID.Frozen] = true;
                        Player.buffImmune[BuffID.Chilled] = true;
                    }
                    // 八卦推演阵：显示地图上的敌人、宝箱、矿石位置
                    else if (CheckBaGuaTuiYanFormation(specialCur)) {
                        // 给予玩家相关信息（通过buff效果实现）
                        // 实际上，这些信息在Terraria中通过配饰提供，我们需要通过其他方式实现
                        // 可以使用Lighting或Minimap相关的功能
                        Player.detectCreature = true; // 显示敌人
                        Player.findTreasure = true; // 显示宝箱
                        // 矿石检测需要额外的处理
                    }
                }
            }
            
            // ───────── 综合强化类阵法效果：防御加成（与防御类合并） ─────────
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                int[] defenseComboCur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (defenseComboCur != null) {
                    // 三才合一阵：防御+6
                    if (CheckSanCaiHeYiFormation(defenseComboCur)) {
                        Player.statDefense += 6;
                    }
                    // 五行相生阵：防御+10
                    else if (CheckWuXingXiangShengFormation(defenseComboCur)) {
                        Player.statDefense += 10;
                    }
                    // 六合归一阵：防御+12
                    else if (CheckLiuHeGuiYiFormation(defenseComboCur)) {
                        Player.statDefense += 12;
                    }
                    // 太极混元阵：防御+18
                    else if (CheckTaiJiHunYuanFormation(defenseComboCur)) {
                        Player.statDefense += 18;
                    }
                }
            }
            
            // 更新先天不败阵冷却
            if (XianTianBuBaiCD > 0) {
                XianTianBuBaiCD--;
            }
            
            // 更新缩地成寸阵冲刺冷却
            if (SuoDiChengCunDashCD > 0) {
                SuoDiChengCunDashCD--;
            }
            
            // 更新咫尺天涯阵冲刺冷却
            if (ChiZhiTianYaDashCD > 0) {
                ChiZhiTianYaDashCD--;
            }
            
            // 混元无极阵效果更新：每10秒回复3点生命值
            if (cur != null && CheckHunYuanWuJiFormation(cur)) {
                try {
                    HunYuanWuJiTimer++;
                    if (HunYuanWuJiTimer >= HunYuanWuJiCD) {
                        HunYuanWuJiTimer = 0;
                        if (Player != null && Player.active && !Player.dead && Player.statLifeMax2 > 0) {
                            int healAmount = 3; // 固定回复3点
                            if (Player.statLife < Player.statLifeMax2) {
                                int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                int actualHeal = newLife - Player.statLife;
                                if (actualHeal > 0) {
                                    Player.statLife += actualHeal;
                                    // 显示回血效果（客户端）
                                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                        Player.HealEffect(actualHeal, true);
                                        CombatText.NewText(Player.Hitbox, Color.Green, $"+{actualHeal}", true);
                                    }
                                }
                            }
                        }
                    }
                }
                catch {
                    // 忽略错误，避免崩溃
                }
            }
            
            // ───────── 移动速度类阵法效果更新 ─────────
            if (cur != null && Player != null && Player.active && !Player.dead) {
                // 御风阵：飞行时间+30%，跳跃高度+12%
                if (CheckYuFengFormation(cur)) {
                    try {
                        if (Player.wingTimeMax > 0) {
                            Player.wingTimeMax = (int)(Player.wingTimeMax * 1.30f);
                        }
                        Player.jumpSpeedBoost += 0.12f;
                    }
                    catch {
                        // 忽略错误
                    }
                }
                // 腾云驾雾阵：飞行时间+60%，可在空中停留2秒
                else if (CheckTengYunJiaWuFormation(cur)) {
                    try {
                        if (Player.wingTimeMax > 0) {
                            Player.wingTimeMax = (int)(Player.wingTimeMax * 1.60f);
                        }
                        // 空中停留：如果玩家在空中且没有按方向键，保持悬浮
                        // 检测玩家是否在地面：通过检查垂直速度和碰撞
                        bool isOnGround = Player.velocity.Y == 0f && 
                                         Collision.SolidCollision(
                                             Player.position + new Microsoft.Xna.Framework.Vector2(0, Player.height),
                                             Player.width, 2);
                        
                        if (!isOnGround && Player.velocity.Y > -0.5f && Player.velocity.Y < 0.5f && 
                            !Player.controlLeft && !Player.controlRight && !Player.controlUp && !Player.controlDown) {
                            if (TengYunJiaWuHoverTimer < TengYunJiaWuHoverMax) {
                                TengYunJiaWuHoverTimer++;
                                Player.velocity.Y = 0f; // 悬浮
                            }
                        } else {
                            TengYunJiaWuHoverTimer = 0; // 重置计时器
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                // 咫尺天涯阵：飞行时间+80%
                else if (CheckChiZhiTianYaFormation(cur)) {
                    try {
                        if (Player.wingTimeMax > 0) {
                            Player.wingTimeMax = (int)(Player.wingTimeMax * 1.80f);
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // 缩地成寸阵：冲刺功能
                if (CheckSuoDiChengCunFormation(cur)) {
                    try {
                        // 检测冲刺输入（双击方向键或按住冲刺键）
                        if (SuoDiChengCunDashCD == 0 && !SuoDiChengCunDashActive) {
                            // 如果玩家按下冲刺键（通常是左Shift或右键），启动冲刺
                            if (Player.controlLeft || Player.controlRight) {
                                // 检查是否连续快速移动（简单的冲刺检测）
                                if (Math.Abs(Player.velocity.X) > 4f && SuoDiChengCunDashCD == 0) {
                                    SuoDiChengCunDashActive = true;
                                    SuoDiChengCunDashDuration = SuoDiChengCunDashDurationMax;
                                }
                            }
                        }
                        
                        // 执行冲刺
                        if (SuoDiChengCunDashActive && SuoDiChengCunDashDuration > 0) {
                            SuoDiChengCunDashDuration--;
                            if (Player.controlLeft || Player.controlRight) {
                                float dashSpeed = 15f;
                                Player.velocity.X = Player.controlLeft ? -dashSpeed : dashSpeed;
                            } else {
                                SuoDiChengCunDashActive = false;
                            }
                            
                            if (SuoDiChengCunDashDuration <= 0) {
                                SuoDiChengCunDashActive = false;
                                SuoDiChengCunDashCD = SuoDiChengCunDashCDMax;
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // 咫尺天涯阵：连续冲刺能力
                if (CheckChiZhiTianYaFormation(cur)) {
                    try {
                        // 连续冲刺：冷却时间更短（2秒）
                        if (ChiZhiTianYaDashCD == 0 && !ChiZhiTianYaDashActive) {
                            if (Player.controlLeft || Player.controlRight) {
                                if (Math.Abs(Player.velocity.X) > 4f) {
                                    ChiZhiTianYaDashActive = true;
                                    ChiZhiTianYaDashDuration = ChiZhiTianYaDashDurationMax;
                                }
                            }
                        }
                        
                        if (ChiZhiTianYaDashActive && ChiZhiTianYaDashDuration > 0) {
                            ChiZhiTianYaDashDuration--;
                            if (Player.controlLeft || Player.controlRight) {
                                float dashSpeed = 18f; // 更快的冲刺速度
                                Player.velocity.X = Player.controlLeft ? -dashSpeed : dashSpeed;
                            } else {
                                ChiZhiTianYaDashActive = false;
                            }
                            
                            if (ChiZhiTianYaDashDuration <= 0) {
                                ChiZhiTianYaDashActive = false;
                                ChiZhiTianYaDashCD = ChiZhiTianYaDashCDMax;
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
            }
            
            // ───────── 召唤类阵法效果更新 ─────────
            if (cur != null && Player != null && Player.active && !Player.dead) {
                // 万兽朝宗阵：召唤物持续对周围敌人造成伤害
                if (CheckWanShouChaoZongFormation(cur)) {
                    try {
                        WanShouChaoZongTimer++;
                        if (WanShouChaoZongTimer >= WanShouChaoZongCD) {
                            WanShouChaoZongTimer = 0;
                            // 对玩家周围150像素范围内的敌人造成伤害
                            float damageRadius = 150f;
                            foreach (NPC npc in Main.npc) {
                                if (npc != null && npc.active && !npc.friendly && !npc.dontTakeDamage && npc.life > 0) {
                                    float distance = (Player.Center - npc.Center).Length();
                                    if (distance <= damageRadius) {
                                        int damage = (int)(Player.GetDamage(DamageClass.Summon).ApplyTo(20)); // 基础伤害20
                                        npc.StrikeNPC(new NPC.HitInfo {
                                            Damage = damage,
                                            Knockback = 0f,
                                            HitDirection = npc.Center.X > Player.Center.X ? 1 : -1
                                        });
                                        // 生成伤害特效
                                        if (Main.netMode != NetmodeID.Server) {
                                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, 0f, 0, default(Color), 1f);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // 混沌万灵阵：召唤物自动追踪敌人并造成范围伤害
                if (CheckHunDunWanLingFormation(cur)) {
                    try {
                        HunDunWanLingTimer++;
                        if (HunDunWanLingTimer >= HunDunWanLingCD) {
                            HunDunWanLingTimer = 0;
                            // 对玩家周围200像素范围内的敌人造成范围伤害
                            float damageRadius = 200f;
                            foreach (NPC npc in Main.npc) {
                                if (npc != null && npc.active && !npc.friendly && !npc.dontTakeDamage && npc.life > 0) {
                                    float distance = (Player.Center - npc.Center).Length();
                                    if (distance <= damageRadius) {
                                        int damage = (int)(Player.GetDamage(DamageClass.Summon).ApplyTo(35)); // 基础伤害35
                                        npc.StrikeNPC(new NPC.HitInfo {
                                            Damage = damage,
                                            Knockback = 2f,
                                            HitDirection = npc.Center.X > Player.Center.X ? 1 : -1
                                        });
                                        // 生成范围伤害特效
                                        if (Main.netMode != NetmodeID.Server) {
                                            for (int i = 0; i < 5; i++) {
                                                Dust.NewDust(npc.position, npc.width, npc.height, DustID.MagicMirror, 0f, 0f, 0, default(Color), 1.5f);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // ───────── 特殊效果类阵法持续效果 ─────────
                // 隐身遁形阵：隐身状态和冷却管理
                if (CheckYinShenDunXingFormation(cur)) {
                    try {
                        // 更新冷却
                        if (YinShenDunXingCD > 0) {
                            YinShenDunXingCD--;
                        }
                        
                        // 如果冷却完毕且未激活，自动激活（每45秒）
                        if (YinShenDunXingCD == 0 && !YinShenDunXingActive) {
                            YinShenDunXingActive = true;
                            YinShenDunXingTimer = 0;
                        }
                        
                        // 如果激活，更新持续时间
                        if (YinShenDunXingActive) {
                            YinShenDunXingTimer++;
                            if (YinShenDunXingTimer >= YinShenDunXingDuration) {
                                YinShenDunXingActive = false;
                                YinShenDunXingCD = YinShenDunXingCDMax;
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // 烈焰焚天阵：对周围敌人持续造成火焰伤害
                if (CheckLieYanFenTianFormation(cur)) {
                    try {
                        LieYanFenTianTimer++;
                        if (LieYanFenTianTimer >= LieYanFenTianCD) {
                            LieYanFenTianTimer = 0;
                            // 对玩家周围100像素范围内的敌人造成5点火焰伤害
                            float damageRadius = 100f;
                            foreach (NPC npc in Main.npc) {
                                if (npc != null && npc.active && !npc.friendly && !npc.dontTakeDamage && npc.life > 0) {
                                    float distance = (Player.Center - npc.Center).Length();
                                    if (distance <= damageRadius) {
                                        npc.StrikeNPC(new NPC.HitInfo {
                                            Damage = 5,
                                            Knockback = 0f,
                                            HitDirection = npc.Center.X > Player.Center.X ? 1 : -1
                                        });
                                        // 添加火焰debuff
                                        npc.AddBuff(BuffID.OnFire, 60 * 2); // 持续2秒
                                        // 生成火焰特效
                                        if (Main.netMode != NetmodeID.Server) {
                                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.Torch, 0f, 0f, 0, default(Color), 1f);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // ───────── 综合强化类阵法持续回复效果 ─────────
                // 五行相生阵：每15秒回复生命值2%，每15秒回复魔力值3%
                if (CheckWuXingXiangShengFormation(cur)) {
                    try {
                        WuXingXiangShengTimer++;
                        if (WuXingXiangShengTimer >= WuXingXiangShengCD) {
                            WuXingXiangShengTimer = 0;
                            // 回复生命值
                            if (Player != null && Player.active && !Player.dead && 
                                Player.statLifeMax2 > 0 && Player.statLife < Player.statLifeMax2) {
                                int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.02f));
                                if (healAmount > 0) {
                                    int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                    if (newLife > Player.statLife) {
                                        int actualHeal = newLife - Player.statLife;
                                        Player.statLife = newLife;
                                        if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                            Player.HealEffect(actualHeal, true);
                                        }
                                    }
                                }
                            }
                            // 回复魔力值
                            if (Player != null && Player.active && !Player.dead && 
                                Player.statManaMax2 > 0 && Player.statMana < Player.statManaMax2) {
                                int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.03f));
                                if (manaAmount > 0) {
                                    int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                    if (newMana > Player.statMana) {
                                        int actualMana = newMana - Player.statMana;
                                        Player.statMana = newMana;
                                        if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                            CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // 六合归一阵：每12秒回复生命值3%，每12秒回复魔力值4%
                if (CheckLiuHeGuiYiFormation(cur)) {
                    try {
                        LiuHeGuiYiTimer++;
                        if (LiuHeGuiYiTimer >= LiuHeGuiYiCD) {
                            LiuHeGuiYiTimer = 0;
                            // 回复生命值
                            if (Player != null && Player.active && !Player.dead && 
                                Player.statLifeMax2 > 0 && Player.statLife < Player.statLifeMax2) {
                                int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.03f));
                                if (healAmount > 0) {
                                    int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                    if (newLife > Player.statLife) {
                                        int actualHeal = newLife - Player.statLife;
                                        Player.statLife = newLife;
                                        if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                            Player.HealEffect(actualHeal, true);
                                        }
                                    }
                                }
                            }
                            // 回复魔力值
                            if (Player != null && Player.active && !Player.dead && 
                                Player.statManaMax2 > 0 && Player.statMana < Player.statManaMax2) {
                                int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.04f));
                                if (manaAmount > 0) {
                                    int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                    if (newMana > Player.statMana) {
                                        int actualMana = newMana - Player.statMana;
                                        Player.statMana = newMana;
                                        if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                            CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
                
                // 太极混元阵：每10秒回复生命值5%，每10秒回复魔力值6%
                if (CheckTaiJiHunYuanFormation(cur)) {
                    try {
                        TaiJiHunYuanTimer++;
                        if (TaiJiHunYuanTimer >= TaiJiHunYuanCD) {
                            TaiJiHunYuanTimer = 0;
                            // 回复生命值
                            if (Player != null && Player.active && !Player.dead && 
                                Player.statLifeMax2 > 0 && Player.statLife < Player.statLifeMax2) {
                                int healAmount = Math.Max(1, (int)(Player.statLifeMax2 * 0.05f));
                                if (healAmount > 0) {
                                    int newLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
                                    if (newLife > Player.statLife) {
                                        int actualHeal = newLife - Player.statLife;
                                        Player.statLife = newLife;
                                        if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                            Player.HealEffect(actualHeal, true);
                                        }
                                    }
                                }
                            }
                            // 回复魔力值
                            if (Player != null && Player.active && !Player.dead && 
                                Player.statManaMax2 > 0 && Player.statMana < Player.statManaMax2) {
                                int manaAmount = Math.Max(1, (int)(Player.statManaMax2 * 0.06f));
                                if (manaAmount > 0) {
                                    int newMana = Math.Min(Player.statMana + manaAmount, Player.statManaMax2);
                                    if (newMana > Player.statMana) {
                                        int actualMana = newMana - Player.statMana;
                                        Player.statMana = newMana;
                                        if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                                            CombatText.NewText(Player.Hitbox, Color.Blue, $"+{actualMana}", true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {
                        // 忽略错误
                    }
                }
            }
        }

        /* ----------------- 内部结构体 ----------------- */
        private struct Formation
        {
            public int[] RequiredTypes;
            public string Name;
            public string Desc;
            public Action<BaGuaPlayer> ApplyEffect;
        }

        /* ---- 保存 / 读取到角色文件 ---- */
        public override void SaveData(TagCompound tag) {
            var list = new List<TagCompound>(SlotCount);
            foreach (Item it in BaGuaItems)
                list.Add(ItemIO.Save(it));
            tag["BaGuaItems"] = list;
            tag["Wear"] = wearCounter;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("BaGuaItems")) {
                var list = tag.GetList<TagCompound>("BaGuaItems");
                for (int i = 0; i < SlotCount && i < list.Count; i++)
                    BaGuaItems[i] = ItemIO.Load(list[i]);
            }
            if (tag.ContainsKey("Wear"))
                wearCounter = tag.GetIntArray("Wear");
        }

        /* ───────── 甘露阵：每15秒回复最大生命值的1% ───────── */
        private void DoGanLu() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            GanLuTimer = 0;
        }

        /* ───────── 长生阵：每12秒回复最大生命值的2% ───────── */
        private void DoChangSheng() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            ChangShengTimer = 0;
        }
        
        /* ───────── 回春阵：每10秒回复最大生命值的3% ───────── */
        private void DoHuiChun() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            HuiChunTimer = 0;
        }
        
        /* ───────── 太乙还魂阵：每8秒回复最大生命值的5% ───────── */
        private void DoTaiYiHuanHun() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            TaiYiHuanHunTimer = 0;
        }
        
        /* ───────── 生生不息阵：每10秒回复最大生命值的1% + 固定回复2点 ───────── */
        private void DoShengShengBuXi() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            ShengShengBuXiTimer = 0;
        }
        
        /* ───────── 九转金丹阵：每8秒回复最大生命值的6% ───────── */
        private void DoJiuZhuanJinDan() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            JiuZhuanJinDanTimer = 0;
        }
        
        /* ───────── 阴阳调和阵：每6秒回复最大生命值的8% ───────── */
        private void DoYinYangTiaoHe() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            YinYangTiaoHeTimer = 0;
        }
        
        /* ───────── 万象回春阵：每10秒回复最大生命值的2%，生命值低于50%时提升至每6秒回复5% ───────── */
        private void DoWanXiangHuiChun() {
            // 回血逻辑在PostUpdate中处理，这里只做初始化
            WanXiangHuiChunTimer = 0;
        }
        
        /* ───────── 神农护体阵：每10秒回复最大生命值的4%，并免疫中毒、流血、着火状态 ───────── */
        private void DoShenNongHuTi() {
            // 回血和免疫逻辑在PostUpdate中处理，这里只做初始化
            ShenNongHuTiTimer = 0;
        }
        
        /* ───────── 聚灵阵：每15秒回复最大魔力值的2% ───────── */
        private void DoJuLing() {
            // 回蓝逻辑在PostUpdate中处理，这里只做初始化
            JuLingTimer = 0;
        }
        
        /* ───────── 检查聚灵阵配方（支持精金/钛金矿） ───────── */
        private bool CheckJuLingFormation(int[] cur) {
            if (cur == null || cur.Length != 1) return false;
            
            try {
                // 需要：精金/钛金矿 × 1
                return cur[0] == ItemID.AdamantiteOre || cur[0] == ItemID.TitaniumOre;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 灵泉阵：每12秒回复最大魔力值的4% ───────── */
        private void DoLingQuan() {
            // 回蓝逻辑在PostUpdate中处理，这里只做初始化
            LingQuanTimer = 0;
        }
        
        /* ───────── 检查灵泉阵配方（支持精金/钛金矿） ───────── */
        private bool CheckLingQuanFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：精金/钛金矿 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2
                int adamantiteOreCount = cur.Count(t => t == ItemID.AdamantiteOre);
                int titaniumOreCount = cur.Count(t => t == ItemID.TitaniumOre);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                // 验证：精金或钛金矿只能有一种，且数量为2
                bool oreValid = (adamantiteOreCount == 2 && titaniumOreCount == 0) || 
                                (adamantiteOreCount == 0 && titaniumOreCount == 2);
                
                return oreValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 星辰聚灵阵：每10秒回复最大魔力值的6% ───────── */
        private void DoXingChenJuLing() {
            // 回蓝逻辑在PostUpdate中处理，这里只做初始化
            XingChenJuLingTimer = 0;
        }
        
        /* ───────── 检查星辰聚灵阵配方（支持精金/钛金锭） ───────── */
        private bool CheckXingChenJuLingFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                // 验证：精金或钛金锭只能有一种，且数量为2
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 太乙聚灵阵：每8秒回复最大魔力值的8% ───────── */
        private void DoTaiYiJuLing() {
            // 回蓝逻辑在PostUpdate中处理，这里只做初始化
            TaiYiJuLingTimer = 0;
        }
        
        /* ───────── 检查太乙聚灵阵配方（支持精金/钛金锭） ───────── */
        private bool CheckTaiYiJuLingFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2，光明之魂 × 1，暗影之魂 × 1
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                // 验证：精金或钛金锭只能有一种，且数量为2
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 法力潮汐阵：每10秒回复最大魔力值的4%，使用魔法武器时额外回复每15秒1% ───────── */
        private void DoFaLiChaoXi() {
            // 回蓝逻辑在PostUpdate中处理，这里只做初始化
            FaLiChaoXiTimer = 0;
            FaLiChaoXiExtraTimer = 0;
        }
        
        /* ───────── 检查法力潮汐阵配方（使用夜明锭和部分天柱碎片，支持精金/钛金锭） ───────── */
        private bool CheckFaLiChaoXiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：夜明锭 × 2，日耀碎片 × 2，星尘碎片 × 2，精金/钛金锭 × 2
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                
                // 验证：精金或钛金锭只能有一种，且数量为2
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return lunarBarCount == 2 &&
                       fragmentSolarCount == 2 &&
                       fragmentStardustCount == 2 &&
                       barValid;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 鸿蒙灵源阵：每6秒回复最大魔力值的10% ───────── */
        private void DoHongMengLingYuan() {
            // 回蓝逻辑在PostUpdate中处理，这里只做初始化
            HongMengLingYuanTimer = 0;
        }
        
        /* ───────── 检查鸿蒙灵源阵配方（使用夜明锭和天柱碎片） ───────── */
        private bool CheckHongMengLingYuanFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：夜明锭 × 2，日耀碎片 × 2，星尘碎片 × 2，星云碎片 × 1，星旋碎片 × 1
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int fragmentNebulaCount = cur.Count(t => t == ItemID.FragmentNebula);
                int fragmentVortexCount = cur.Count(t => t == ItemID.FragmentVortex);
                
                return lunarBarCount == 2 &&
                       fragmentSolarCount == 2 &&
                       fragmentStardustCount == 2 &&
                       fragmentNebulaCount == 1 &&
                       fragmentVortexCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查神农护体阵配方（使用夜明锭和天柱碎片） ───────── */
        private bool CheckShenNongHuTiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 2，十字章护盾 × 1，夜明锭 × 2，日耀碎片 × 1，星尘碎片 × 1，光明之魂 × 1
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int ankhShieldCount = cur.Count(t => t == ItemID.AnkhShield);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                
                return lifeFruitCount == 2 && 
                       ankhShieldCount == 1 &&
                       lunarBarCount == 2 &&
                       fragmentSolarCount == 1 &&
                       fragmentStardustCount == 1 &&
                       soulOfLightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        // ───────── 攻击类阵法检查和初始化方法 ─────────
        
        /* ───────── 锋芒阵：近战伤害+3% ───────── */
        private void DoFengMang() {
            // 效果在ModifyWeaponDamage中处理
        }
        
        private bool CheckFengMangFormation(int[] cur) {
            if (cur == null || cur.Length != 1) return false;
            try {
                return cur[0] == ItemID.AdamantiteOre || cur[0] == ItemID.TitaniumOre;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 破军阵：近战伤害+8%，近战速度+5% ───────── */
        private void DoPoJun() {
            // 效果在ModifyWeaponDamage和GetWeaponAttackSpeed中处理
        }
        
        private bool CheckPoJunFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                int adamantiteOreCount = cur.Count(t => t == ItemID.AdamantiteOre);
                int titaniumOreCount = cur.Count(t => t == ItemID.TitaniumOre);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool oreValid = (adamantiteOreCount == 2 && titaniumOreCount == 0) || 
                                (adamantiteOreCount == 0 && titaniumOreCount == 2);
                
                return oreValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 诛邪阵：所有伤害+8%，暴击率+3% ───────── */
        private void DoZhuXie() {
            // 效果在ModifyWeaponDamage和ModifyWeaponCrit中处理
        }
        
        private bool CheckZhuXieFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 万剑归宗阵：近战伤害+15% ───────── */
        private void DoWanJianGuiZong() {
            // 效果在ModifyWeaponDamage中处理
        }
        
        private bool CheckWanJianGuiZongFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：任意两把高级剑 × 2，神圣锭 × 2，夜明锭 × 2，光明之魂 × 1，暗影之魂 × 1
                int advancedSwordCount = cur.Count(t => AdvancedSwords.Contains(t));
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                return advancedSwordCount == 2 &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 天罡破阵：所有伤害+12%，暴击率+5%，攻击速度+8% ───────── */
        private void DoTianGangPo() {
            // 效果在ModifyWeaponDamage、ModifyWeaponCrit和GetWeaponAttackSpeed中处理
        }
        
        private bool CheckTianGangPoFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 混沌灭世阵：所有伤害+18%，暴击率+8%，攻击有8%概率触发范围爆炸 ───────── */
        private void DoHunDunMieShi() {
            // 效果在ModifyWeaponDamage、ModifyWeaponCrit和ModifyHitNPC中处理
        }
        
        private bool CheckHunDunMieShiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                return lunarBarCount == 2 &&
                       fragmentSolarCount == 2 &&
                       fragmentStardustCount == 2 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 盘古开天阵：所有伤害+22%，暴击率+12% ───────── */
        private void DoPanGuKaiTian() {
            // 效果在ModifyWeaponDamage和ModifyWeaponCrit中处理
        }
        
        private bool CheckPanGuKaiTianFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int fragmentNebulaCount = cur.Count(t => t == ItemID.FragmentNebula);
                int fragmentVortexCount = cur.Count(t => t == ItemID.FragmentVortex);
                
                return fragmentSolarCount == 3 &&
                       fragmentStardustCount == 2 &&
                       fragmentNebulaCount == 2 &&
                       fragmentVortexCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 弑神诛魔阵：所有伤害+30%，暴击率+15%，对boss伤害额外+15% ───────── */
        private void DoShiShenZhuMo() {
            // 效果在ModifyWeaponDamage、ModifyWeaponCrit和ModifyHitNPCWithProj中处理
        }
        
        private bool CheckShiShenZhuMoFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int fragmentNebulaCount = cur.Count(t => t == ItemID.FragmentNebula);
                int fragmentVortexCount = cur.Count(t => t == ItemID.FragmentVortex);
                
                return fragmentSolarCount == 2 &&
                       fragmentStardustCount == 2 &&
                       fragmentNebulaCount == 2 &&
                       fragmentVortexCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查万象回春阵配方（支持精金/钛金锭，需要十字章护盾） ───────── */
        private bool CheckWanXiangHuiChunFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 2，十字章护盾 × 1，精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 1
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int ankhShieldCount = cur.Count(t => t == ItemID.AnkhShield);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                
                // 验证：精金或钛金锭只能有一种，且数量为2
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return lifeFruitCount == 2 && 
                       ankhShieldCount == 1 &&
                       barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查阴阳调和阵配方（使用天柱碎片） ───────── */
        private bool CheckYinYangTiaoHeFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 2，夜明锭 × 2，日耀碎片 × 2，星云碎片 × 2
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentNebulaCount = cur.Count(t => t == ItemID.FragmentNebula);
                
                return lifeFruitCount == 2 && 
                       lunarBarCount == 2 &&
                       fragmentSolarCount == 2 &&
                       fragmentNebulaCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查九转金丹阵配方（支持精金/钛金锭） ───────── */
        private bool CheckJiuZhuanJinDanFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 2，精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                
                // 验证：精金或钛金锭只能有一种，且数量为2
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return lifeFruitCount == 2 && 
                       barValid &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查生生不息阵配方（支持精金/钛金矿） ───────── */
        private bool CheckShengShengBuXiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 1，精金/钛金矿 × 2，神圣锭 × 3，光明之魂 × 1，暗影之魂 × 1
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int adamantiteOreCount = cur.Count(t => t == ItemID.AdamantiteOre);
                int titaniumOreCount = cur.Count(t => t == ItemID.TitaniumOre);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                // 验证：精金或钛金矿只能有一种，且数量为2
                bool oreValid = (adamantiteOreCount == 2 && titaniumOreCount == 0) || 
                                (adamantiteOreCount == 0 && titaniumOreCount == 2);
                
                return lifeFruitCount == 1 && 
                       oreValid &&
                       hallowedBarCount == 3 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查太乙还魂阵配方（支持精金/钛金锭） ───────── */
        private bool CheckTaiYiHuanHunFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 3，精金/钛金锭 × 3，神圣锭 × 2
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                
                return lifeFruitCount == 3 && 
                       (adamantiteBarCount == 3 || titaniumBarCount == 3) &&
                       hallowedBarCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查长生阵配方（支持精金/钛金矿） ───────── */
        private bool CheckChangShengFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 2，精金/钛金矿 × 3，神圣锭 × 3
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int adamantiteCount = cur.Count(t => t == ItemID.AdamantiteOre);
                int titaniumCount = cur.Count(t => t == ItemID.TitaniumOre);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                
                return lifeFruitCount == 2 && 
                       (adamantiteCount == 3 || titaniumCount == 3) &&
                       hallowedBarCount == 3;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 检查回春阵配方（支持精金/钛金锭） ───────── */
        private bool CheckHuiChunFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            
            try {
                // 需要：生命果 × 3，精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 1
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                
                return lifeFruitCount == 3 && 
                       (adamantiteBarCount == 2 || titaniumBarCount == 2) &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 1;
            }
            catch {
                return false;
            }
        }

        /* ───────── 镇海阵：防+15、荆棘、减速 ───────── */
        private void DoZhenHai() {
            //1) 额外防御 +15
            Player.statDefense += 15;

            //2) 100% 荆棘反伤：1f == 100%（与荆棘药剂相同）
            Player.thorns += 1f;

            //3) 移动速度 -15%
            Player.moveSpeed -= 0.4f;          //整体加速系数
            Player.maxRunSpeed *= 0.6f;        //封顶跑速也同步降低，手感更一致
        }

        /* ───────── 朱雀涅槃：一次性复活 ───────── */
        public void DoPhoenix() {
            phoenixActive = true;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp,
                                     ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            // 涅槃阵：凤凰涅槃
            if (phoenixActive && phoenixCD == 0) {
                PhoenixRebirth();
                phoenixCD = PhoenixCDMax;
                return false;   //阻止死亡
            }
            
            // 先天不败阵：受到致命伤害时保留1点生命（冷却10分钟）
            if (XianTianBuBaiActive && XianTianBuBaiCD == 0 && Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                int[] cur = BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (cur != null && CheckXianTianBuBaiFormation(cur)) {
                    Player.statLife = 1; // 保留1点生命
                    XianTianBuBaiCD = XianTianBuBaiCDMax; // 设置冷却
                    Player.immune = true;
                    Player.immuneTime = 60; // 1秒无敌
                    if (Main.netMode != NetmodeID.Server && Main.myPlayer == Player.whoAmI) {
                        Player.HealEffect(1, true);
                    }
                    return false; // 阻止死亡
                }
            }
            
            return true;        //允许死亡
        }

        private void PhoenixRebirth() {
            int heal = Player.statLifeMax2 / 2;
            Player.statLife = heal;
            Player.HealEffect(heal, true);

            Player.immune = true;
            Player.immuneTime = 120;        //2 秒无敌

            //400 点范围火爆
            Projectile.NewProjectile(
                Player.GetSource_FromThis(),
                Player.Center,
                Vector2.Zero,
                ProjectileID.DD2ExplosiveTrapT3Explosion,
                400, 8f, Player.whoAmI);

            SoundEngine.PlaySound(SoundID.Item74, Player.Center);

            for (int i = 0; i < 40; i++)
                Dust.NewDust(Player.position, Player.width, Player.height, DustID.Torch, Scale: 1.8f);
        }

        /* ───────── 回旋镖阵法 ───────── */
        private int BoomerangTimer;
        private const int BoomerangCD = 10;
        public void DoBoomerang() {
            BoomerangTimer++;
            if (BoomerangTimer < BoomerangCD) return;
            BoomerangTimer = 0;

            NPC target = null;
            float dist2 = 600 * 600;
            foreach (NPC npc in Main.npc)
                if (npc.CanBeChasedBy(Player) && !npc.friendly) {
                    float d = Vector2.DistanceSquared(npc.Center, Player.Center);
                    if (d < dist2) { dist2 = d; target = npc; }
                }
            if (target == null) return;

            Vector2 dir = (target.Center - Player.Center).SafeNormalize(Vector2.UnitY) * 14f;

            int projType = BoomerangIDs[Main.rand.Next(BoomerangIDs.Length)];
            Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, dir,
                                      projType, 25, 2f, Player.whoAmI);
        }

        /* ───────── 落星阵法 ───────── */
        public void DoFallingStar() {
            FallingStarTimer++;
            if (FallingStarTimer < FallingStarCD) return;
            FallingStarTimer = 0;

            //寻找最近的非友好 NPC（与回旋镖逻辑保持一致）
            NPC target = null;
            float dist2 = 600 * 600;
            foreach (NPC npc in Main.npc)
                if (npc.CanBeChasedBy(Player) && !npc.friendly) {
                    float d = Vector2.DistanceSquared(npc.Center, Player.Center);
                    if (d < dist2) { dist2 = d; target = npc; }
                }
            if (target == null) return;

            //确定星星出生点：目标上方 600 像素随机 ±80 X 偏移
            Vector2 spawn = new(target.Center.X + Main.rand.Next(-80, 81), target.Center.Y - 600f);
            Vector2 vel = Vector2.UnitY * 16f;           //垂直向下

            int dmg = 80;      //调整为想要的伤害
            float kb = 1.5f;   //击退

            Projectile.NewProjectile(
                Player.GetSource_FromThis(),
                spawn, vel,
                ProjectileID.Starfury, //原版星怒坠星弹道 & 贴图
                dmg, kb, Player.whoAmI);
        }
        
        // ───────── 防御类阵法 Do 和 Check 方法 ─────────
        
        /* ───────── 金刚阵：防御+3 ───────── */
        private void DoJinGang() {
            // 效果在ResetEffects中处理（防御加成）
        }
        
        private bool CheckJinGangFormation(int[] cur) {
            if (cur == null) return false;
            try {
                // 需要：任意精金/钛金盔甲部件 × 1
                return cur.Any(t => AdamantiteTitaniumArmor.Contains(t)) && cur.Length == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 玄武护体阵：防御+8，减少8%所受伤害 ───────── */
        private void DoXuanWuHuTi() {
            // 效果在ResetEffects和ModifyHitByNPC/ModifyHitByProjectile中处理
        }
        
        private bool CheckXuanWuHuTiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金盔甲部件 × 1，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2，生命果 × 1
                bool hasArmor = cur.Any(t => AdamantiteTitaniumArmor.Contains(t));
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                
                return hasArmor &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2 &&
                       lifeFruitCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 不灭金身阵：防御+15，减少12%所受伤害，免疫击退 ───────── */
        private void DoBuMieJinShen() {
            // 效果在ResetEffects和ModifyHitByNPC/ModifyHitByProjectile中处理
        }
        
        private bool CheckBuMieJinShenFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金盔甲部件 × 1，神圣锭 × 2，夜明锭 × 2，光明之魂 × 2，生命果 × 1
                bool hasArmor = cur.Any(t => AdamantiteTitaniumArmor.Contains(t));
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                
                return hasArmor &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       lifeFruitCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 九转玄功阵：防御+22，减少18%所受伤害，免疫大部分debuff ───────── */
        private void DoJiuZhuanXuanGong() {
            // 效果在ResetEffects、ModifyHitByNPC/ModifyHitByProjectile和PostUpdate中处理
        }
        
        private bool CheckJiuZhuanXuanGongFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：十字章护盾 × 1，精金/钛金盔甲部件 × 1，神圣锭 × 2，夜明锭 × 2，生命果 × 2
                bool hasAnkhShield = cur.Any(t => t == ItemID.AnkhShield);
                bool hasArmor = cur.Any(t => AdamantiteTitaniumArmor.Contains(t));
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                
                return hasAnkhShield &&
                       hasArmor &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2 &&
                       lifeFruitCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 先天不败阵：防御+30，减少25%所受伤害，受到致命伤害时保留1点生命（冷却10分钟） ───────── */
        private void DoXianTianBuBai() {
            // 效果在ResetEffects、ModifyHitByNPC/ModifyHitByProjectile和PreKill中处理
        }
        
        private bool CheckXianTianBuBaiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：十字章护盾 × 1，夜明锭 × 2，日耀碎片 × 2，星尘碎片 × 2，生命果 × 1
                bool hasAnkhShield = cur.Any(t => t == ItemID.AnkhShield);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                
                return hasAnkhShield &&
                       lunarBarCount == 2 &&
                       fragmentSolarCount == 2 &&
                       fragmentStardustCount == 2 &&
                       lifeFruitCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 混元无极阵：防御+35，减少30%所受伤害，每10秒回复3点生命值 ───────── */
        private void DoHunYuanWuJi() {
            // 效果在ResetEffects、ModifyHitByNPC/ModifyHitByProjectile和PostUpdate中处理
        }
        
        private bool CheckHunYuanWuJiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：十字章护盾 × 1，日耀碎片 × 2，星尘碎片 × 2，星云碎片 × 2，星旋碎片 × 1
                bool hasAnkhShield = cur.Any(t => t == ItemID.AnkhShield);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int fragmentNebulaCount = cur.Count(t => t == ItemID.FragmentNebula);
                int fragmentVortexCount = cur.Count(t => t == ItemID.FragmentVortex);
                
                return hasAnkhShield &&
                       fragmentSolarCount == 2 &&
                       fragmentStardustCount == 2 &&
                       fragmentNebulaCount == 2 &&
                       fragmentVortexCount == 1;
            }
            catch {
                return false;
            }
        }
        
        // ───────── 移动速度类阵法 Do 和 Check 方法 ─────────
        
        /* ───────── 神行阵：移动速度+12%，奔跑速度+8% ───────── */
        private void DoShenXing() {
            // 效果在ResetEffects中处理（移动速度和奔跑速度加成）
        }
        
        private bool CheckShenXingFormation(int[] cur) {
            if (cur == null) return false;
            try {
                // 需要：任意忍者大师装备 × 1
                return cur.Any(t => NinjaGearItems.Contains(t)) && cur.Length == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 御风阵：移动速度+20%，飞行时间+30%，跳跃高度+12% ───────── */
        private void DoYuFeng() {
            // 效果在ResetEffects和PostUpdate中处理
        }
        
        private bool CheckYuFengFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：翅膀（任意）× 1，精金/钛金矿 × 3，神圣锭 × 3，光明之魂 × 1
                bool hasWings = cur.Any(t => IsWings(t));
                int adamantiteOreCount = cur.Count(t => t == ItemID.AdamantiteOre);
                int titaniumOreCount = cur.Count(t => t == ItemID.TitaniumOre);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                
                bool oreValid = (adamantiteOreCount == 3 && titaniumOreCount == 0) || 
                                (adamantiteOreCount == 0 && titaniumOreCount == 3);
                
                return hasWings &&
                       oreValid &&
                       hallowedBarCount == 3 &&
                       soulOfLightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 缩地成寸阵：移动速度+28%，可在短时间内冲刺（冷却15秒） ───────── */
        private void DoSuoDiChengCun() {
            // 效果在ResetEffects和PostUpdate中处理
        }
        
        private bool CheckSuoDiChengCunFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：忍者大师装备 × 1，精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 1
                bool hasNinjaGear = cur.Any(t => NinjaGearItems.Contains(t));
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return hasNinjaGear &&
                       barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 腾云驾雾阵：移动速度+35%，飞行时间+60%，可在空中停留2秒 ───────── */
        private void DoTengYunJiaWu() {
            // 效果在ResetEffects和PostUpdate中处理
        }
        
        private bool CheckTengYunJiaWuFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：翅膀（任意）× 1，忍者大师装备 × 1，精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2
                bool hasWings = cur.Any(t => IsWings(t));
                bool hasNinjaGear = cur.Any(t => NinjaGearItems.Contains(t));
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return hasWings &&
                       hasNinjaGear &&
                       barValid &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 咫尺天涯阵：移动速度+45%，飞行时间+80%，连续冲刺能力 ───────── */
        private void DoChiZhiTianYa() {
            // 效果在ResetEffects和PostUpdate中处理
        }
        
        private bool CheckChiZhiTianYaFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：星旋碎片 × 2，日耀碎片 × 2，翅膀（高级）× 1，忍者大师装备 × 1，夜明锭 × 2
                int fragmentVortexCount = cur.Count(t => t == ItemID.FragmentVortex);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                bool hasAdvancedWings = cur.Any(t => IsAdvancedWings(t));
                bool hasNinjaGear = cur.Any(t => NinjaGearItems.Contains(t));
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                
                return fragmentVortexCount == 2 &&
                       fragmentSolarCount == 2 &&
                       hasAdvancedWings &&
                       hasNinjaGear &&
                       lunarBarCount == 2;
            }
            catch {
                return false;
            }
        }
        
        // ───────── 召唤类阵法 Do 和 Check 方法 ─────────
        
        /* ───────── 百兽召唤阵：召唤物上限+1，召唤伤害+8% ───────── */
        private void DoBaiShouZhaoHuan() {
            // 效果在ResetEffects和ModifyWeaponDamage中处理
        }
        
        private bool CheckBaiShouZhaoHuanFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金矿 × 4，神圣锭 × 3，光明之魂 × 1
                int adamantiteOreCount = cur.Count(t => t == ItemID.AdamantiteOre);
                int titaniumOreCount = cur.Count(t => t == ItemID.TitaniumOre);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                
                bool oreValid = (adamantiteOreCount == 4 && titaniumOreCount == 0) || 
                                (adamantiteOreCount == 0 && titaniumOreCount == 4);
                
                return oreValid &&
                       hallowedBarCount == 3 &&
                       soulOfLightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 天兵天将阵：召唤物上限+2，召唤伤害+15%，召唤物移动速度+10% ───────── */
        private void DoTianBingTianJiang() {
            // 效果在ResetEffects和ModifyWeaponDamage中处理（移动速度需要在Projectile AI中处理）
        }
        
        private bool CheckTianBingTianJiangFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 万兽朝宗阵：召唤物上限+3，召唤伤害+22%，召唤物持续对周围敌人造成伤害 ───────── */
        private void DoWanShouChaoZong() {
            // 效果在ResetEffects、ModifyWeaponDamage和PostUpdate中处理
            WanShouChaoZongTimer = 0;
        }
        
        private bool CheckWanShouChaoZongFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2，光明之魂 × 1，暗影之魂 × 1
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 神魔降世阵：召唤物上限+4，召唤伤害+35%，召唤物有概率触发特殊攻击 ───────── */
        private void DoShenMoJiangShi() {
            // 效果在ResetEffects和ModifyWeaponDamage中处理（特殊攻击需要在Projectile AI中处理）
        }
        
        private bool CheckShenMoJiangShiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：星尘碎片 × 3，夜明锭 × 2，日耀碎片 × 2，神圣锭 × 1
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                
                return fragmentStardustCount == 3 &&
                       lunarBarCount == 2 &&
                       fragmentSolarCount == 2 &&
                       hallowedBarCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 混沌万灵阵：召唤物上限+5，召唤伤害+50%，召唤物自动追踪敌人并造成范围伤害 ───────── */
        private void DoHunDunWanLing() {
            // 效果在ResetEffects、ModifyWeaponDamage和PostUpdate中处理
            HunDunWanLingTimer = 0;
        }
        
        private bool CheckHunDunWanLingFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：星尘碎片 × 2，星云碎片 × 2，星旋碎片 × 2，月亮领主奖章 × 1，夜明锭 × 1
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int fragmentNebulaCount = cur.Count(t => t == ItemID.FragmentNebula);
                int fragmentVortexCount = cur.Count(t => t == ItemID.FragmentVortex);
                bool hasTrophy = cur.Any(t => t == ItemID.MoonLordTrophy);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                
                return fragmentStardustCount == 2 &&
                       fragmentNebulaCount == 2 &&
                       fragmentVortexCount == 2 &&
                       hasTrophy &&
                       lunarBarCount == 1;
            }
            catch {
                return false;
            }
        }
        
        // ───────── 特殊效果类阵法 Do 和 Check 方法 ─────────
        
        /* ───────── 隐身遁形阵：进入隐身状态（移动速度-30%，但完全隐身），持续8秒（冷却45秒） ───────── */
        private void DoYinShenDunXing() {
            // 效果在ResetEffects和PostUpdate中处理
        }
        
        private bool CheckYinShenDunXingFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 烈焰焚天阵：对周围敌人持续造成火焰伤害（每秒5点），免疫火焰debuff ───────── */
        private void DoLieYanFenTian() {
            // 效果在ResetEffects和PostUpdate中处理
            LieYanFenTianTimer = 0;
        }
        
        private bool CheckLieYanFenTianFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 寒冰封天阵：攻击有15%概率冰冻敌人，免疫冰冻debuff ───────── */
        private void DoHanBingFengTian() {
            // 效果在ResetEffects和OnHitNPCWithItem中处理
        }
        
        private bool CheckHanBingFengTianFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2，光明之魂 × 1，暗影之魂 × 1
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return barValid &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 雷霆万钧阵：攻击有12%概率触发连锁闪电，对多个敌人造成伤害 ───────── */
        private void DoLeiTingWanJun() {
            // 效果在OnHitNPCWithItem中处理
        }
        
        private bool CheckLeiTingWanJunFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：夜明锭 × 2，日耀碎片 × 2，星尘碎片 × 2，神圣锭 × 2
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                
                return lunarBarCount == 2 &&
                       fragmentSolarCount == 2 &&
                       fragmentStardustCount == 2 &&
                       hallowedBarCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 八卦推演阵：显示地图上的敌人、宝箱、矿石位置 ───────── */
        private void DoBaGuaTuiYan() {
            // 效果在ResetEffects中处理（通过Player属性）
        }
        
        private bool CheckBaGuaTuiYanFormation(int[] cur) {
            if (cur == null || cur.Length != 4) return false;
            try {
                // 需要：罗盘 × 1，GPS × 1，生命体分析机 × 1，雷达 × 1
                bool hasCompass = cur.Any(t => t == ItemID.Compass);
                bool hasGPS = cur.Any(t => t == ItemID.GPS);
                bool hasLifeformAnalyzer = cur.Any(t => t == ItemID.LifeformAnalyzer);
                bool hasRadar = cur.Any(t => t == ItemID.Radar);
                
                return hasCompass && hasGPS && hasLifeformAnalyzer && hasRadar;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 时空扭曲阵：使用时间缩短15%，魔力消耗减少10% ───────── */
        private void DoShiKongNiuQu() {
            // 效果在GlobalItem中处理（需要在Global/BaGuaTimeAndManaGlobalItem.cs中实现）
        }
        
        internal bool CheckShiKongNiuQuFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：魔力花 × 1，魔力手环 × 1，天界磁石 × 1，魔法手铐 × 1，夜明锭 × 2，日耀碎片 × 2
                bool hasManaFlower = cur.Any(t => t == ItemID.ManaFlower);
                bool hasMagnetFlower = cur.Any(t => t == ItemID.MagnetFlower);
                bool hasCelestialMagnet = cur.Any(t => t == ItemID.CelestialMagnet);
                bool hasMagicCuffs = cur.Any(t => t == ItemID.MagicCuffs);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                
                return hasManaFlower && hasMagnetFlower && hasCelestialMagnet && hasMagicCuffs &&
                       lunarBarCount == 2 &&
                       fragmentSolarCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 吞噬万物阵：击败敌人时回复3点生命值和2点魔力值 ───────── */
        private void DoTunShiWanWu() {
            // 效果在GlobalNPC中处理（需要在Global/BaGuaKillRewardGlobalNPC.cs中实现）
        }
        
        internal bool CheckTunShiWanWuFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：血肉指虎 × 1，魔法手铐 × 1，星星斗篷 × 1，精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 1
                bool hasFleshKnuckles = cur.Any(t => t == ItemID.FleshKnuckles);
                bool hasMagicCuffs = cur.Any(t => t == ItemID.MagicCuffs);
                bool hasStarVeil = cur.Any(t => t == ItemID.StarVeil);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return hasFleshKnuckles && hasMagicCuffs && hasStarVeil &&
                       barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        // ───────── 综合强化类阵法 Do 和 Check 方法 ─────────
        
        /* ───────── 三才合一阵：所有伤害+10%，防御+6，移动速度+12% ───────── */
        private void DoSanCaiHeYi() {
            // 效果在ResetEffects和ModifyWeaponDamage中处理
        }
        
        private bool CheckSanCaiHeYiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：精金/钛金矿 × 2，神圣锭 × 2，光明之魂 × 2，暗影之魂 × 2
                int adamantiteOreCount = cur.Count(t => t == ItemID.AdamantiteOre);
                int titaniumOreCount = cur.Count(t => t == ItemID.TitaniumOre);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool oreValid = (adamantiteOreCount == 2 && titaniumOreCount == 0) || 
                                (adamantiteOreCount == 0 && titaniumOreCount == 2);
                
                return oreValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 2 &&
                       soulOfNightCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 五行相生阵：所有伤害+12%，防御+10，每15秒回复生命值2%，每15秒回复魔力值3% ───────── */
        private void DoWuXingXiangSheng() {
            // 效果在ResetEffects、ModifyWeaponDamage和PostUpdate中处理
            WuXingXiangShengTimer = 0;
        }
        
        private bool CheckWuXingXiangShengFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：生命果 × 2，精金/钛金锭 × 2，神圣锭 × 2，光明之魂 × 1，暗影之魂 × 1
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int soulOfLightCount = cur.Count(t => t == ItemID.SoulofLight);
                int soulOfNightCount = cur.Count(t => t == ItemID.SoulofNight);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return lifeFruitCount == 2 &&
                       barValid &&
                       hallowedBarCount == 2 &&
                       soulOfLightCount == 1 &&
                       soulOfNightCount == 1;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 六合归一阵：所有伤害+15%，防御+12，每12秒回复生命值3%，每12秒回复魔力值4%，移动速度+18% ───────── */
        private void DoLiuHeGuiYi() {
            // 效果在ResetEffects、ModifyWeaponDamage和PostUpdate中处理
            LiuHeGuiYiTimer = 0;
        }
        
        private bool CheckLiuHeGuiYiFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：生命果 × 2，精金/钛金锭 × 2，神圣锭 × 2，夜明锭 × 2
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int adamantiteBarCount = cur.Count(t => t == ItemID.AdamantiteBar);
                int titaniumBarCount = cur.Count(t => t == ItemID.TitaniumBar);
                int hallowedBarCount = cur.Count(t => t == ItemID.HallowedBar);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                
                bool barValid = (adamantiteBarCount == 2 && titaniumBarCount == 0) || 
                                (adamantiteBarCount == 0 && titaniumBarCount == 2);
                
                return lifeFruitCount == 2 &&
                       barValid &&
                       hallowedBarCount == 2 &&
                       lunarBarCount == 2;
            }
            catch {
                return false;
            }
        }
        
        /* ───────── 太极混元阵：所有伤害+20%，防御+18，每10秒回复生命值5%，每10秒回复魔力值6%，移动速度+25%，召唤物上限+1 ───────── */
        private void DoTaiJiHunYuan() {
            // 效果在ResetEffects、ModifyWeaponDamage和PostUpdate中处理
            TaiJiHunYuanTimer = 0;
        }
        
        private bool CheckTaiJiHunYuanFormation(int[] cur) {
            if (cur == null || cur.Length != 8) return false;
            try {
                // 需要：生命果 × 2，日耀碎片 × 1，星尘碎片 × 1，星云碎片 × 1，星旋碎片 × 1，月亮领主奖章 × 1，夜明锭 × 1
                int lifeFruitCount = cur.Count(t => t == ItemID.LifeFruit);
                int fragmentSolarCount = cur.Count(t => t == ItemID.FragmentSolar);
                int fragmentStardustCount = cur.Count(t => t == ItemID.FragmentStardust);
                int fragmentNebulaCount = cur.Count(t => t == ItemID.FragmentNebula);
                int fragmentVortexCount = cur.Count(t => t == ItemID.FragmentVortex);
                bool hasTrophy = cur.Any(t => t == ItemID.MoonLordTrophy);
                int lunarBarCount = cur.Count(t => t == ItemID.LunarBar);
                
                return lifeFruitCount == 2 &&
                       fragmentSolarCount == 1 &&
                       fragmentStardustCount == 1 &&
                       fragmentNebulaCount == 1 &&
                       fragmentVortexCount == 1 &&
                       hasTrophy &&
                       lunarBarCount == 1;
            }
            catch {
                return false;
            }
        }
    }
}
