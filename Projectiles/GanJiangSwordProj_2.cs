using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static AncientChineseMythology.AncientChineseMythology;

namespace AncientChineseMythology.Projectiles
{
    public class GanJiangSwordProj_2 : ModProjectile
    {
        private const float SWINGRANGE = 1.67f * (float)Math.PI;
        private const float SPINRANGE = 1.67f * (float)Math.PI;
        private const float UNWIND = 0.4f;
        private const float SPINTIME = 1f;
        private int Swtimer = 0;
        private Vector2 spawnPoint;

        private enum AttackType
        {
            Swing,
            Spin,
        }

        private enum AttackStage
        {
            Prepare,
            Execute,
            Unwind
        }

        private AttackType CurrentAttack {
            get => (AttackType)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.localAI[0];
            set {
                Projectile.localAI[0] = (float)value;
                Timer = 0;
            }
        }

        private ref float InitialAngle => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];
        private ref float Progress => ref Projectile.localAI[1];
        private ref float Size => ref Projectile.localAI[2];

        private float prepTime => 4f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float execTime => 8f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float hideTime => 4f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/GanJiangSword";
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            base.SetStaticDefaults();
        }

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 68;
            Projectile.friendly = true;
            Projectile.timeLeft = 60;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            NPC target = FindClosestNPC(600f);
            if (target != null) {
                Vector2 directionToTarget = target.DirectionTo(Owner.Center);
                Vector2 offset = directionToTarget * 80f;
                Projectile.Center = target.Center + offset;
                InitialAngle = directionToTarget.ToRotation();
            }
            else {
                Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
                float targetAngle = (Owner.MountedCenter - Main.MouseWorld).ToRotation();
                InitialAngle = targetAngle;
            }
            spawnPoint = Projectile.Center;
        }

        private NPC FindClosestNPC(float maxDetectDistance) {
            NPC closestNPC = null;
            float closestDistance = maxDetectDistance;

            Vector2 mousePosition = Main.MouseWorld;

            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.friendly && (npc.lifeMax > 5 || npc.lifeMax == 1) && npc.damage > 0) {
                    if (Collision.CanHitLine(Projectile.Center, 1, 1, npc.Center, 1, 1)) {
                        float distanceToMouse = Vector2.Distance(mousePosition, npc.Center);

                        if (distanceToMouse < closestDistance) {
                            closestDistance = distanceToMouse;
                            closestNPC = npc;
                        }
                    }
                }
            }

            return closestNPC;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((sbyte)Projectile.spriteDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.spriteDirection = reader.ReadSByte();
        }

        public override void AI() {
            Projectile.oldPos[0] = Projectile.position;
            Projectile.oldRot[0] = Projectile.rotation;

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            switch (CurrentStage) {
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

            SetSwordPosition();
            Timer++;
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int particleCount = 10;
            for (int i = 0; i < particleCount; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 6f);

                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Clentaminator_Green, velocity, 100, Color.Blue, 1f);
                dust.noGravity = true;
                dust.fadeIn = 0.8f;
                dust.scale = 1f;
            }
        }
        public struct CustomVertex : IVertexType
        {
            public Vector3 Position;
            public Color Color;

            public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0)
            );

            VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

            public CustomVertex(Vector3 position, Color color) {
                Position = position;
                Color = color;
            }
        }
        public override bool PreDraw(ref Microsoft.Xna.Framework.Color lightColor) {
            Microsoft.Xna.Framework.Vector2 origin;
            float rotationOffset;
            SpriteEffects effects;

            if (Projectile.spriteDirection > 0) {
                origin = new Microsoft.Xna.Framework.Vector2(0, Projectile.height);
                rotationOffset = MathHelper.ToRadians(45f);
                effects = SpriteEffects.None;
            }
            else {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width, Projectile.height);
                rotationOffset = MathHelper.ToRadians(135f);
                effects = SpriteEffects.FlipHorizontally;
            }

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            List<Vertex> ve = new List<Vertex>();

            Color color = Color.LightGreen * 1f;
            if (CurrentAttack == AttackType.Swing && CurrentStage != AttackStage.Prepare) {
                if (Projectile.spriteDirection > 0) {
                    for (int i = 0; i < 12; i++) {

                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition + new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition + new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));

                    }
                }
                else {
                    for (int i = 0; i < 12; i++) {

                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition + new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition + new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                    }
                }
            }
            if (CurrentAttack == AttackType.Spin && CurrentStage != AttackStage.Prepare) {
                if (Projectile.spriteDirection > 0) {
                    for (int i = 0; i < 12; i++) {
                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition - new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition - new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                    }
                }
                else {
                    for (int i = 0; i < 12; i++) {
                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition - new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new Vertex(Projectile.Center - Main.screenPosition - new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                    }
                }

            }

            if (ve.Count >= 3) {
                gd.Textures[0] = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/SwordTrail551").Value;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, ve.ToArray(), 0, ve.Count - 2);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);



            Main.spriteBatch.Draw(TextureAssets.Projectile[Type].Value,
                Projectile.Center - Main.screenPosition,
                default,
                lightColor * Projectile.Opacity * lightColor.A,
                Projectile.rotation + rotationOffset,
                origin,
                Projectile.scale,
                effects,
                0);

            return false;
        }

        public override bool? Colliding(Microsoft.Xna.Framework.Rectangle projHitbox, Microsoft.Xna.Framework.Rectangle targetHitbox) {
            Microsoft.Xna.Framework.Vector2 start = Projectile.Center;
            Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * ((Projectile.Size.Length()) * Projectile.scale * 1.06f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);
        }

        public override void CutTiles() {
            Microsoft.Xna.Framework.Vector2 start = Projectile.Center;
            Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.06f);
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() {
            if (CurrentStage == AttackStage.Prepare)
                return false;
            return base.CanDamage();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;

        }

        public void SetSwordPosition() {
            Swtimer++;

            float radius = 20f;
            float speed = 1.2f;

            float angleOffset = MathHelper.ToRadians((Swtimer * speed) % 360);

            float direction = (CurrentAttack == AttackType.Swing) ? 1 : -1;

            Vector2 offset = new Vector2(
                (float)Math.Cos(angleOffset) * radius,
                (float)Math.Sin(angleOffset) * radius * direction
            );
            Projectile.Center = spawnPoint + offset;
            Projectile.rotation = InitialAngle + Projectile.spriteDirection * Progress;
            Projectile.scale = Size * 1.2f;
        }

        private void PrepareStrike() {
            Size = 1f;
            if (Timer >= prepTime) {
                SoundEngine.PlaySound(SoundID.Item1);
                CurrentStage = AttackStage.Execute;
            }
        }

        private void ExecuteStrike() {
            if (CurrentAttack == AttackType.Swing) {
                Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 2) * Timer / (execTime * 2));

                if (Timer >= execTime * 3) {
                    CurrentStage = AttackStage.Unwind;
                }
            }
            else {
                Progress = MathHelper.SmoothStep(0, -SPINRANGE, (1f - UNWIND / 2) * Timer / (execTime * SPINTIME * 2));

                if (Timer >= execTime * SPINTIME * 3) {
                    CurrentStage = AttackStage.Unwind;
                }
            }
        }

        private void UnwindStrike() {
            if (CurrentAttack == AttackType.Swing) {
                Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime));
                if (Timer >= hideTime) {
                    Projectile.Kill();
                }
            }
            else {
                Progress = MathHelper.SmoothStep(0, -SPINRANGE, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime * SPINTIME));
                if (Timer >= hideTime * SPINTIME) {
                    Projectile.Kill();
                }
            }
        }
    }
}