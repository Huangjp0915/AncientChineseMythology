using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 蔚蓝激流剑群 —— 超级毕业召唤武器，召唤蓝色剑群，3帧动画，悬浮旋转后俯冲追踪敌人
    /// </summary>
    public class AzureTorrentBladesBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<AzureTorrentBladesProj>()] > 0)
                player.buffTime[buffIndex] = 18000;
        }
    }

    public class AzureTorrentBlades : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 440;
            Item.DamageType = DamageClass.Summon;
            Item.mana = 8;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.buffType = ModContent.BuffType<AzureTorrentBladesBuff>();
            Item.shoot = ModContent.ProjectileType<AzureTorrentBladesProj>();
            Item.shootSpeed = 10f;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class AzureTorrentBladesProj : ModProjectile
    {
        // 帧0=剑鞘(待机)  帧1=剑刃(追踪)  帧2=剑刃闪光(贴身突刺)
        private static readonly int TOTAL_FRAMES = 3;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = TOTAL_FRAMES;
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 144;
            Projectile.height = 144;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 0.5f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
            // 关键：独立 NPC 免疫冷却，否则多个召唤物/多次命中无法造成伤害
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        private ref float Timer => ref Projectile.ai[0];
        private ref float LungeTimer => ref Projectile.ai[1];

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<AzureTorrentBladesBuff>())) {
                Projectile.active = false; return;
            }
            Projectile.timeLeft = 2;
            Timer++;

            // 寻找最近目标
            bool hit = false; float rng = 850f; Vector2 tgt = Vector2.Zero;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < rng) { rng = d; tgt = npc.Center; hit = true; }
            }

            if (hit) {
                Vector2 dir = Projectile.DirectionTo(tgt);
                if (rng < 160f) {
                    // ===== 突刺模式：直接高速贯入，确保碰撞箱接触 =====
                    LungeTimer++;
                    Projectile.frame = 2;
                    // 用较高系数 Lerp，使刀快速对准，穿越目标
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 30f, 0.35f);
                }
                else {
                    LungeTimer = MathHelper.Max(LungeTimer - 1f, 0f);
                    // ===== 追踪模式 =====
                    Projectile.frame = 1;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 22f, 0.18f);
                }
                // 刀刃朝向运动方向（+45° 让美术朝正确方向）
                float targetRot = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
                float rotDiff = MathHelper.WrapAngle(targetRot - Projectile.rotation);
                Projectile.rotation += rotDiff * 0.28f;
            }
            else {
                LungeTimer = 0;
                Projectile.frame = 0;
                // 悬浮待机：绕玩家上方轻微律动
                Vector2 idle = owner.Center + new Vector2(
                    -40f * owner.direction + (float)Math.Sin(Timer * 0.04f) * 12f,
                    -90f - (float)Math.Sin(Timer * 0.05f) * 20f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (idle - Projectile.Center) * 0.09f, 0.12f);
                // 待机时刀刃竖立：轻微摆动
                Projectile.rotation = (float)Math.Sin(Timer * 0.05f) * 0.30f - MathHelper.PiOver4;
            }

            Projectile.spriteDirection = Projectile.velocity.X < 0 ? -1 : 1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中时：刀击音效 + 轻微震屏
            SoundEngine.PlaySound(SoundID.Item71, Projectile.position);
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(3.5f, 6);
        }

        public override bool MinionContactDamage() {
            // 允许召唤物本体直接对 NPC 造成接触伤害
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D lsh = ACMAsset.LightShot;
            Texture2D wave = ACMAsset.GlaciateWave;
            Texture2D star = ACMAsset.BlankStar;
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            int fh = tex.Height / TOTAL_FRAMES;
            Rectangle src = new Rectangle(0, Projectile.frame * fh, tex.Width, fh);

            float speed = Projectile.velocity.Length();
            float dashT = MathHelper.Clamp((speed - 5f) / 18f, 0f, 1f);
            float glowA = MathHelper.Clamp(speed / 22f, 0.15f, 0.92f);
            float bladeRot = Projectile.rotation - MathHelper.PiOver4; // 速度方向角

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // === 1. 刀光拖影：沿刀刃方向的幻影 ===
            if (dashT > 0.04f && Projectile.oldPos != null && Projectile.oldRot != null) {
                for (int i = 1; i < 12 && i < Projectile.oldPos.Length; i++) {
                    float ta = (1f - i / 12f) * dashT * 0.62f;
                    float iRot = Projectile.oldRot[i] - MathHelper.PiOver4;
                    Vector2 tp = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    // 沿刀轴方向的细长光束（模拟刀迹余辉）
                    sb.Draw(lsh, tp, null,
                        new Color(50, 160, 255) * ta,
                        iRot,
                        new Vector2(lsh.Width * 0.5f, lsh.Height * 0.5f),
                        new Vector2(0.12f, 0.65f + i * 0.03f), SpriteEffects.None, 0);
                    // 白芯细线
                    sb.Draw(lsh, tp, null,
                        new Color(220, 240, 255) * (ta * 0.40f),
                        iRot,
                        new Vector2(lsh.Width * 0.5f, lsh.Height * 0.5f),
                        new Vector2(0.05f, 0.35f), SpriteEffects.None, 0);
                }
            }

            // === 2. 突刺模式：横向剑气波纹（垂直于速度方向） ===
            if (Projectile.frame == 2 && dashT > 0.3f) {
                float luneA = MathHelper.Clamp(LungeTimer / 8f, 0f, 1f) * dashT;
                float perpRot = bladeRot + MathHelper.PiOver2; // 垂直于刀刃
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    new Color(80, 180, 255) * (luneA * 0.65f),
                    perpRot,
                    new Vector2(wave.Width * 0.5f, wave.Height * 0.5f),
                    new Vector2(0.55f * dashT, 0.12f), SpriteEffects.None, 0);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    new Color(200, 235, 255) * (luneA * 0.32f),
                    perpRot,
                    new Vector2(wave.Width * 0.5f, wave.Height * 0.5f),
                    new Vector2(0.30f * dashT, 0.06f), SpriteEffects.None, 0);
            }

            // === 3. 柔光底色 ===
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(40, 155, 255) * glowA, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 1.3f, SpriteEffects.None, 0);

            // === 4. 帧2 突刺时刀尖星芒 ===
            if (Projectile.frame == 2 && dashT > 0.25f) {
                // 刀尖偏移位置（沿刀刃方向前推约20px）
                Vector2 tipOffset = bladeRot.ToRotationVector2() * 20f;
                sb.Draw(star, Projectile.Center + tipOffset - Main.screenPosition, null,
                    new Color(160, 225, 255) * (dashT * 0.85f),
                    (float)Main.timeForVisualEffects * 0.07f,
                    new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                    0.30f + dashT * 0.10f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 待机帧保持原来柔光方式而非强调
            SpriteEffects se = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, src, lightColor, Projectile.rotation,
                new Vector2(tex.Width * 0.5f, fh * 0.5f), 1.0f, se, 0);
            return false;
        }
    }
}
