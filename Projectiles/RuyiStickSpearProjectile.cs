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
using static AncientChineseMythology.AncientChineseMythology;

namespace AncientChineseMythology.Projectiles
{
    class RuyiStickSpearProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";
        // 定义一些常量，决定剑的挥动范围
        // 注意，我们在这里使用乘数，因为这简化了这些交互的调整
        // 你可以更改这些值或完全替换它们，但这些值是根据外观调整的
        private const float SWINGRANGE = 1.67f * (float)Math.PI; // 挥动攻击覆盖的角度（300度）
        private const float FIRSTHALFSWING = 0.45f; // 达到目标角度之前的挥动比例（相对于 swingRange）
        private const float SPINRANGE = 1.67f * (float)Math.PI; // 旋转攻击覆盖的角度（630度）
        private const float UNWIND = 0.4f; // 剑何时开始消失
        private const float SPINTIME = 1f; // 旋转攻击比挥动攻击多的时长

        private const float WIDE_SWING_RANGE = 2.5f * (float)Math.PI; // 更大范围的挥舞（450度）
        private const float WIDE_SWING_RANGE1 = 2f * (float)Math.PI; // 更大范围的挥舞
        //private const float WIDE_SWING_RANGE2 = 1.8f * (float)Math.PI; // 更大范围的挥舞
        private const float WIDE_SWING_TIME = 1.5f; // 更大范围挥舞的时间倍率


        private enum AttackType // 当前进行的攻击类型
        {
            Swing, // 挥动,上到下
            Spin,  // 挥动,下到上
            WideSwing // 更大范围的挥舞
        }


        private enum AttackStage // 当前执行的攻击阶段，具体见 AI 中的函数描述
        {
            Prepare,
            Execute,
            Unwind
        }

        // 这些属性封装了常规的 ai 和 localAI 数组，以便更简洁易懂
        private AttackType CurrentAttack
        {
            get => (AttackType)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private AttackStage CurrentStage
        {
            get => (AttackStage)Projectile.localAI[0];
            set
            {
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
        private float prepTime => 4f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float execTime => 6f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float hideTime => 4f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        //public override string Texture => "MyMod2/Content/Items/YingYangSword"; // 使用物品的纹理作为投射物的纹理
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2; // 尾随模式为 2，表示尾随着玩家
            ProjectileID.Sets.TrailCacheLength[Type] = 12; // 尾迹缓存长度
            base.SetStaticDefaults();
        }

        public override void SetDefaults()
        {
            Projectile.width = 108; // 投射物的碰撞箱宽度
            Projectile.height = 108; // 投射物的碰撞箱高度
            Projectile.friendly = true; // 投射物可以击中敌人
            Projectile.timeLeft = 10000; // 投射物失效所需的时间
            Projectile.penetrate = -1; // 投射物无限穿透
            Projectile.tileCollide = false; // 投射物不与瓦片碰撞
            Projectile.usesLocalNPCImmunity = true; // 使用局部免疫帧
            Projectile.localNPCHitCooldown = 10; // 设置局部NPC命中冷却时间
            Projectile.ownerHitCheck = true; // 确保投射物的拥有者有视线可以瞄准目标（即不能穿越瓦片击中目标）
            Projectile.DamageType = DamageClass.Melee; // 投射物为近战投射物
        }

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();

            if (CurrentAttack == AttackType.Spin)
            {
                InitialAngle = targetAngle - FIRSTHALFSWING * SWINGRANGE * Projectile.spriteDirection * 1.6f; // 否则我们计算角度
            }
            else if (CurrentAttack == AttackType.Swing)
            {
                if (Projectile.spriteDirection == 1)
                {
                    // 不过，我们限制可能方向的范围，以免看起来太过荒谬
                    targetAngle = MathHelper.Clamp(targetAngle, (float)-Math.PI * 1 / 3, (float)Math.PI * 1 / 6);
                }
                else
                {
                    if (targetAngle < 0)
                    {
                        targetAngle += 2 * (float)Math.PI; // 使角度范围连续，以便于操作
                    }

                    targetAngle = MathHelper.Clamp(targetAngle, (float)Math.PI * 5 / 6, (float)Math.PI * 4 / 3);
                }

                InitialAngle = targetAngle - FIRSTHALFSWING * SWINGRANGE * Projectile.spriteDirection * 1.2f; // 否则我们计算角度
            }
            else if (CurrentAttack == AttackType.WideSwing)
            {
                if (Projectile.spriteDirection == 1)
                {
                    targetAngle = MathHelper.Clamp(targetAngle, (float)-Math.PI * 1 / 2, (float)Math.PI * 1 / 3);
                }
                else
                {
                    if (targetAngle < 0)
                    {
                        targetAngle += 2 * (float)Math.PI;
                    }
                    targetAngle = MathHelper.Clamp(targetAngle, (float)Math.PI * 2 / 3, (float)Math.PI * 3 / 2);
                }

                InitialAngle = targetAngle - FIRSTHALFSWING * WIDE_SWING_RANGE1 * Projectile.spriteDirection * 1.2f;
            }

        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            // 这个投射物的 Projectile.spriteDirection 在 OnSpawn 中根据拥有者的鼠标位置得出，因此需要同步。spriteDirection 不是自动同步的字段. 由于所有 Projectile.ai 插槽都已使用，因此我们将其手动同步。
            writer.Write((sbyte)Projectile.spriteDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.spriteDirection = reader.ReadSByte();
        }

        public override void AI()
        {
            // 更新投射物的位置和旋转
            Projectile.oldPos[0] = Projectile.position;
            Projectile.oldRot[0] = Projectile.rotation;

            // 添加光效
            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3() * 0.5f);

            // 更新历史位置和旋转
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            }

            // 在投射物被杀死之前延长使用动画
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            // 如果玩家死去或被控制，杀死投射物
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed)
            {
                Projectile.Kill();
                return;
            }

            // 检测 WideSwing 是否反弹敌人弹幕
            if (CurrentAttack == AttackType.WideSwing && CurrentStage == AttackStage.Execute)
            {
                ReflectHostileProjectiles();
            }

            // 根据阶段执行逻辑
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

            SetSwordPosition();
            Timer++;
        }
        private void ReflectHostileProjectiles()
        {
            foreach (Projectile proj in Main.projectile)
            {
                // 检查投射物是否是敌人弹幕且未被友方使用
                if (proj.active && proj.hostile && !proj.friendly)
                {
                    // 检查投射物是否与 WideSwing 的范围发生碰撞
                    if (Projectile.Hitbox.Intersects(proj.Hitbox))
                    {
                        // 反弹逻辑：将投射物的速度反转并设置为友方
                        proj.velocity = -proj.velocity;
                        proj.hostile = false;
                        proj.friendly = true;

                        // 添加视觉效果
                        for(int j = 0; j < 5; j++)
                        Dust.NewDust(proj.Center, proj.width, proj.height, DustID.Firework_Red, proj.velocity.X * 0.5f, proj.velocity.Y * 0.5f, 1, Color.Red, 1f);

                        // 播放声音效果
                        SoundEngine.PlaySound(SoundID.Item10, proj.position);
                    }
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 重置投射物的碰撞检测，以便可以多次击中敌人
            Projectile.localNPCImmunity[target.whoAmI] = 10; // 设置局部NPC命中冷却时间
            target.immune[Projectile.owner] = 0; // 确保敌人不会对投射物的拥有者免疫
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

            public CustomVertex(Vector3 position, Color color)
            {
                Position = position;
                Color = color;
            }
        }
        public override bool PreDraw(ref Microsoft.Xna.Framework.Color lightColor)
        {
            Microsoft.Xna.Framework.Vector2 origin;
            float rotationOffset;
            SpriteEffects effects; // 贴图效果

            if (Projectile.spriteDirection > 0)
            {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width / 2, Projectile.height / 2); // 原点在中心
                rotationOffset = MathHelper.ToRadians(45f); // 旋转偏移45度
                effects = SpriteEffects.None; // 贴图不翻转
            }
            else
            {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width / 2, Projectile.height / 2); // 原点在中心
                rotationOffset = MathHelper.ToRadians(135f); // 旋转偏移135度
                effects = SpriteEffects.FlipHorizontally; // 翻转贴图
            }

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //开始顶点绘制

            List<Vertex> ve = new List<Vertex>();

            Color color = new(250, 20, 60);
            
            Player player = Main.player[Projectile.owner];
            if (CurrentAttack == AttackType.Swing
                && CurrentStage != AttackStage.Prepare && Timer >= execTime / 2
                )
            {
                if (Projectile.spriteDirection > 0)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        ve.Add(new Vertex(player.Center - Main.screenPosition + new Vector2(0, -120).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new Vertex(player.Center - Main.screenPosition + new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));

                    }
                }
                else
                {
                    for (int i = 0; i < 12; i++)
                    {

                        ve.Add(new Vertex(player.Center - Main.screenPosition + new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                        ve.Add(new Vertex(player.Center - Main.screenPosition + new Vector2(0, -120).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                    }
                }
            }
            if (CurrentAttack == AttackType.Spin
                && CurrentStage != AttackStage.Prepare && Timer >= execTime / 2)
            {
                if (Projectile.spriteDirection > 0)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -120).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                    }
                }
                else
                {
                    for (int i = 0; i < 12; i++)
                    {
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -120).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -40).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                    }
                }
            }
            if (CurrentAttack == AttackType.WideSwing && CurrentStage == AttackStage.Execute && Timer >= execTime * WIDE_SWING_TIME/2)
            {
                if (Projectile.spriteDirection > 0)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -180).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -60).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                    }
                }
                else
                {
                    for (int i = 0; i < 12; i++)
                    {
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -60).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                        ve.Add(new Vertex(player.Center - Main.screenPosition - new Vector2(0, -180).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                    }
                }
            }

            if (ve.Count >= 3)
            {
                gd.Textures[0] = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/SwordTrail553").Value;
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

            // 由于我们在进行自定义绘制，因此不进行正常绘制
            return false;
        }

        // 找到剑的起始和结束位置，并使用线段碰撞检测与敌人检查碰撞
        public override bool? Colliding(Microsoft.Xna.Framework.Rectangle projHitbox, Microsoft.Xna.Framework.Rectangle targetHitbox)
        {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;// 计算投射物的起点
            float collisionPoint = 0f;// 碰撞点
            if (CurrentAttack != AttackType.WideSwing)
            {
                Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.7f);// 计算投射物的终点
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);// 调用线段碰撞检测
            }else
            {
                Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.7f);//计算投射物的终点
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);// 调用线段碰撞检测
            }
        }

        // 对瓦片进行类似的碰撞检测
        public override void CutTiles()
        {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;
            if (CurrentAttack != AttackType.WideSwing)
            {
                Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.9f);// 计算投射物的终点
                Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);// 绘制线段，并对其上的瓦片进行碰撞检测
            }
            else
            {
                Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.92f);// 计算投射物的终点
                Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);// 绘制线段，并对其上的瓦片进行碰撞检测
            }
        }

        // 确保投射物仅在释放阶段和放松阶段造成伤害
        public override bool? CanDamage()
        {
            if (CurrentStage == AttackStage.Prepare)
                return false;
            return base.CanDamage();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // 确保击退方向远离玩家
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;

            if(CurrentAttack == AttackType.WideSwing)   modifiers.FinalDamage *= 2f;
            
        }

        // 方便设置投射物和手臂位置的函数
        public void SetSwordPosition()
        {
            Vector2 mousPos = Main.MouseWorld; // 获取鼠标位置
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - (float)Math.PI / 2); // 获取手的位置

            armPosition.Y += Owner.gfxOffY; // 添加偏移
            Owner.heldProj = Projectile.whoAmI; // 设置持有的投射物为这个投射物
            if (CurrentAttack != AttackType.WideSwing)
            {
                Projectile.scale = Size * 1.2f * Owner.GetAdjustedItemScale(Owner.HeldItem); // 稍微放大投射物，也考虑到近战尺寸的修正
                // 设置复合手臂，允许你独立设置手臂的旋转和前后手臂的伸展
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.ToRadians(90f)); // 设置手臂位置（由于手臂起始时低下，所以有 90 度偏移）
                Projectile.rotation = InitialAngle + Projectile.spriteDirection * Progress; // 设置投射物的旋转

                Projectile.Center = armPosition + Projectile.rotation.ToRotationVector2() * 30f; // 设置投射物到手的位置，并向外偏移20像素
            }else
            {
                Projectile.scale = Size * 1.8f * Owner.GetAdjustedItemScale(Owner.HeldItem); // 稍微放大投射物，也考虑到近战尺寸的修正
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.ToRadians(90f)); // 设置手臂位置（由于手臂起始时低下，所以有 90 度偏移）
                Projectile.rotation = InitialAngle + Projectile.spriteDirection * Progress; // 设置投射物的旋转

                Projectile.Center = armPosition + Projectile.rotation.ToRotationVector2() * 50f; // 设置投射物到手的位置，并向外偏移20像素
            }
        }

        // 准备攻击的函数
        private void PrepareStrike()
        {
            Size = 1f; // 使剑在准备攻击时缓慢增加大小，直到达到最大值
            if (Timer >= prepTime)
            {
                SoundEngine.PlaySound(SoundID.Item1); // 播放剑的声音，因为在生成时播放太早
                CurrentStage = AttackStage.Execute; // 如果攻击超过准备时间，进入下一个阶段
            }
        }

        // 实现挥动的首半部分
        private void ExecuteStrike()
        {
            if (CurrentAttack == AttackType.Swing)
            {
                Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 2) * Timer / (execTime * 2));

                if (Timer >= execTime * 3)
                {
                    CurrentStage = AttackStage.Unwind; // 完成攻击，进入放松阶段
                }
            }
            else if (CurrentAttack == AttackType.Spin)
            {
                Progress = MathHelper.SmoothStep(0, -SPINRANGE, (1f - UNWIND / 2) * Timer / (execTime * SPINTIME * 2));

                if (Timer >= execTime * SPINTIME * 3)
                {
                    CurrentStage = AttackStage.Unwind; // 完成攻击，进入放松阶段
                }
            }
            else if (CurrentAttack == AttackType.WideSwing)
            {
                Progress = MathHelper.SmoothStep(0, WIDE_SWING_RANGE1, (1f - UNWIND / 2) * Timer / (execTime * WIDE_SWING_TIME*1.6f));

                if (Timer >= execTime * WIDE_SWING_TIME * 3)
                {
                    CurrentStage = AttackStage.Unwind; // 完成攻击，进入放松阶段
                }
            }

        }

        // 实现挥动后半部分，剑消失
        private void UnwindStrike()
        {
            if (CurrentAttack == AttackType.Swing)
            {
                Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime));
                if (Timer >= hideTime)
                {
                    Projectile.Kill(); // 完成隐藏阶段，杀死投射物
                }
            }
            else if (CurrentAttack == AttackType.Spin)
            {
                Progress = MathHelper.SmoothStep(0, -SPINRANGE, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime * SPINTIME));
                if (Timer >= hideTime * SPINTIME)
                {
                    Projectile.Kill(); // 完成隐藏阶段，杀死投射物
                }
            }
            else if (CurrentAttack == AttackType.WideSwing)
            {
                Progress = MathHelper.SmoothStep(0, WIDE_SWING_RANGE1, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime * WIDE_SWING_TIME));
                if (Timer >= hideTime * WIDE_SWING_TIME/2)
                {
                    Projectile.Kill(); // 完成隐藏阶段，杀死投射物
                }
            }
        }
    }
}
