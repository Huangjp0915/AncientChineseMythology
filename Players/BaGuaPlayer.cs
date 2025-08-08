using AncientChineseMythology.Items.Materials;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static AncientChineseMythology.AncientChineseMythologyNetwork;

namespace AncientChineseMythology.Players
{
    public class BaGuaPlayer : ModPlayer
    {
        // 常量：倍率
        private const float Scale = 0.10f;
        public const int SlotCount = 8;
        public Item[] BaGuaItems = new Item[SlotCount];
        public string CurrentName = "";
        public string CurrentDesc = "";
        private const int WearInterval = 60 * 60 * 30;      // 30
        private int[] wearCounter = new int[SlotCount];

        /*  ----------------- 阵法内定义 ----------------- */
        private bool phoenixActive;
        private int phoenixCD;
        private const int PhoenixCDMax = 60 * 60 * 10; // 10 min冷却
        private static readonly int[] BoomerangIDs = {
            ProjectileID.Flamarang,
            ProjectileID.EnchantedBoomerang,
            ProjectileID.WoodenBoomerang,
            ProjectileID.IceBoomerang,
            ProjectileID.Shroomerang
        };
        private int FallingStarTimer;
        private const int FallingStarCD = 15;   // 每 15 tick ≈ 0.25 s 召唤 1 颗

        public override void PostUpdateEquips() {
            // 若拥有八卦 Buff，则缩放最终生命 / 魔力
            if (Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                // 1. 缩放最大值
                Player.statLifeMax2 = (int)(Player.statLifeMax2 * Scale);
                Player.statManaMax2 = (int)(Player.statManaMax2 * Scale);

                // 2. 防止当前值溢出新上限
                if (Player.statLife > Player.statLifeMax2)
                    Player.statLife = Player.statLifeMax2;

                if (Player.statMana > Player.statManaMax2)
                    Player.statMana = Player.statManaMax2;
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
            // 只在玩家带着 BaGuaBuff 时消耗材料；去掉这行就永久计时
            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>()))
                return;

            for (int i = 0; i < SlotCount; i++) {
                if (BaGuaItems[i].IsAir) { wearCounter[i] = 0; continue; }

                if (++wearCounter[i] >= WearInterval) {
                    wearCounter[i] = 0;

                    if (BaGuaItems[i].stack > 1)
                        BaGuaItems[i].stack--;       // 掉 1 个
                    else
                        BaGuaItems[i].TurnToAir();   // 没了就清空

                    // 多人联机同步
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
            /* ... 可继续追加 ... */
        };

        public override void ResetEffects() {
            // 默认清空显示文本
            CurrentName = "";
            CurrentDesc = "";

            phoenixActive = false;
            if (phoenixCD > 0) phoenixCD--;

            if (!Player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>()))
                return;

            int[] cur = BaGuaItems.Where(it => !it.IsAir).Select(it => it.type).ToArray();

            foreach (var f in Formations) {
                if (cur.Length == f.RequiredTypes.Length &&
                    cur.All(t => f.RequiredTypes.Contains(t))) {
                    CurrentName = f.Name;
                    CurrentDesc = f.Desc;
                    f.ApplyEffect(this);
                    break;          // 命中一个即可
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

        /* ───────── 镇海阵：防+15、荆棘、减速 ───────── */
        private void DoZhenHai() {
            // 1) 额外防御 +15
            Player.statDefense += 15;

            // 2) 100% 荆棘反伤：1f == 100%（与荆棘药剂相同）
            Player.thorns += 1f;

            // 3) 移动速度 -15%
            Player.moveSpeed -= 0.4f;          // 整体加速系数
            Player.maxRunSpeed *= 0.6f;        // 封顶跑速也同步降低，手感更一致
        }

        /* ───────── 朱雀涅槃：一次性复活 ───────── */
        public void DoPhoenix() {
            phoenixActive = true;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp,
                                     ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (phoenixActive && phoenixCD == 0) {
                PhoenixRebirth();
                phoenixCD = PhoenixCDMax;
                return false;   // 阻止死亡
            }
            return true;        // 允许死亡
        }

        private void PhoenixRebirth() {
            int heal = Player.statLifeMax2 / 2;
            Player.statLife = heal;
            Player.HealEffect(heal, true);

            Player.immune = true;
            Player.immuneTime = 120;        // 2 秒无敌

            // 400 点范围火爆
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

            // 寻找最近的非友好 NPC（与回旋镖逻辑保持一致）
            NPC target = null;
            float dist2 = 600 * 600;
            foreach (NPC npc in Main.npc)
                if (npc.CanBeChasedBy(Player) && !npc.friendly) {
                    float d = Vector2.DistanceSquared(npc.Center, Player.Center);
                    if (d < dist2) { dist2 = d; target = npc; }
                }
            if (target == null) return;

            // 确定星星出生点：目标上方 600 像素随机 ±80 X 偏移
            Vector2 spawn = new(target.Center.X + Main.rand.Next(-80, 81), target.Center.Y - 600f);
            Vector2 vel = Vector2.UnitY * 16f;           // 垂直向下

            int dmg = 80;      // 调整为想要的伤害
            float kb = 1.5f;   // 击退

            Projectile.NewProjectile(
                Player.GetSource_FromThis(),
                spawn, vel,
                ProjectileID.Starfury, // 原版星怒坠星弹道 & 贴图
                dmg, kb, Player.whoAmI);
        }
    }
}
