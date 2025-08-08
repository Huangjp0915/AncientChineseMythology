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
    class WoodenStickSpearProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/WoodenStickSpearProjectile";
        // 定义一些常量，决定剑的挥动范围
        // 注意，我们在这里使用乘数，因为这简化了这些交互的调整
        // 你可以更改这些值或完全替换它们，但这些值是根据外观调整的
        private const float SWINGRANGE = 1.67f * (float)Math.PI; // 挥动攻击覆盖的角度（300度）
        private const float FIRSTHALFSWING = 0.45f; // 达到目标角度之前的挥动比例（相对于 swingRange）
        private const float SPINRANGE = 1.67f * (float)Math.PI; // 旋转攻击覆盖的角度（630度）
        private const float WINDUP = 0.15f; // 玩家攻击前手臂向后摆动的程度（相对于 swingRange）
        private const float UNWIND = 0.4f; // 剑何时开始消失
        private const float SPINTIME = 1f; // 旋转攻击比挥动攻击多的时长
        //private bool isShoot = false; // 标记是否击中目标

        private enum AttackType // 当前进行的攻击类型
        {
            // 挥动是正常的剑挥动，可以稍微瞄准
            // 挥动会经历完整的动画周期
            Swing,
            // 旋转是全圆形的挥动
            // 它们较慢并造成更多击退
            Spin,
        }

        private enum AttackStage // 当前执行的攻击阶段，具体见 AI 中的函数描述
        {
            Prepare,
            Execute,
            Unwind
        }

        // 这些属性封装了常规的 ai 和 localAI 数组，以便更简洁易懂
        private AttackType CurrentAttack {
            get => (AttackType)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.localAI[0];
            set {
                Projectile.localAI[0] = (float)value;
                Timer = 0; // 切换状态时重置计时器
            }
        }

        // 运行时跟踪的变量
        private ref float InitialAngle => ref Projectile.ai[1]; // 瞄准的角度（带有限制）
        private ref float Timer => ref Projectile.ai[2]; // 计时器，用于跟踪每个阶段的进度
        private ref float Progress => ref Projectile.localAI[1]; // 剑相对于初始角度的位置
        private ref float Size => ref Projectile.localAI[2]; // 剑的大小

        // 定义每个阶段的时间函数，考虑到近战攻击速度
        // 注意，你可以根据投射物的需要更改这个
        private float prepTime => 12f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        //private float execTime => 6f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float execTime => 12f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float hideTime => 12f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        //public override string Texture => "MyMod2/Content/Items/YingYangSword"; // 使用物品的纹理作为投射物的纹理
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2; // 尾随模式为 2，表示尾随着玩家
            ProjectileID.Sets.TrailCacheLength[Type] = 12; // 尾迹缓存长度
            base.SetStaticDefaults();
        }

        public override void SetDefaults() {
            Projectile.width = 86; // 投射物的碰撞箱宽度
            Projectile.height = 86; // 投射物的碰撞箱高度
            Projectile.friendly = true; // 投射物可以击中敌人
            Projectile.timeLeft = 10000; // 投射物失效所需的时间
            Projectile.penetrate = -1; // 投射物无限穿透
            Projectile.tileCollide = false; // 投射物不与瓦片碰撞
            Projectile.usesLocalNPCImmunity = true; // 使用局部免疫帧
            Projectile.localNPCHitCooldown = -1; // 设置为 -1 以确保投射物不会命中两次
            Projectile.ownerHitCheck = true; // 确保投射物的拥有者有视线可以瞄准目标（即不能穿越瓦片击中目标）
            Projectile.DamageType = DamageClass.Melee; // 投射物为近战投射物
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();

            if (CurrentAttack == AttackType.Spin) {
                InitialAngle = targetAngle - FIRSTHALFSWING * SWINGRANGE * Projectile.spriteDirection * 1.6f; // 否则我们计算角度
            }
            else {
                if (Projectile.spriteDirection == 1) {
                    // 不过，我们限制可能方向的范围，以免看起来太过荒谬
                    targetAngle = MathHelper.Clamp(targetAngle, (float)-Math.PI * 1 / 3, (float)Math.PI * 1 / 6);
                }
                else {
                    if (targetAngle < 0) {
                        targetAngle += 2 * (float)Math.PI; // 使角度范围连续，以便于操作
                    }

                    targetAngle = MathHelper.Clamp(targetAngle, (float)Math.PI * 5 / 6, (float)Math.PI * 4 / 3);
                }

                InitialAngle = targetAngle - FIRSTHALFSWING * SWINGRANGE * Projectile.spriteDirection * 1.2f; // 否则我们计算角度
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            // 这个投射物的 Projectile.spriteDirection 在 OnSpawn 中根据拥有者的鼠标位置得出，因此需要同步。spriteDirection 不是自动同步的字段. 由于所有 Projectile.ai 插槽都已使用，因此我们将其手动同步。
            writer.Write((sbyte)Projectile.spriteDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.spriteDirection = reader.ReadSByte();
        }

        public override void AI() {
            // 更新投射物的位置和旋转
            Projectile.oldPos[0] = Projectile.position;
            Projectile.oldRot[0] = Projectile.rotation;


            // 更新历史位置和旋转
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            }

            // 在投射物被杀死之前延长使用动画
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            // 如果玩家死去或被控制，杀死投射物
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                //drawTrail = 0;
                Projectile.Kill();
                return;
            }

            // AI 取决于阶段和攻击
            // 注意，这些阶段是为了在开始和结束时促使缩放效果
            // 如果这不是你想要的，可以简化
            switch (CurrentStage) {
                case AttackStage.Prepare:
                    PrepareStrike();
                    break;
                case AttackStage.Execute:
                    ExecuteStrike();
                    //Attack_1();
                    break;
                default:
                    UnwindStrike();
                    break;
            }

            SetSwordPosition();
            Timer++;

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
            SpriteEffects effects; // 贴图效果

            if (Projectile.spriteDirection > 0) {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width / 2, Projectile.height / 2); // 原点在中心
                rotationOffset = MathHelper.ToRadians(45f); // 旋转偏移45度
                effects = SpriteEffects.None; // 贴图不翻转
            }
            else {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width / 2, Projectile.height / 2); // 原点在中心
                rotationOffset = MathHelper.ToRadians(135f); // 旋转偏移135度
                effects = SpriteEffects.FlipHorizontally; // 翻转贴图
            }

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //开始顶点绘制

            List<ColoredVertex> ve = new List<ColoredVertex>();

            Color color = Color.DarkGreen * 0.12f;
            Color color1 = Color.Goldenrod * 0.12f;
            if (Main.dayTime) {
                color = Color.DarkGreen * 0.24f;
                color1 = Color.Goldenrod * 0.24f;
            }
            Player player = Main.player[Projectile.owner];
            if (CurrentAttack == AttackType.Swing
                && CurrentStage != AttackStage.Prepare
                ) {
                if (Projectile.spriteDirection > 0) {
                    for (int i = 0; i < 12; i++) {
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color1));

                    }
                }
                else {
                    for (int i = 0; i < 12; i++) {

                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color1));
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                    }
                }
            }
            if (CurrentAttack == AttackType.Spin
                //&& Projectile.spriteDirection > 0 
                && CurrentStage != AttackStage.Prepare) {
                if (Projectile.spriteDirection > 0) {
                    for (int i = 0; i < 12; i++) {
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition - new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition - new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color1));
                    }
                }
                else {
                    for (int i = 0; i < 12; i++) {
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition - new Vector2(0, -115).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color1));
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition - new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                    }
                }

            }

            if (ve.Count >= 3) {
                gd.Textures[0] = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/SwordTrail55").Value;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, ve.ToArray(), 0, ve.Count - 2);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.spriteBatch.Draw(TextureAssets.Projectile[Type].Value,
                Projectile.Center - Main.screenPosition,
                default,
                lightColor * Projectile.Opacity,
                Projectile.rotation + rotationOffset,
                origin,
                Projectile.scale,
                effects,
                0);

            // 由于我们在进行自定义绘制，因此不进行正常绘制
            return false;
        }

        // 找到剑的起始和结束位置，并使用线段碰撞检测与敌人检查碰撞
        public override bool? Colliding(Microsoft.Xna.Framework.Rectangle projHitbox, Microsoft.Xna.Framework.Rectangle targetHitbox) {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;// 计算投射物的起点
            Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * ((Projectile.Size.Length()) * Projectile.scale * 0.8f);// 计算投射物的终点
            float collisionPoint = 0f;// 碰撞点
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);// 调用线段碰撞检测
        }

        // 对瓦片进行类似的碰撞检测
        public override void CutTiles() {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;
            Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.9f);// 计算投射物的终点
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);// 绘制线段，并对其上的瓦片进行碰撞检测
        }

        // 确保投射物仅在释放阶段和放松阶段造成伤害
        public override bool? CanDamage() {
            if (CurrentStage == AttackStage.Prepare)
                return false;
            return base.CanDamage();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 确保击退方向远离玩家
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;

        }

        // 方便设置投射物和手臂位置的函数
        public void SetSwordPosition() {
            Projectile.rotation = InitialAngle + Projectile.spriteDirection * Progress; // 设置投射物的旋转

            // 设置复合手臂，允许你独立设置手臂的旋转和前后手臂的伸展
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.ToRadians(90f)); // 设置手臂位置（由于手臂起始时低下，所以有 90 度偏移）
            Microsoft.Xna.Framework.Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - (float)Math.PI / 2); // 获取手的位置

            armPosition.Y += Owner.gfxOffY; // 添加偏移
            Projectile.Center = armPosition + Projectile.rotation.ToRotationVector2() * 30f; // 设置投射物到手的位置，并向外偏移20像素
            Projectile.scale = Size * 1.2f * Owner.GetAdjustedItemScale(Owner.HeldItem); // 稍微放大投射物，也考虑到近战尺寸的修正

            Owner.heldProj = Projectile.whoAmI; // 设置持有的投射物为这个投射物
        }

        // 准备攻击的函数
        private void PrepareStrike() {
            Size = 1f; // 使剑在准备攻击时缓慢增加大小，直到达到最大值
            if (Timer >= prepTime) {
                SoundEngine.PlaySound(SoundID.Item1); // 播放剑的声音，因为在生成时播放太早
                CurrentStage = AttackStage.Execute; // 如果攻击超过准备时间，进入下一个阶段
            }
        }

        // 实现挥动的首半部分
        private void ExecuteStrike() {
            if (CurrentAttack == AttackType.Swing) {
                Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 2) * Timer / (execTime * 2));

                if (Timer >= execTime * 3) {
                    CurrentStage = AttackStage.Unwind; // 完成攻击，进入放松阶段
                }
            }
            else {
                Progress = MathHelper.SmoothStep(0, -SPINRANGE, (1f - UNWIND / 2) * Timer / (execTime * SPINTIME * 2));

                if (Timer >= execTime * SPINTIME * 3) {
                    CurrentStage = AttackStage.Unwind; // 完成攻击，进入放松阶段
                }
            }
        }

        // 实现挥动后半部分，剑消失
        private void UnwindStrike() {
            if (CurrentAttack == AttackType.Swing) {
                Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime));
                //Size = 1f - MathHelper.SmoothStep(0, 1, Timer / hideTime); // 在挥动结束时，使剑在大小上逐渐减小，形成平滑的隐藏动画

                if (Timer >= hideTime) {
                    Projectile.Kill(); // 完成隐藏阶段，杀死投射物
                }
            }
            else {
                Progress = MathHelper.SmoothStep(0, -SPINRANGE, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime * SPINTIME));
                //Size = 1f - MathHelper.SmoothStep(0, 1, Timer / (hideTime * SPINTIME));

                if (Timer >= hideTime * SPINTIME) {
                    Projectile.Kill(); // 完成隐藏阶段，杀死投射物
                }
            }
        }
    }
}
