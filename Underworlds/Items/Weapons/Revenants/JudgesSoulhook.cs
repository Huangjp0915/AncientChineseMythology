using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 判官勾魂枪 - 判官用以勾取有罪之魂的长枪，近战长枪类武器
    /// 肉后中期，三连段手持突刺 (刺→疾刺→勾魂上挑)：一二段命中 +1 业、三段 +2 业,
    /// 命中固定吸取 3 HP; 三段勾在目标业力将满 (≥5) 时命中触发"灵魂剥离"大补时刻。
    /// </summary>
    public class JudgesSoulhook : ModItem
    {
        /// <summary>连段计数 (0/1/2 循环)。Shoot 仅在 owner 客户端执行, 经弹幕 ai[2] 下发连段号。</summary>
        private int comboStep;

        public override void SetDefaults() {
            Item.damage = 66;
            Item.crit = 6;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            //起刺音效移交弹幕爆发帧播放 (音高随连段上行), 物品本身不出声
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<JudgesSoulhookProjectile>();
            Item.shootSpeed = 4f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<JudgesSoulhookProjectile>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0f, 0f, comboStep);
            comboStep = (comboStep + 1) % 3;
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 判官勾魂枪弹幕 - 三连段手持突刺长枪 (ai[2]=连段号 0/1/2)。
    /// 一段刺 42px / 二段疾刺 52px / 三段勾魂上挑 78px (蓄势抖枪 + 弧形上旋 ~0.35rad, 伤害 ×1.25)。
    /// 前摇 quad 回拉 -10px → 爆发 poly ease-out (前 1-2 帧完成 ~80% 行程) → SmoothStep 收招。
    /// 使用ACMAsset.LightShot叠加枪尖光弹，ACMAsset.SoftGlow绘制勾魂光圈
    /// </summary>
    public class JudgesSoulhookProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/JudgesSoulhook";

        private enum AttackStage { Prepare, Thrust, Retract }
        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set {
                Projectile.ai[0] = (float)value;
                Timer = 0;
            }
        }

        private ref float Timer => ref Projectile.ai[1];
        private ref float ThrustDistance => ref Projectile.localAI[0];
        private const float BaseOffset = 4f;
        private const float PullbackDistance = 10f;
        private Player Owner => Main.player[Projectile.owner];

        /// <summary>连段号 (0=一段刺 1=二段疾刺 2=三段勾魂上挑), 由物品 Shoot 经 ai[2] 传入。</summary>
        private int Combo => (int)Projectile.ai[2];

        /// <summary>三段蓄势期的枪尖抖动偏移 (纯视觉, 各客户端各自随机)。</summary>
        private Vector2 tipJitter;

        private float AttackSpeed => Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float MaxThrust => Combo switch { 0 => 42f, 1 => 52f, _ => 78f };
        private float PrepareTime => (Combo switch { 0 => 4f, 1 => 3f, _ => 8f }) / AttackSpeed;
        private float ThrustTime => (Combo == 1 ? 5f : 6f) / AttackSpeed;
        private float RetractTime => (Combo == 2 ? 10f : 8f) / AttackSpeed;

        /// <summary>三段上挑弧偏角: Thrust 内随进度向上旋 ~0.35rad, Retract 保持挑起角回拉。</summary>
        private float ArcOffset {
            get {
                if (Combo != 2)
                    return 0f;
                float t = CurrentStage switch {
                    AttackStage.Thrust => MathHelper.Clamp(Timer / ThrustTime, 0f, 1f),
                    AttackStage.Retract => 1f,
                    _ => 0f,
                };
                //屏幕 Y 向下, 面右取负角、面左取正角才是"向上"挑
                return -0.35f * t * Projectile.spriteDirection;
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 68;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        //位置每帧由 AI 钉在手上, velocity 只复用作瞄准方向 (随弹幕自动同步)
        public override bool ShouldUpdatePosition() => false;

        public override void OnSpawn(IEntitySource source) {
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            //前摇期 owner 持续跟枪, 进入爆发前一帧锁向并同步给其他客户端
            if (CurrentStage == AttackStage.Prepare && Main.myPlayer == Projectile.owner) {
                Projectile.velocity = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
                if (Timer >= PrepareTime - 1f)
                    Projectile.netUpdate = true;
            }

            switch (CurrentStage) {
                case AttackStage.Prepare:
                    HandlePrepare();
                    break;
                case AttackStage.Thrust:
                    HandleThrust();
                    break;
                case AttackStage.Retract:
                    HandleRetract();
                    break;
            }

            UpdatePositionAndRotation();
            SpawnSoulhookParticles();

            //判官绿色光照
            Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.3f);

            Timer++;
        }

        private void HandlePrepare() {
            float progress = MathHelper.Clamp(Timer / PrepareTime, 0f, 1f);
            //quad in-out 回拉蓄势 (-10px 起手)
            float pull = progress < 0.5f
                ? 2f * progress * progress
                : 1f - MathF.Pow(-2f * progress + 2f, 2f) * 0.5f;
            ThrustDistance = -PullbackDistance * pull;

            //三段: 蓄势期枪尖抖动 ±1.5px, 末 2 帧静止 (凝势读招帧)
            tipJitter = Combo == 2 && Timer < PrepareTime - 2f
                ? Main.rand.NextVector2Circular(1.5f, 1.5f)
                : Vector2.Zero;

            if (Timer >= PrepareTime) {
                tipJitter = Vector2.Zero;
                CurrentStage = AttackStage.Thrust;

                //起刺音: 音高随连段上行 (0/0.15/0.3) + 随机微扰; 三段叠低音重锤
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Pitch = 0.15f * Combo + Main.rand.NextFloat(-0.1f, 0.1f)
                }, Projectile.Center);
                if (Combo == 2)
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.35f }, Projectile.Center);

                //爆发帧冲击粒子: 沿刺出方向自枪尖喷薄
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 tip = Projectile.Center + dir * 35f;
                int burstCount = Combo == 2 ? 10 : 6;
                for (int i = 0; i < burstCount; i++) {
                    Dust d = Dust.NewDustPerfect(
                        tip, DustID.GreenTorch,
                        dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 8f),
                        100, default, Main.rand.NextFloat(1.1f, 1.6f)
                    );
                    d.noGravity = true;
                }
            }
        }

        private void HandleThrust() {
            float progress = MathHelper.Clamp(Timer / ThrustTime, 0f, 1f);
            //poly ease-out 爆发: 前 1-2 帧完成 ~80% 行程, 余帧缓收
            float burst = MathF.Pow(progress, 0.12f);
            ThrustDistance = MathHelper.Lerp(-PullbackDistance, MaxThrust, burst);
            if (Timer >= ThrustTime) {
                CurrentStage = AttackStage.Retract;
            }
        }

        private void HandleRetract() {
            float progress = MathHelper.Clamp(Timer / RetractTime, 0f, 1f);
            ThrustDistance = MathHelper.SmoothStep(MaxThrust, 0f, progress);
            if (Timer >= RetractTime) {
                Projectile.Kill();
            }
        }

        private void UpdatePositionAndRotation() {
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(ArcOffset);
            Projectile.rotation = direction.ToRotation();
            Owner.direction = Projectile.spriteDirection;

            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            Vector2 handPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);
            handPosition.Y += Owner.gfxOffY;
            Projectile.Center = handPosition + direction * (BaseOffset + ThrustDistance) + tipJitter;
        }

        private void SpawnSoulhookParticles() {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 tipPos = Projectile.Center + dir * 35f;

            //蓄势收束: 粒子自外圈汇向枪尖 (勾魂吸聚感)
            if (CurrentStage == AttackStage.Prepare && Main.rand.NextBool(2)) {
                Vector2 from = tipPos + Main.rand.NextVector2CircularEdge(26f, 26f);
                Dust gather = Dust.NewDustPerfect(
                    from, DustID.GreenTorch, (tipPos - from) * 0.16f,
                    120, default, Main.rand.NextFloat(0.8f, 1.2f)
                );
                gather.noGravity = true;
            }

            //突刺时枪尖勾魂粒子
            if (CurrentStage == AttackStage.Thrust && Main.rand.NextBool(2)) {
                Dust hook = Dust.NewDustDirect(
                    tipPos, 8, 8, DustID.GreenTorch,
                    0f, 0f, 100, default, Main.rand.NextFloat(1.0f, 1.6f)
                );
                hook.noGravity = true;
                hook.velocity = -dir * 2f + Main.rand.NextVector2Circular(1f, 1f);
            }

            //枪身暗影粒子
            if (Main.rand.NextBool(4)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center, 10, 10, DustID.Shadowflame,
                    0f, 0f, 150, default, Main.rand.NextFloat(0.6f, 1.0f)
                );
                shadow.noGravity = true;
            }

            //勾魂锁链闪光（枪尖处）
            if (CurrentStage == AttackStage.Thrust && Main.rand.NextBool(6)) {
                Dust chain = Dust.NewDustDirect(
                    tipPos + Main.rand.NextVector2Circular(8, 8),
                    4, 4, DustID.Wraith,
                    0f, -1.5f, 120, default, 1.2f
                );
                chain.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool hookFinisher = Combo == 2;

            //宣判预读: 三段 +2 业即将业满 (业力 ≥5) 的"灵魂剥离"时刻, 须在 AddKarma 前判层
            bool soulRip = hookFinisher && target.GetGlobalNPC<RevenantKarmaGlobalNPC>().Karma >= 5;

            RevenantKarma.AddKarma(Projectile, target, hookFinisher ? 2 : 1);

            //勾魂固定吸取 (仅 owner 本地 Heal)
            if (Main.myPlayer == Projectile.owner) {
                Owner.Heal(3);
                if (soulRip)
                    Owner.Heal(12);
            }

            if (hookFinisher) {
                //灵魂剥离: 减速 + 暗影焰
                target.AddBuff(BuffID.Slow, 120);
                target.AddBuff(BuffID.ShadowFlame, 120);
                //勾拽: 非 Boss 且可受击退者被拽向玩家 (owner 端结算, 接受轻微视觉差)
                if (!target.boss && target.knockBackResist > 0f)
                    target.velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * 6f;
                WeaponVFX.AddScreenShake(target.Center, 2.5f);
            }

            //勾魂命中演出: 鬼绿径向辉光 (更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GhostGreen, scale: hookFinisher ? 1.2f : 0.8f, owner: Projectile.owner);

            if (soulRip) {
                //灵魂剥离大补演出: 追加更大鬼绿爆发 + 魂流回饲
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.GhostGreen, scale: 1.4f, owner: Projectile.owner);
                for (int i = 0; i < 10; i++) {
                    Vector2 velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 9f);
                    velocity = velocity.RotatedByRandom(MathHelper.ToRadians(20));
                    Dust soul = Dust.NewDustPerfect(
                        target.Center, DustID.GreenTorch, velocity,
                        100, default, Main.rand.NextFloat(1.3f, 1.8f)
                    );
                    soul.noGravity = true;
                }
            }

            //基础勾魂反馈: 少量魂流向玩家 + 暗影焰迸散
            for (int i = 0; i < 4; i++) {
                Vector2 velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 7f);
                velocity = velocity.RotatedByRandom(MathHelper.ToRadians(25));
                Dust soul = Dust.NewDustPerfect(
                    target.Center, DustID.GreenTorch, velocity,
                    100, default, Main.rand.NextFloat(1.1f, 1.5f)
                );
                soul.noGravity = true;
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    100, default, 1.3f
                );
                burst.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.4f }, target.Center);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 55f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 20f, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 55f);
            Utils.PlotTileLine(start, end, 20f, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() {
            return CurrentStage == AttackStage.Thrust;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            //三段勾魂重击
            if (Combo == 2)
                modifiers.FinalDamage *= 1.25f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2;
            float rotationOffset;
            SpriteEffects effects;

            if (Projectile.spriteDirection > 0) {
                rotationOffset = MathHelper.PiOver4;
                effects = SpriteEffects.None;
            }
            else {
                rotationOffset = MathHelper.Pi - MathHelper.PiOver4;
                effects = SpriteEffects.FlipHorizontally;
            }

            //绘制主体
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0
            );

            //三段勾魂: 枪身整程叠一层鬼绿挥舞辉光 (a=0 加色)
            if (Combo == 2) {
                Color hookGlow = new Color(90, 230, 140) * (CurrentStage == AttackStage.Thrust ? 0.55f : 0.32f);
                hookGlow.A = 0;
                Main.EntitySpriteDraw(
                    texture, Projectile.Center - Main.screenPosition, null, hookGlow,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.12f, effects, 0
                );
            }

            //枪身 BeamGrad 鬼绿冥流光束 (突刺时增亮, 三段更宽)
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 tip = Projectile.Center + dir * 38f;
            Vector2 butt = Projectile.Center - dir * 22f;
            bool thrusting = CurrentStage == AttackStage.Thrust;
            ACMShaders.DrawBeam(butt, tip, halfWidth: thrusting ? (Combo == 2 ? 11f : 9f) : 6f,
                core: new Color(150, 255, 170), edge: new Color(30, 110, 70),
                intensity: thrusting ? 0.85f : 0.4f, flowSpeed: 2.4f, flowScale: 2.2f, coreSharp: 2.4f);

            //突刺时绘制勾魂光效
            if (thrusting) {
                //枪身光晕
                Color glowColor = new Color(80, 200, 120) * 0.4f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(
                    texture, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.1f, effects, 0
                );

                //枪尖处使用LightShot叠加勾魂光弹
                Texture2D lightShot = ACMAsset.LightShot;
                if (lightShot != null) {
                    Vector2 tipPos = Projectile.Center + dir * 35f - Main.screenPosition;
                    Vector2 lsOrigin = lightShot.Size() / 2f;
                    Color tipGlow = new Color(100, 255, 150) * 0.5f;
                    tipGlow.A = 0;
                    Main.EntitySpriteDraw(lightShot, tipPos, null, tipGlow, Projectile.rotation, lsOrigin, 0.5f, SpriteEffects.None, 0);
                }

                //枪尖处使用SoftGlow叠加勾魂光圈
                Texture2D softGlow = ACMAsset.SoftGlow;
                if (softGlow != null) {
                    Vector2 tipPos = Projectile.Center + dir * 35f - Main.screenPosition;
                    Vector2 sgOrigin = softGlow.Size() / 2f;
                    Color circleGlow = new Color(60, 220, 100) * 0.35f;
                    circleGlow.A = 0;
                    float pulse = 0.6f + MathF.Sin(Timer * 0.3f) * 0.1f;
                    Main.EntitySpriteDraw(softGlow, tipPos, null, circleGlow, 0f, sgOrigin, pulse, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }
}
