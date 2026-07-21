using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.NiuMa
{
    /// <summary>
    /// 勾魂索 — 牛头马面掉落鞭 (马面执勾的差役幻想)。
    /// 三痕收魂: 每鞭刻 1 道魂痕 (上限 3), 对满 3 痕的敌人再鞭触发【收魂】——
    /// 伤害 ×1.8、猛拽向玩家、一缕魂魄飞回己身给予拘魂增益 (+8% 召唤伤害)。
    /// 魂痕本身是鞭 tag: 召唤物命中带痕敌人 +4 伤害。
    /// </summary>
    public class SoulHookWhip : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<SoulHookWhipProjectile>(), 28, 2f, 2f, 20);
            Item.damage = 48;
            Item.knockBack = 4f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Pink;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.ThornWhip;
    }

    /// <summary>
    /// 魂痕 (鞭 tag debuff, 同步 buff 承载"是否带痕", 层数走 <see cref="SoulScratchNPC"/> owner 端计数):
    /// 召唤物与其射弹命中带痕敌人 +4 伤害; 轻微冥焰视觉。
    /// (旧版"持续拉拽 + lifeRegen"已按设计文档移除 — 拉拽收束为收魂峰值的一次性时刻。)
    /// </summary>
    public class SoulHookWhipDebuff : ModBuff
    {
        /// <summary>召唤 tag 固定加伤。</summary>
        public const int TagDamage = 4;

        public override string Texture => "Terraria/Images/Buff_" + BuffID.ShadowFlame;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.IsATagBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame,
                    0f, 0f, 130, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = true;
                d.velocity *= 0.35f;
            }
        }
    }

    /// <summary>
    /// 魂痕层数 (每 NPC 实例): owner 客户端累计 (鞭命中判定本就在 owner 端), 满 3 痕由第四鞭收魂。
    /// 同时承担鞭 tag 加伤钩子与头顶抓痕绘制。
    /// </summary>
    public class SoulScratchNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public const int MaxStacks = 3;
        /// <summary>魂痕衰减帧 (8s 无续叠清零)。</summary>
        public const int Decay = 480;

        public int Stacks;
        public int Timer;
        public int OwnerWho = -1;

        public override void PostAI(NPC npc) {
            if (Stacks <= 0)
                return;
            if (--Timer <= 0) {
                Stacks = 0;
                OwnerWho = -1;
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            // 鞭 tag: 召唤物/召唤射弹命中带痕敌人 +4 (buff 经网络同步, 各端一致)
            if ((projectile.minion || ProjectileID.Sets.MinionShot[projectile.type])
                && npc.HasBuff(ModContent.BuffType<SoulHookWhipDebuff>())) {
                modifiers.FlatBonusDamage += SoulHookWhipDebuff.TagDamage;
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Stacks <= 0 || Main.dedServ)
                return;
            Texture2D slash = ACMAsset.LightShot;
            if (slash == null)
                return;

            // 头顶 1~3 道幽紫抓痕; 满痕时加速脉动 (收魂就绪预警)
            float pulse = Stacks >= MaxStacks
                ? 0.55f + 0.45f * MathF.Abs(MathF.Sin((float)Main.GlobalTimeWrappedHourly * 6.5f))
                : 0.85f;
            float spacing = 12f;
            Vector2 basePos = npc.Top - screenPos + new Vector2(-(Stacks - 1) * spacing * 0.5f, -18f);
            for (int i = 0; i < Stacks && i < MaxStacks; i++) {
                Color c = Color.Lerp(NiuMaSoulChainVFX.HorseBloom, NiuMaSoulChainVFX.HorseCore, i / 2f) * pulse;
                c.A = 0;
                spriteBatch.Draw(slash, basePos + new Vector2(i * spacing, 0f), null, c,
                    MathHelper.PiOver2 + 0.35f, slash.Size() * 0.5f, new Vector2(0.5f, 0.14f), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>拘魂 (收魂后的召唤增益): +8% 召唤伤害。</summary>
    public class SoulCaptureBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Wrath;

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.GetDamage(DamageClass.Summon) += 0.08f;
        }
    }

    /// <summary>
    /// 勾魂索鞭体: 原版鞭骨架 + 甩鞭 crack 帧反馈 + 链节铺设 (与冥链刃同族的锁链语言, 幽紫配色)
    /// + 魂链条带着色器 + 鞭梢勾光。命中叠魂痕, 满痕收魂。
    /// </summary>
    public class SoulHookWhipProjectile : ModProjectile
    {
        // 复用鞭控制点缓冲, 避免每帧 List 分配
        private static readonly List<Vector2> _whipPoints = new(24);

        private bool _cracked;          // 本次挥鞭是否已过最远点
        private float _crackFlash;      // crack 帧柔光衰减
        private bool _harvestHit;       // ModifyHit → OnHit 传递收魂判定 (同帧, owner 端)

        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20;
            Projectile.WhipSettings.RangeMultiplier = 1f;
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThornWhip;

        private ref float Timer => ref Projectile.ai[0];

        public override void AI() {
            if (_crackFlash > 0f)
                _crackFlash -= 0.12f;

            // 甩鞭最远点 = crack 帧: 鞭啸 + 鞭梢闪 (原版鞭动画自带前摇, 这里补上爆发帧反馈)
            Projectile.GetWhipSettings(Projectile, out float timeToFlyOut, out _, out _);
            if (!_cracked && Timer >= timeToFlyOut / 2f) {
                _cracked = true;
                _crackFlash = 1f;
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.5f, Pitch = 0.4f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);

                // 鞭梢破空尘
                _whipPoints.Clear();
                Projectile.FillWhipControlPoints(Projectile, _whipPoints);
                if (_whipPoints.Count > 1) {
                    Vector2 tip = _whipPoints[^1];
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustPerfect(tip, DustID.Shadowflame,
                            Main.rand.NextVector2Circular(3.5f, 3.5f), 60, default, 1.2f);
                        d.noGravity = true;
                    }
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            _harvestHit = false;
            var scratch = target.GetGlobalNPC<SoulScratchNPC>();
            if (scratch.Stacks >= SoulScratchNPC.MaxStacks && scratch.OwnerWho == Projectile.owner) {
                // 收魂鞭: ×1.8
                modifiers.FinalDamage *= 1.8f;
                _harvestHit = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player owner = Main.player[Projectile.owner];

            // 鞭 tag (同步 buff) + 鞭标准聚焦
            target.AddBuff(ModContent.BuffType<SoulHookWhipDebuff>(), SoulScratchNPC.Decay);
            owner.MinionAttackTargetNPC = target.whoAmI;
            Projectile.damage = (int)(Projectile.damage * 0.7f); // 多目标衰减 (鞭标配)

            if (Projectile.owner != Main.myPlayer)
                return;

            var scratch = target.GetGlobalNPC<SoulScratchNPC>();

            if (_harvestHit) {
                _harvestHit = false;
                DoHarvest(owner, target, scratch);
                return;
            }

            // 叠痕 (换主人则重新从 1 计)
            if (scratch.OwnerWho != Projectile.owner)
                scratch.Stacks = 0;
            scratch.OwnerWho = Projectile.owner;
            scratch.Stacks = Math.Min(scratch.Stacks + 1, SoulScratchNPC.MaxStacks);
            scratch.Timer = SoulScratchNPC.Decay;

            // 刻痕反馈: 音高随层数上行 ("一痕、二痕、三痕"听觉读条)
            SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.4f, Pitch = -0.1f + scratch.Stacks * 0.15f }, target.Center);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, 0.7f + scratch.Stacks * 0.1f, Projectile.owner);

            for (int i = 0; i < 4 + scratch.Stacks * 2; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Wraith,
                    Main.rand.NextVector2Circular(3f, 3f), 80, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        /// <summary>收魂 (第四鞭峰值): 清痕 + 猛拽向玩家 + 魂魄飞回 + 演出栈。仅 owner 端。</summary>
        private void DoHarvest(Player owner, NPC target, SoulScratchNPC scratch) {
            scratch.Stacks = 0;
            scratch.OwnerWho = -1;

            // 猛拽向玩家 (保留旧版"勾向玩家"的时刻感, 收束为峰值一次性冲量)
            Vector2 pullDir = (owner.Center - target.Center).SafeNormalize(Vector2.Zero);
            float dist = Vector2.Distance(owner.Center, target.Center);
            float impulse = MathHelper.Clamp(11f - dist / 90f, 4f, 11f) * (1f - target.knockBackResist * 0.85f);
            if (impulse > 0f)
                target.velocity += pullDir * impulse;

            // 魂魄飞回玩家 (到达时给拘魂 buff)
            Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center,
                pullDir * 7f, ModContent.ProjectileType<SoulHookWisp>(), 0, 0f, Projectile.owner);

            // 收魂演出栈: 双层音 + 震屏 + 大 Burst + 魂尘喷向玩家
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.55f, Pitch = 0.3f }, target.Center);
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.4f, Pitch = -0.2f }, target.Center);
            WeaponVFX.AddScreenShake(target.Center, 2.5f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, 1.6f, Projectile.owner);

            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Wraith,
                    pullDir.RotatedByRandom(0.7) * Main.rand.NextFloat(2f, 6f), 60, default, Main.rand.NextFloat(1.1f, 1.6f));
                d.noGravity = true;
            }
        }

        // ===== 绘制: 链节鞭体 + 魂链条带 + 鞭梢勾光 =====

        public override bool PreDraw(ref Color lightColor) {
            _whipPoints.Clear();
            Projectile.FillWhipControlPoints(Projectile, _whipPoints);
            if (_whipPoints.Count < 2)
                return false;

            Vector2[] pts = _whipPoints.ToArray();

            // 魂链条带垫底 (幽紫; crack 后行波从根跑向梢)
            Projectile.GetWhipSettings(Projectile, out float timeToFlyOut, out _, out _);
            float swingT = MathHelper.Clamp(Timer / MathF.Max(timeToFlyOut, 1f), 0f, 1f);
            float pulsePos = _cracked ? MathHelper.Clamp((swingT - 0.5f) * 2.4f, 0f, 1f) : -1f;
            NiuMaSoulChainVFX.DrawSoulChainStrip(pts, 6f,
                NiuMaSoulChainVFX.HorseCore, NiuMaSoulChainVFX.HorseDeep,
                0.75f, pulsePos, pulseGlow: 1.2f, flowSpeed: 1.3f);

            // 锁链分节铺设 (与冥链刃同族语言, 偏紫染色)
            NiuMaSoulChainVFX.DrawChainLinks(pts, new Color(150, 120, 190, 200), 0.72f);

            // 鞭梢"勾"光: crack 闪 + 常态微光
            Texture2D hook = ACMAsset.LightShot;
            if (hook != null) {
                Vector2 tip = pts[^1];
                Vector2 tipDir = (pts[^1] - pts[^2]).SafeNormalize(Vector2.UnitX);
                float flash = 0.45f + _crackFlash * 0.8f;
                Color c = NiuMaSoulChainVFX.HorseCore * flash;
                c.A = 0;
                Main.spriteBatch.Draw(hook, tip - Main.screenPosition, null, c,
                    tipDir.ToRotation(), hook.Size() * 0.5f, new Vector2(0.6f + _crackFlash * 0.4f, 0.28f), SpriteEffects.None, 0f);
            }
            if (_crackFlash > 0.5f)
                WeaponVFX.DrawGlowBurst(pts[^1], 0.8f * _crackFlash, NiuMaSoulChainVFX.HorseBloom * 0.8f);

            return false;
        }
    }

    /// <summary>
    /// 收魂魂魄: 从敌人处螺旋飞回玩家的一缕幽紫魂焰, 抵达时授予拘魂 buff (+8% 召唤伤害 4s)。
    /// 纯演出弹幕 (damage 0)。
    /// </summary>
    public class SoulHookWisp : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.SoulHookWisp.DisplayName",
                () => "Captured Soul");
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            // 螺旋归巢: 追向玩家, 带一点侧向摆动 (魂魄游动感)
            Vector2 toOwner = owner.Center - Projectile.Center;
            float dist = toOwner.Length();
            if (dist < 24f) {
                Arrive(owner);
                return;
            }

            float speed = MathHelper.Lerp(7f, 17f, MathHelper.Clamp(1f - dist / 500f, 0f, 1f));
            Vector2 desired = toOwner.SafeNormalize(Vector2.Zero) * speed;
            desired += desired.RotatedBy(MathHelper.PiOver2) * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI) * 0.3f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith,
                    -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.8f, 0.8f), 100, default, 1.1f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.35f, 0.2f, 0.55f);
        }

        private void Arrive(Player owner) {
            // 拘魂增益仅对 owner 本人生效 (own-client AddBuff 自然同步)
            if (Projectile.owner == Main.myPlayer)
                owner.AddBuff(ModContent.BuffType<SoulCaptureBuff>(), 240);

            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.25f }, owner.Center);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(owner.Center, DustID.Wraith,
                    Main.rand.NextVector2CircularEdge(3f, 3f), 60, default, 1.2f);
                d.noGravity = true;
            }
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            // 幽紫双层魂尾 + 头部柔光
            WeaponVFX.DrawProjectileTrail(Projectile, 7f,
                NiuMaSoulChainVFX.HorseDeep with { A = 140 }, NiuMaSoulChainVFX.HorseCore with { A = 190 },
                uvScroll: -(float)Main.GlobalTimeWrappedHourly * 1.5f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.55f, NiuMaSoulChainVFX.HorseBloom * 0.75f);
            return false;
        }
    }
}
