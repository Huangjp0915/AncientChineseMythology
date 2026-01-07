using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers.Items
{
    /// <summary>
    /// 天罚审判轮 - 天庭观察者掉落的回旋镖
    /// 投掷后自动追踪敌人并返回，击中敌人时释放审判光环
    /// </summary>
    public class CelestialJudgmentChakram : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<CelestialChakramProjectile>();
            Item.shootSpeed = 18f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<CelestialChakramProjectile>()] < 2;
        }
    }

    /// <summary>
    /// 天罚审判轮弹幕 - 追踪回旋镖
    /// </summary>
    public class CelestialChakramProjectile : ModProjectile
    {
        private enum ChakramState { Flying, Tracking, Returning }

        private ChakramState State {
            get => (ChakramState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Timer => ref Projectile.ai[1];
        private Player Owner => Main.player[Projectile.owner];
        private NPC targetNPC;
        private float rotationSpeed = 0.3f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += rotationSpeed;

            switch (State) {
                case ChakramState.Flying:
                    HandleFlying();
                    break;
                case ChakramState.Tracking:
                    HandleTracking();
                    break;
                case ChakramState.Returning:
                    HandleReturning();
                    break;
            }

            SpawnChakramParticles();
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.7f) * 0.6f);
        }

        private void HandleFlying() {
            // 减速
            Projectile.velocity *= 0.97f;

            // 寻找目标
            if (Timer > 10) {
                targetNPC = FindClosestNPC(500f);
                if (targetNPC != null) {
                    State = ChakramState.Tracking;
                    Timer = 0;
                }
            }

            // 飞行时间过长开始返回
            if (Timer > 40 || Projectile.velocity.Length() < 3f) {
                State = ChakramState.Returning;
                Timer = 0;
            }
        }

        private void HandleTracking() {
            if (targetNPC == null || !targetNPC.active || targetNPC.life <= 0) {
                State = ChakramState.Returning;
                Timer = 0;
                return;
            }

            // 追踪目标
            Vector2 toTarget = (targetNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float targetSpeed = 20f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * targetSpeed, 0.1f);

            // 追踪超时
            if (Timer > 60) {
                State = ChakramState.Returning;
                Timer = 0;
            }
        }

        private void HandleReturning() {
            Vector2 toOwner = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float returnSpeed = 22f + Timer * 0.3f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner * returnSpeed, 0.12f);

            // 接近玩家时消失
            if (Vector2.Distance(Projectile.Center, Owner.Center) < 30f) {
                Projectile.Kill();
            }
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        private void SpawnChakramParticles() {
            // 光环粒子
            if (Main.rand.NextBool(2)) {
                float dustAngle = Projectile.rotation + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + dustAngle.ToRotationVector2() * 18f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = dustAngle.ToRotationVector2() * 1.5f;
            }

            // 审判光粒
            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 审判光环爆发
            SpawnJudgmentBurst(target.Center);

            // 切换到追踪新目标
            if (State != ChakramState.Returning) {
                targetNPC = null;
                Timer = 0;
                State = ChakramState.Tracking;
            }

            // 减速效果
            target.AddBuff(BuffID.Slow, 120);
        }

        private void SpawnJudgmentBurst(Vector2 position) {
            // 审判光环
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(position, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 星光爆发
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(position, 0, 0, DustID.YellowStarDust, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.5f }, position);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 230, 150) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            // 光晕
            Color glowColor = new Color(255, 240, 180) * 0.4f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor, Projectile.rotation,
                origin, Projectile.scale * 1.3f, SpriteEffects.None, 0f);

            // 主体
            Main.spriteBatch.Draw(texture, drawPos, null, lightColor, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0f);

            // 核心星光
            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = new Color(255, 255, 220) * 0.3f;
                sparkleColor.A = 0;
                float sparkleRot = Projectile.rotation * 0.5f;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, drawPos, null, sparkleColor, sparkleRot,
                    ACMAsset.Sparkle.Size() / 2f, 0.6f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                Vector2 vel = angle.ToRotationVector2() * 3f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
