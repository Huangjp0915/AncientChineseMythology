using AncientChineseMythology.Helpers;
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
    /// 金凤召唤杖 - 天庭观察者掉落的召唤法杖
    /// 召唤金凤凰跟随玩家，自动攻击敌人并释放火焰攻击
    /// </summary>
    public class GoldenPhoenixSummonStaff : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 2300;
            Item.DamageType = DamageClass.Summon;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GoldenPhoenixMinion>();
            Item.shootSpeed = 0f;
            Item.mana = 10;
            Item.buffType = ModContent.BuffType<GoldenPhoenixBuff>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);

            var projectile = Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            // 召唤特效
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f }, player.Center);
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GoldCoin;
                int dust = Dust.NewDust(player.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }
    }

    /// <summary>
    /// 金凤凰Buff
    /// </summary>
    public class GoldenPhoenixBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GoldenPhoenixMinion>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>
    /// 金凤凰召唤物 - 跟随玩家并自动攻击敌人
    /// </summary>
    public class GoldenPhoenixMinion : ModProjectile
    {
        private enum PhoenixState { Idle, Targeting, Attacking, Diving }

        private PhoenixState State {
            get => (PhoenixState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float AttackCooldown => ref Projectile.localAI[0];

        private NPC targetNPC;
        private float wingPhase = 0f;
        private float glowPulse = 0f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead) {
                player.ClearBuff(ModContent.BuffType<GoldenPhoenixBuff>());
                return;
            }

            if (player.HasBuff(ModContent.BuffType<GoldenPhoenixBuff>())) {
                Projectile.timeLeft = 2;
            }

            StateTimer++;
            wingPhase += 0.15f;
            glowPulse += 0.1f;
            if (AttackCooldown > 0) AttackCooldown--;

            switch (State) {
                case PhoenixState.Idle:
                    HandleIdleState(player);
                    break;
                case PhoenixState.Targeting:
                    HandleTargetingState(player);
                    break;
                case PhoenixState.Attacking:
                    HandleAttackingState(player);
                    break;
                case PhoenixState.Diving:
                    HandleDivingState(player);
                    break;
            }

            SpawnPhoenixParticles();
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.7f, 0.3f) * 0.7f);
        }

        private void HandleIdleState(Player player) {
            // 优雅地环绕玩家飞行
            float orbitAngle = Main.GlobalTimeWrappedHourly * 2f + Projectile.whoAmI * MathHelper.PiOver2;
            float orbitRadius = 100f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 30f;
            float heightOffset = MathF.Sin(Main.GlobalTimeWrappedHourly * 2f) * 40f - 50f;

            Vector2 targetPos = player.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle) * 0.5f) * orbitRadius;
            targetPos.Y += heightOffset;

            Vector2 toTarget = targetPos - Projectile.Center;
            float speed = MathHelper.Clamp(toTarget.Length() * 0.12f, 3f, 18f);

            if (toTarget.Length() > 15f) {
                Projectile.velocity = toTarget.SafeNormalize(Vector2.Zero) * speed;
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            // 面向移动方向
            if (Projectile.velocity.Length() > 1f) {
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, Projectile.velocity.ToRotation(), 0.15f);
            }

            Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;

            // 寻找目标
            if (AttackCooldown <= 0) {
                targetNPC = FindTarget(player, 700f);
                if (targetNPC != null) {
                    State = PhoenixState.Targeting;
                    StateTimer = 0;
                }
            }
        }

        private void HandleTargetingState(Player player) {
            if (targetNPC == null || !targetNPC.active || targetNPC.life <= 0) {
                State = PhoenixState.Idle;
                StateTimer = 0;
                return;
            }

            // 移动到攻击位置（目标上方）
            Vector2 attackPos = targetNPC.Center + new Vector2(0, -180);
            Vector2 toAttackPos = attackPos - Projectile.Center;

            if (toAttackPos.Length() > 80f) {
                Projectile.velocity = toAttackPos.SafeNormalize(Vector2.Zero) * 20f;
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }
            else {
                // 选择攻击方式
                if (Main.rand.NextBool(3)) {
                    State = PhoenixState.Diving;
                }
                else {
                    State = PhoenixState.Attacking;
                }
                StateTimer = 0;
                Projectile.velocity = Vector2.Zero;
            }

            if (StateTimer > 60) {
                State = PhoenixState.Attacking;
                StateTimer = 0;
            }
        }

        private void HandleAttackingState(Player player) {
            if (targetNPC == null || !targetNPC.active) {
                State = PhoenixState.Idle;
                StateTimer = 0;
                AttackCooldown = 30;
                return;
            }

            // 悬停并发射火焰
            Projectile.velocity *= 0.9f;

            // 面向目标
            Vector2 toTarget = targetNPC.Center - Projectile.Center;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, toTarget.ToRotation(), 0.2f);

            // 发射火焰
            if (StateTimer == 10 || StateTimer == 20 || StateTimer == 30) {
                FirePhoenixFlame();
            }

            if (StateTimer > 40) {
                State = PhoenixState.Idle;
                StateTimer = 0;
                AttackCooldown = 50;
                targetNPC = null;
            }
        }

        private void HandleDivingState(Player player) {
            if (targetNPC == null || !targetNPC.active) {
                State = PhoenixState.Idle;
                StateTimer = 0;
                AttackCooldown = 30;
                return;
            }

            // 俯冲攻击
            if (StateTimer < 10) {
                // 蓄力
                Projectile.velocity *= 0.9f;
                Projectile.rotation = (targetNPC.Center - Projectile.Center).ToRotation();
            }
            else if (StateTimer < 30) {
                // 俯冲
                Vector2 toTarget = (targetNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = toTarget * 25f;
                Projectile.rotation = Projectile.velocity.ToRotation();

                // 俯冲火焰拖尾
                if (Main.rand.NextBool()) {
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
                }
            }
            else {
                State = PhoenixState.Idle;
                StateTimer = 0;
                AttackCooldown = 60;
                targetNPC = null;
            }
        }

        private void FirePhoenixFlame() {
            if (Main.netMode == NetmodeID.MultiplayerClient || targetNPC == null) return;

            Vector2 toTarget = (targetNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);

            Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                Projectile.Center,
                toTarget * 14f,
                ModContent.ProjectileType<PhoenixMinionFlame>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner
            );

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
        }

        private NPC FindTarget(Player player, float maxDistance) {
            if (player.HasMinionAttackTargetNPC) {
                NPC targeted = Main.npc[player.MinionAttackTargetNPC];
                if (targeted.active && targeted.CanBeChasedBy() && !targeted.friendly) {
                    float dist = Vector2.Distance(targeted.Center, Projectile.Center);
                    if (dist < maxDistance * 1.5f) {
                        return targeted;
                    }
                }
            }

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

        private void SpawnPhoenixParticles() {
            // 翅膀火焰
            if (Main.rand.NextBool(3)) {
                float wingOffset = MathF.Sin(wingPhase) * 20f;
                Vector2 perpendicular = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                Vector2 dustPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f + perpendicular * wingOffset;

                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.OrangeTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1, 1);
            }

            // 金色光粒
            if (Main.rand.NextBool(6)) {
                int dust = Dust.NewDust(Projectile.Center + Main.rand.NextVector2Circular(20, 20), 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool? CanDamage() {
            return State == PhoenixState.Diving && StateTimer >= 10 && StateTimer < 30;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);

            // 俯冲爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 0.6f }, target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.ClockworkGold, 1.3f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 俯冲时火羽双层 ribbon (外暗金 + 内亮金)
            if (State == PhoenixState.Diving)
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 20f,
                    outerColor: new Color(200, 90, 25, 120), innerColor: new Color(255, 235, 150, 180),
                    uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            float glow = 1f + MathF.Sin(glowPulse) * 0.15f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 150, 50) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * progress, effects, 0f);
            }

            // 外层光晕
            Color glowColor = new Color(255, 200, 100) * 0.4f * glow;
            glowColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor, Projectile.rotation, origin,
                Projectile.scale * 1.2f, effects, 0f);

            // 主体
            Main.spriteBatch.Draw(texture, drawPos, null, Color.White, Projectile.rotation, origin,
                Projectile.scale, effects, 0f);

            // 核心高光
            Color coreColor = new Color(255, 255, 200) * 0.5f * glow;
            coreColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, coreColor, Projectile.rotation, origin,
                Projectile.scale * 0.8f, effects, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 凤凰召唤物火焰 - 召唤物发射的火焰弹
    /// </summary>
    public class PhoenixMinionFlame : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 轻微追踪
            NPC target = FindClosestNPC(300f);
            if (target != null && Projectile.timeLeft > 30) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float targetAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.05f);
                Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
            }

            // 火焰粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.OrangeTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.6f, 0.2f) * 0.5f);
        }

        private NPC FindClosestNPC(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 120);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.ClockworkGold, 0.7f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
                outerColor: new Color(200, 90, 25, 120), innerColor: new Color(255, 225, 130, 180),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            Texture2D texture = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 100, 50), 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, 0f, origin, 0.6f * progress, SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 200, 100);
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, 0f, origin, 0.7f, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = new Color(255, 255, 200);
            coreColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, coreColor * 0.6f, 0f, origin, 0.4f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.OrangeTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
