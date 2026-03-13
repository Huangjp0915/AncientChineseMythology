using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡左键手持弹幕 —— 三段式仪式动作：
    /// 1. 竖幡：玩家将幡旗高高竖起（蓄力感）
    /// 2. 展幡：幡旗向前挥出，大弧度展开吸收灵魂
    /// 3. 收幡：幡旗收回，吸入的灵魂凝聚爆发
    /// 纹理：竖直旗帜，顶端为握持端，底端自然飘垂
    /// </summary>
    public class SoulBannerHeldProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Items/Weapons/SoulBanners/SoulBanner";

        // ── 动作参数 ──
        private const float SwingRange = 1.67f * MathF.PI; // 挥舞角度（300度大弧）
        private const float FirstHalfSwing = 0.45f;         // 到达目标角度前的挥舞比例
        private const float Unwind = 0.4f;                   // 收回阶段比例
        private const float MaxReach = 60f;                   // 幡旗延伸距离
        private const float AbsorbRadius = 250f;              // 吸魂范围

        private Player Owner => Main.player[Projectile.owner];

        private enum AttackStage { Prepare, Execute, Unwind }

        private AttackStage CurrentStage
        {
            get => (AttackStage)Projectile.localAI[0];
            set { Projectile.localAI[0] = (float)value; Timer = 0; }
        }

        private ref float InitialAngle => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];
        private ref float Progress => ref Projectile.localAI[1];
        private ref float Size => ref Projectile.localAI[2];

        // 阶段时长（受攻速影响）
        private float PrepTime => 10f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ExecTime => 12f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float HideTime => 8f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        private bool hasPlayedSound;
        private bool hasAbsorbBurst;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults()
        {
            Projectile.width = 84;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 10000;
        }

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();

            // 限制瞄准方向不要过于极端
            if (Projectile.spriteDirection == 1)
            {
                targetAngle = MathHelper.Clamp(targetAngle, -MathF.PI / 3f, MathF.PI / 6f);
            }
            else
            {
                if (targetAngle < 0) targetAngle += MathF.PI * 2f;
                targetAngle = MathHelper.Clamp(targetAngle, MathF.PI * 5f / 6f, MathF.PI * 4f / 3f);
            }

            InitialAngle = targetAngle - FirstHalfSwing * SwingRange * Projectile.spriteDirection * 1.2f;
            Size = 0f;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((sbyte)Projectile.spriteDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.spriteDirection = reader.ReadSByte();
        }

        public override void AI()
        {
            // 手动更新拖尾位置
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            }
            Projectile.oldPos[0] = Projectile.position;
            Projectile.oldRot[0] = Projectile.rotation;

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed)
            {
                Projectile.Kill();
                return;
            }

            switch (CurrentStage)
            {
                case AttackStage.Prepare:
                    PrepareStrike();
                    break;
                case AttackStage.Execute:
                    ExecuteStrike();
                    break;
                default:
                    UnwindStrike();
                    break;
            }

            SetBannerPosition();
            Timer++;

            // ── 吸魂粒子效果 ──
            if (CurrentStage == AttackStage.Execute)
            {
                SpawnSoulAbsorbEffect();
            }

            // ── 幡旗飘动粒子 ──
            if (Main.rand.NextBool(3))
            {
                Vector2 tipPos = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.7f);
                Dust dust = Dust.NewDustDirect(tipPos, 1, 1, DustID.DungeonSpirit, 0f, 0f, 150, default, 0.7f);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
                dust.velocity.Y -= 0.5f;
            }
        }

        // ── 竖幡阶段：缓慢举起幡旗 ──
        private void PrepareStrike()
        {
            Size = MathHelper.SmoothStep(0f, 1f, Timer / PrepTime);

            if (Timer >= PrepTime)
            {
                SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);
                CurrentStage = AttackStage.Execute;
            }
        }

        // ── 展幡阶段：大弧度挥出吸魂 ──
        private void ExecuteStrike()
        {
            Progress = MathHelper.SmoothStep(0, SwingRange, (1f - Unwind / 2f) * Timer / (ExecTime * 2f));

            // 挥舞中段释放吸魂音效
            if (!hasPlayedSound && Timer > ExecTime * 0.3f)
            {
                hasPlayedSound = true;
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            }

            if (Timer >= ExecTime * 3f)
            {
                CurrentStage = AttackStage.Unwind;
            }
        }

        // ── 收幡阶段：收回并爆发灵魂 ──
        private void UnwindStrike()
        {
            Progress = MathHelper.SmoothStep(0, SwingRange, (1f - Unwind / 10f) + Unwind * Timer / HideTime);

            // 收回时灵魂爆发效果
            if (!hasAbsorbBurst && Timer > HideTime * 0.2f)
            {
                hasAbsorbBurst = true;
                SoulBurst();
            }

            if (Timer >= HideTime)
            {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 设置幡旗的位置和手臂姿态
        /// 万魂幡是竖直旗帜：顶端握持，旗面向外延伸
        /// </summary>
        private void SetBannerPosition()
        {
            Projectile.rotation = InitialAngle + Projectile.spriteDirection * Progress;

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.ToRadians(90f));

            Vector2 armPosition = Owner.GetFrontHandPosition(
                Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathF.PI / 2f);
            armPosition.Y += Owner.gfxOffY;

            Projectile.Center = armPosition + Projectile.rotation.ToRotationVector2() * MaxReach * Math.Max(Size, 0.3f);
            Projectile.scale = Size * 1.2f * Owner.GetAdjustedItemScale(Owner.HeldItem);

            Owner.heldProj = Projectile.whoAmI;
        }

        /// <summary>
        /// 展幡时的吸魂效果：范围内敌人灵魂被抽离飞向幡旗
        /// </summary>
        private void SpawnSoulAbsorbEffect()
        {
            Vector2 bannerTip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.6f);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this))
                    continue;

                float dist = Vector2.Distance(npc.Center, bannerTip);
                if (dist < AbsorbRadius && Main.rand.NextBool(4))
                {
                    Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                    Vector2 toSelf = (bannerTip - dustPos).SafeNormalize(Vector2.Zero);

                    // 灵魂粒子带有弧形轨迹感
                    Vector2 tangent = new(-toSelf.Y, toSelf.X);
                    Vector2 dustVel = toSelf * 7f + tangent * Main.rand.NextFloat(-2f, 2f);

                    Dust dust = Dust.NewDustDirect(dustPos, 1, 1, DustID.DungeonSpirit,
                        dustVel.X, dustVel.Y, 80, default, 1.2f);
                    dust.noGravity = true;
                    dust.fadeIn = 1.5f;
                }
            }
        }

        /// <summary>
        /// 收幡时灵魂凝聚爆发
        /// </summary>
        private void SoulBurst()
        {
            Vector2 center = Projectile.Center;
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = 0.4f }, center);

            for (int i = 0; i < 16; i++)
            {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                Dust dust = Dust.NewDustDirect(center, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 60, default, 1.4f);
                dust.noGravity = true;
                dust.fadeIn = 1.8f;
            }

            // 内圈紫色火焰
            for (int i = 0; i < 10; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                Dust dust = Dust.NewDustDirect(center, 1, 1, DustID.PurpleTorch,
                    vel.X, vel.Y, 100, default, 1.6f);
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 击中时产生灵魂被吸走的效果
            for (int i = 0; i < 10; i++)
            {
                Vector2 toOwner = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero);
                Vector2 vel = toOwner * Main.rand.NextFloat(3f, 8f) + Main.rand.NextVector2Circular(2f, 2f);
                Dust dust = Dust.NewDustDirect(target.Center, 1, 1, DustID.DungeonSpirit,
                    vel.X, vel.Y, 80, default, 1.3f);
                dust.noGravity = true;
                dust.fadeIn = 1.6f;
            }

            // 吸取生命：每次命中少量回血
            if (Main.rand.NextBool(3))
            {
                int healAmount = Math.Max(1, damageDone / 20);
                Main.player[Projectile.owner].Heal(healAmount);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool? CanDamage()
        {
            if (CurrentStage == AttackStage.Prepare)
                return false;
            return base.CanDamage();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.8f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 25f * Projectile.scale, ref collisionPoint);
        }

        public override void CutTiles()
        {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.9f);
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);
        }

        // ── 自定义绘制：拖尾 + 幡旗本体 ──
        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 origin;
            float rotationOffset;
            SpriteEffects effects;

            // 万魂幡是竖直旗帜：
            //   朝右（spriteDirection > 0）：原点在中心，旋转偏移45°
            //   朝左（spriteDirection < 0）：镜像翻转，旋转偏移135°
            if (Projectile.spriteDirection > 0)
            {
                origin = new Vector2(Projectile.width / 2f, Projectile.height / 2);
                rotationOffset = MathHelper.ToRadians(90);
                effects = SpriteEffects.None;
            }
            else
            {
                origin = new Vector2(Projectile.width / 2f, Projectile.height / 2);
                rotationOffset = MathHelper.ToRadians(90);
                effects = SpriteEffects.FlipHorizontally;
            }

            // ── 幡旗光晕层 ──
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float glowPulse = 0.25f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.15f);

            if (CurrentStage == AttackStage.Execute)
                glowPulse += 0.3f;

            Color glowColor = new Color(120, 40, 200) * glowPulse;
            Main.spriteBatch.Draw(texture,
                Projectile.Center - Main.screenPosition,
                default, glowColor * Projectile.Opacity,
                Projectile.rotation + rotationOffset,
                origin, Projectile.scale * 1.12f, effects, 0);

            // ── 幡旗本体 ──
            Main.spriteBatch.Draw(texture,
                Projectile.Center - Main.screenPosition,
                default, lightColor * Projectile.Opacity,
                Projectile.rotation + rotationOffset,
                origin, Projectile.scale, effects, 0);

            return false;
        }

        // 拖尾顶点结构（复用项目已有模式）
        public struct ColoredVertex : IVertexType
        {
            public Vector3 Position;
            public Vector3 TexCoord;
            public Color Color;

            public static readonly VertexDeclaration _VertexDeclaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0)
            );

            VertexDeclaration IVertexType.VertexDeclaration => _VertexDeclaration;

            public ColoredVertex(Vector2 position, Vector3 texCoord, Color color)
            {
                Position = new Vector3(position, 0f);
                TexCoord = texCoord;
                Color = color;
            }
        }
    }
}
