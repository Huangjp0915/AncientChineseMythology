using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.Swords;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 干将剑刃 (左键三连段, 手持弹幕) — 干将莫邪之"赤"雄剑。
    /// ai[0]=段序 (0 正斩 / 1 逆斩(反向, 快 12%) / 2 十字旋斩(大弧, ×1.3)); ai[1]=瞄准初始角; ai[2]=段内计时。
    /// 波形: 前摇 quadratic 后摆聚光 → 爆发 poly(9) ease-out → 收招 quintic;
    /// 每段爆发 6f 后放出莫邪影随斩 (GanJiangSwordProj_2 echo, ×0.35; 段 3 反向十字 ×0.65)。
    /// 挥砍弧光走系列专属 GanJiangTwinArc.fx (扫描头白热 + 噪声撕裂缘)。命中积攒剑鸣。
    /// 修复旧版: 每帧 new List + 每帧 Request 纹理; 发射逻辑收敛回物品端。
    /// </summary>
    public class GanJiangSwordProj : ModProjectile
    {
        private const float Backswing = 0.3f;
        private const int ArcSegments = 24;

        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/GanJiangSword";

        private Player Owner => Main.player[Projectile.owner];
        private int Step => (int)Projectile.ai[0];
        private ref float InitialAngle => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        private enum Stage { Prepare, Execute, Recover }
        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[0];
            set { Projectile.localAI[0] = (float)value; Timer = 0; }
        }
        private ref float Progress => ref Projectile.localAI[1];

        private bool echoSpawned;
        private readonly ColoredVertex[] _arcVerts = new ColoredVertex[(ArcSegments + 1) * 2]; // 复用, 避免每帧分配

        private float AtkSpeed => Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float PrepTime => (Step == 2 ? 9f : Step == 1 ? 6f : 7f) / AtkSpeed;
        private float ExecTime => (Step == 2 ? 7f : Step == 1 ? 5.3f : 6f) / AtkSpeed;
        private float RecoverTime => (Step == 2 ? 12f : 9f) / AtkSpeed;
        private float Range => Step == 2 ? 3.6f : 2.4f;
        /// <summary>逆斩反向扫。</summary>
        private float SwingSign => Step == 1 ? -1f : 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 68;
            Projectile.friendly = true;
            Projectile.timeLeft = 600;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float dir = Projectile.spriteDirection;
            float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();

            if (Step != 2) {
                // 目标角限制在面向侧 (沿用旧版夹角保护)
                if (dir > 0)
                    targetAngle = MathHelper.Clamp(targetAngle, -MathHelper.Pi / 3f, MathHelper.Pi / 6f);
                else {
                    if (targetAngle < 0)
                        targetAngle += MathHelper.TwoPi;
                    targetAngle = MathHelper.Clamp(targetAngle, MathHelper.Pi * 5f / 6f, MathHelper.Pi * 4f / 3f);
                }
            }
            // 正/旋斩自后方起手; 逆斩自前方反抡
            InitialAngle = targetAngle - 0.5f * Range * dir * SwingSign;
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write((sbyte)Projectile.spriteDirection);
        public override void ReceiveExtraAI(BinaryReader reader) => Projectile.spriteDirection = reader.ReadSByte();

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.ChangeDir(Projectile.spriteDirection);

            switch (CurrentStage) {
                case Stage.Prepare: DoPrepare(); break;
                case Stage.Execute: DoExecute(); break;
                default: DoRecover(); break;
            }

            SetSwordPosition();
            Timer++;
        }

        private void DoPrepare() {
            float t = MathHelper.Clamp(Timer / PrepTime, 0f, 1f);
            float ease = t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
            Progress = -Backswing * ease;

            // 剑尖聚赤金流光 (前摇广播)
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 tip = Owner.MountedCenter + Projectile.rotation.ToRotationVector2() * 92f * Projectile.scale;
                Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(24f, 24f), DustID.GoldFlame);
                d.noGravity = true;
                d.velocity = (tip - d.position) * 0.15f;
                d.scale = Main.rand.NextFloat(0.8f, 1.2f);
            }

            if (Timer >= PrepTime) {
                CurrentStage = Stage.Execute;
                // 音高随段递升的挥砍音 + 爆发帧微震
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.1f + Step * 0.14f, Volume = 1.05f }, Owner.Center);
                WeaponVFX.AddScreenShake(Owner.Center, Step == 2 ? 1.8f : 1f);
            }
        }

        private void DoExecute() {
            float t = MathHelper.Clamp(Timer / ExecTime, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - t, 9f); // poly(9): 一瞬到位
            Progress = MathHelper.Lerp(-Backswing, Range, ease);

            // 影随斩: 爆发起 2f 放出莫邪虚剑 (段 3 为反向十字半边)
            if (!echoSpawned && Timer >= 2f) {
                echoSpawned = true;
                if (Projectile.owner == Main.myPlayer) {
                    float echoMult = Step == 2 ? 0.65f : 0.35f;
                    float echoSign = Step == 2 ? -SwingSign : SwingSign;
                    // velocity 承载镜像参数 (X=主剑朝向, Y=扫向), 弹幕不位移
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter,
                        new Vector2(Projectile.spriteDirection, echoSign),
                        ModContent.ProjectileType<GanJiangSwordProj_2>(),
                        (int)(Projectile.damage * echoMult), Projectile.knockBack * 0.5f, Projectile.owner,
                        0f, InitialAngle);
                }
            }

            if (Timer >= ExecTime)
                CurrentStage = Stage.Recover;
        }

        private void DoRecover() {
            float t = MathHelper.Clamp(Timer / RecoverTime, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - t, 5f);
            Progress = MathHelper.Lerp(Range, Range * 0.95f, ease);
            Projectile.Opacity = 1f - t * t;

            if (Timer >= RecoverTime)
                Projectile.Kill();
        }

        public void SetSwordPosition() {
            Projectile.rotation = InitialAngle + Projectile.spriteDirection * SwingSign * Progress;

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            armPosition.Y += Owner.gfxOffY;
            Projectile.Center = armPosition;
            Projectile.scale = 1.2f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.06f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.06f);
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.Torch,
                        Main.rand.NextVector2Circular(4f, 6f), 100, default, 1f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.8f;
                }
            }
            float burstScale = Step == 2 ? 1.5f : 1f;
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Crimson, scale: burstScale, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, Step == 2 ? 2f : 1.2f);

            // 剑鸣共鸣 +1 (owner 端权威)
            if (Projectile.owner == Main.myPlayer)
                Owner.GetModPlayer<GanJiangResonancePlayer>().AddResonance(1);
        }

        public override bool PreDraw(ref Color lightColor) {
            // ── 专属弧光 (GanJiangTwinArc.fx): 扫描头跟随挥砍进度 ──
            if (CurrentStage != Stage.Prepare)
                DrawSwingArc();

            Vector2 origin;
            float rotationOffset;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) {
                origin = new Vector2(0, Projectile.height);
                rotationOffset = MathHelper.ToRadians(45f);
                effects = SpriteEffects.None;
            }
            else {
                origin = new Vector2(Projectile.width, Projectile.height);
                rotationOffset = MathHelper.ToRadians(135f);
                effects = SpriteEffects.FlipHorizontally;
            }

            Main.spriteBatch.Draw(TextureAssets.Projectile[Type].Value,
                Projectile.Center - Main.screenPosition, null,
                lightColor * Projectile.Opacity * (lightColor.A / 255f),
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);
            return false;
        }

        /// <summary>扇环弧光 mesh + GanJiangTwinArc 着色器 (顶点数组复用)。</summary>
        private void DrawSwingArc() {
            Effect fx = WeaponVFX.GetEffect("GanJiangTwinArc");
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null || Main.dedServ)
                return;

            float dirSign = Projectile.spriteDirection * SwingSign;
            float startAngle = InitialAngle - dirSign * Backswing;
            float fullSpan = dirSign * (Range + Backswing);
            float progress = MathHelper.Clamp((Progress + Backswing) / (Range + Backswing), 0f, 1f);
            float intensity = CurrentStage == Stage.Execute ? 1f : Projectile.Opacity;
            if (intensity <= 0.03f)
                return;

            Vector2 center = Owner.MountedCenter - Main.screenPosition;
            float innerR = 34f * Projectile.scale;
            float outerR = 112f * Projectile.scale;
            for (int i = 0; i <= ArcSegments; i++) {
                float frac = i / (float)ArcSegments;
                Vector2 rot = (startAngle + fullSpan * frac).ToRotationVector2();
                _arcVerts[i * 2] = new ColoredVertex(center + rot * innerR, new Vector3(frac, 0f, 1f), Color.White);
                _arcVerts[i * 2 + 1] = new ColoredVertex(center + rot * outerR, new Vector3(frac, 1f, 1f), Color.White);
            }

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uProgress"]?.SetValue(progress);
            fx.Parameters["uColorCore"]?.SetValue(new Color(255, 225, 170).ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(new Color(215, 60, 25).ToVector4());
            fx.Parameters["uNoiseScale"]?.SetValue(2.6f);
            fx.Parameters["uTailLen"]?.SetValue(0.45f);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = noise;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, _arcVerts, 0, _arcVerts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }
}
