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
using AncientChineseMythology.Helpers;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.Projectiles
{
    public class RuyiStickPlayer : ModPlayer
    {
        public bool reduceDamageToOne = false;
        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (reduceDamageToOne) {
                modifiers.FinalDamage *= 0.2f;
                //modifiers.SetMaxDamage(1);
            }
        }
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (reduceDamageToOne) {
                modifiers.FinalDamage *= 0.2f;
                //modifiers.SetMaxDamage(1);
            }
        }
    }

    internal class RuyiStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";
        private Color swingColor;
        private Vector2 initialPlayerPosition; //记录玩家初始位置
        private Vector2 dashTargetPosition; //记录冲刺目标位置
        private bool isDashing = false; //标记是否正在冲刺
        private bool isFull = false; //标记是否已经完全展开
        public static bool isA = false;
        private int fallCount = 0;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.scale = 1.2f;
            Projectile.alpha = 1;
            Projectile.ownerHitCheck = true;
            Projectile.hide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
        }
        private Player Owner => Main.player[Projectile.owner];
        public override void OnSpawn(IEntitySource source) {
            initialPlayerPosition = Owner.Center; //记录玩家初始位置
            Projectile.damage = 0;
        }
        private Vector2 dashDirection; //储存冲刺方向向量
        private float dashDistanceRemaining; //剩余冲刺距离

        public override void AI() {
            if (Main.MouseWorld.X > Owner.Center.X) {
                Owner.direction = 1;
            }
            else {
                Owner.direction = -1;
            }
            //棍子始终保持在玩家上方
            Projectile.Center = initialPlayerPosition + new Vector2(0, -60f);

            Owner.heldProj = Projectile.whoAmI;

            //检测鼠标右键是否按住
            if (Main.mouseRight && !isDashing) {
                Projectile.timeLeft = 60;

                //限制玩家向上移动 120 像素
                if (Owner.Center.Y >= initialPlayerPosition.Y - 120f && !isFull) {
                    Owner.velocity.Y = -15; //向上移动
                    fallCount++;
                    if (fallCount > 30 && Owner.Center.Y != initialPlayerPosition.Y - 120f) {
                        Owner.position.Y = initialPlayerPosition.Y - 120; //停止移动
                        isFull = true; //标记已经完全展开
                    }
                }
                else {
                    if (Main.mouseLeft) {
                        Owner.GetModPlayer<RuyiStickPlayer>().reduceDamageToOne = false; //重置标志
                        Projectile.Kill();
                    }
                    else Owner.GetModPlayer<RuyiStickPlayer>().reduceDamageToOne = true;

                    isFull = true; //标记已经完全展开
                    Owner.position.Y = initialPlayerPosition.Y - 120; //停止移动
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.None, MathHelper.ToRadians(-180f)); //设置手臂位置
                    isA = true;

                }
                Owner.position.X = initialPlayerPosition.X; //锁定横坐标
            }
            else if (!isDashing && isFull) {
                isDashing = true; //开始冲刺
                //松开右键时，计算冲刺方向向量
                dashDirection = Vector2.Normalize(Main.MouseWorld - Owner.Center); //计算方向向量
                dashDistanceRemaining = 300f; //固定冲刺距离
            }
            else if (!isDashing && !isFull)
                Projectile.Kill();

            //冲刺逻辑
            if (isDashing) {
                Projectile.timeLeft = 60;
                //每帧移动固定距离
                float dashStep = 30f; //每帧移动的距离
                if (dashDistanceRemaining > 0f) {
                    //计算本帧移动的实际距离
                    float moveDistance = Math.Min(dashStep, dashDistanceRemaining);
                    Owner.position += dashDirection * moveDistance; //更新玩家位置
                    dashDistanceRemaining -= moveDistance; //减少剩余冲刺距离
                    if (Owner.direction == 1)
                        Owner.fullRotation += 0.5f; //玩家旋转
                    else
                        Owner.fullRotation -= 0.5f; //玩家旋转
                }
                else {
                    //冲刺结束
                    Owner.fullRotation = 0f; //玩家旋转为 0 度
                    Projectile.scale -= 0.1f;
                    if (Projectile.scale < 0.4f) {
                        Projectile.Kill();
                    }
                }
            }
        }
        [Obsolete]
        public override void Kill(int timeLeft) {
            Owner.GetModPlayer<RuyiStickPlayer>().reduceDamageToOne = false; //重置标志
            Owner.fullRotation = 0f; //玩家旋转为 0 度
            //生成碰撞粒子效果
            for (int i = 0; i < 10; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, Scale: 1.5f);
            }
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //确保击退方向远离玩家
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }
        public override void OnKill(int timeLeft) {
            Owner.velocity *= 0.5f; //玩家速度减半
        }
        public override bool PreDraw(ref Microsoft.Xna.Framework.Color lightColor) {
            //蓄力上举核心致命红辉光
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.7f, new Color(250, 40, 56));

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(texture.Width / 2, texture.Height / Main.projFrames[Type] / 2); //设置原点为中心;
            Rectangle rectangle = new Rectangle(
                0,
                texture.Height / Main.projFrames[Type] * Projectile.frame,
                texture.Width,
                texture.Height / Main.projFrames[Type]
            );

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition + new Vector2(10, 0),
                rectangle,
                Color.White,
                Projectile.rotation + MathHelper.ToRadians(-45f), //使用当前旋转角度
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0);
            return false;
        }
    }

    internal class RuyiStickSpearProjectile_3 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";
        //定义一些常量，决定剑的挥动范围
        //注意，我们在这里使用乘数，因为这简化了这些交互的调整
        //你可以更改这些值或完全替换它们，但这些值是根据外观调整的
        private const float SWINGRANGE = 2f * (float)Math.PI; //挥动攻击覆盖的角度（300度）
        private const float FIRSTHALFSWING = 0.45f; //达到目标角度之前的挥动比例（相对于 swingRange）
        private const float UNWIND = 0.4f; //剑何时开始消失
        private enum AttackType //当前进行的攻击类型
        {
            Swing, //挥动,上到下
        }

        private enum AttackStage //当前执行的攻击阶段，具体见 AI 中的函数描述
        {
            Prepare,
            Execute,
            Unwind
        }

        //这些属性封装了常规的 ai 和 localAI 数组，以便更简洁易懂
        private AttackType CurrentAttack {
            get => (AttackType)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.localAI[0];
            set {
                Projectile.localAI[0] = (float)value;
                Timer = 0; //切换状态时重置计时器
            }
        }

        //运行时跟踪的变量
        private ref float InitialAngle => ref Projectile.ai[1]; //瞄准的角度（带有限制）
        private ref float Timer => ref Projectile.ai[2]; //计时器，用于跟踪每个阶段的进度
        private ref float Progress => ref Projectile.localAI[1]; //剑相对于初始角度的位置
        private ref float Size => ref Projectile.localAI[2]; //剑的大小

        //定义每个阶段的时间函数，考虑到近战攻击速度
        //注意，你可以根据投射物的需要更改这个
        private float prepTime => 4f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float execTime => 6f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float hideTime => 4f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private Player Owner => Main.player[Projectile.owner];
        private bool isHit = false;
        private Vector2 HitPositon = Vector2.Zero;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2; //尾随模式为 2，表示尾随着玩家
            ProjectileID.Sets.TrailCacheLength[Type] = 12; //尾迹缓存长度
            base.SetStaticDefaults();
        }

        public override void SetDefaults() {
            Projectile.width = 108; //投射物的碰撞箱宽度
            Projectile.height = 108; //投射物的碰撞箱高度
            Projectile.friendly = true; //投射物可以击中敌人
            Projectile.timeLeft = 60; //投射物失效所需的时间
            Projectile.penetrate = -1; //投射物无限穿透
            Projectile.tileCollide = false; //投射物不与瓦片碰撞
            Projectile.usesLocalNPCImmunity = true; //使用局部免疫帧
            Projectile.localNPCHitCooldown = 10; //设置局部NPC命中冷却时间
            Projectile.ownerHitCheck = true; //确保投射物的拥有者有视线可以瞄准目标（即不能穿越瓦片击中目标）
            Projectile.DamageType = DamageClass.Melee; //投射物为近战投射物
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
            if (Projectile.spriteDirection == 1) {
                //不过，我们限制可能方向的范围，以免看起来太过荒谬
                targetAngle = MathHelper.Clamp(targetAngle, (float)-Math.PI * 1 / 3, (float)Math.PI * 1 / 6);
            }
            else {
                if (targetAngle < 0) {
                    targetAngle += 2 * (float)Math.PI; //使角度范围连续，以便于操作
                }

                targetAngle = MathHelper.Clamp(targetAngle, (float)Math.PI * 5 / 6, (float)Math.PI * 4 / 3);
            }

            InitialAngle = targetAngle - FIRSTHALFSWING * SWINGRANGE * Projectile.spriteDirection * 1.2f; //否则我们计算角度
        }

        public override void SendExtraAI(BinaryWriter writer) {
            //这个投射物的 Projectile.spriteDirection 在 OnSpawn 中根据拥有者的鼠标位置得出，因此需要同步。spriteDirection 不是自动同步的字段. 由于所有 Projectile.ai 插槽都已使用，因此我们将其手动同步。
            writer.Write((sbyte)Projectile.spriteDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.spriteDirection = reader.ReadSByte();
        }
        //修改 OnCollideWithGround 方法
        private void OnCollideWithGround() {
            //isHit = true;
            //标记进入碰撞状态
            CurrentStage = AttackStage.Unwind;

            //触发屏幕震动效果
            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 20);
            //定海神针落地: 相变级屏震 + 致命纯红落点演出
            WeaponVFX.AddScreenShake(Projectile.Center, 10f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), Projectile.Center,
                ACMWeaponBurst.Fatal, scale: 1.6f, owner: Projectile.owner);

            //播放碰撞音效
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);

            //停止投射物移动并暂停挥舞
            Projectile.velocity = Vector2.Zero;
            Timer = execTime * 2; //保持当前挥舞进度
            Projectile.timeLeft = 60; //设置较长的剩余时间，防止投射物立即消失
        }

        //修改 AI 方法
        public override void AI() {
            //更新投射物的位置和旋转
            Projectile.oldPos[0] = Projectile.position;
            Projectile.oldRot[0] = Projectile.rotation;

            //添加光效
            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3() * 0.5f);

            //更新历史位置和旋转
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            }

            //在投射物被杀死之前延长使用动画
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            //如果玩家死去或被控制，杀死投射物
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            //检测与地面碰撞
            Vector2 start = Owner.MountedCenter; //投射物的起点
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.7f); //投射物的终点

            bool isCollidingWithTile = false;

            //遍历线段上的瓦片
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, (x, y) => {
                //检查瓦片是否存在、是实心瓦片且未被激活（未被触发）
                if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType] && !Main.tile[x, y].IsActuated) {
                    //检查瓦片是否可以挡住玩家
                    if (!Main.tileSolidTop[Main.tile[x, y].TileType]) {
                        isCollidingWithTile = true;
                        return false; //终止遍历
                    }
                }
                return true; //继续遍历
            });

            if (isCollidingWithTile && !isHit && CurrentStage == AttackStage.Execute
                && Timer >= execTime * 1.2f) {
                //生成碰撞粒子效果
                for (int i = 0; i < 20; i++) {
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, Scale: 1.5f);
                }
                OnCollideWithGround();
                isHit = true;
            }

            //如果已经击中物块，暂停 Timer 的递增
            if (isHit) {
                if (Projectile.timeLeft <= 2) {
                    Projectile.timeLeft = 2;
                    Projectile.scale -= 0.2f;
                    if (Projectile.scale < 0.4f) {
                        //生成碰撞粒子效果
                        for (int i = 0; i < 20; i++) {
                            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, Scale: 1.5f);
                        }
                        Projectile.Kill();
                    }
                }
                return; //停止后续逻辑，保持当前状态
            }

            //根据阶段执行逻辑
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

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //确保击退方向远离玩家
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            modifiers.FinalDamage *= 5f;

            if (CurrentStage == AttackStage.Execute && !isHit && Timer >= execTime && target.lifeMax != 1) {
                OnCollideWithGround();
                isHit = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //重置投射物的碰撞检测，以便可以多次击中敌人
            Projectile.localNPCImmunity[target.whoAmI] = 10; //设置局部NPC命中冷却时间
            target.immune[Projectile.owner] = 0; //确保敌人不会对投射物的拥有者免疫
            //定海神针放大突刺命中: 相变级屏震 + 致命纯红爆裂
            WeaponVFX.AddScreenShake(target.Center, 10f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Fatal, scale: 1.6f, owner: Projectile.owner);

            //计算召唤物与目标之间的距离
            Vector2 toTarget = target.Center - Projectile.Center;
            //float distanceToTarget = toTarget.Length();
            //计算弹幕的速度向量
            Vector2 velocity = toTarget.SafeNormalize(Vector2.Zero) * 0f;
            //发射弹幕
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, velocity,
                85, //弹幕ID
                Projectile.originalDamage, Projectile.knockBack, Projectile.owner);
        }
        //确保投射物仅在释放阶段和放松阶段造成伤害
        public override bool? CanDamage() {
            if (CurrentStage != AttackStage.Prepare) {
                return true;
            }

            return false;
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
            //定海神针放大突刺: 致命纯红粗拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 14f,
                outerColor: new Color(120, 10, 20, 170), innerColor: new Color(250, 40, 56, 220),
                uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);
            //蓄力 (Prepare 阶段) 矛尖致命红径向预警
            if (Projectile.localAI[0] == 0f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.08f, 0.6f, new Color(250, 40, 56), 10f);

            Microsoft.Xna.Framework.Vector2 origin;
            float rotationOffset;
            SpriteEffects effects; //贴图效果

            if (Projectile.spriteDirection > 0) {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width / 2, Projectile.height / 2); //原点在中心
                rotationOffset = MathHelper.ToRadians(45f); //旋转偏移45度
                effects = SpriteEffects.None; //贴图不翻转
            }
            else {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width / 2, Projectile.height / 2); //原点在中心
                rotationOffset = MathHelper.ToRadians(135f); //旋转偏移135度
                effects = SpriteEffects.FlipHorizontally; //翻转贴图
            }

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            //开始顶点绘制

            List<ColoredVertex> ve = new List<ColoredVertex>();

            Color color = new(250, 20, 60);

            Player player = Main.player[Projectile.owner];
            if (CurrentStage != AttackStage.Prepare && Timer >= execTime / 2) {
                if (Projectile.spriteDirection > 0) {
                    for (int i = 0; i < 12; i++) {
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -220).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -80).RotatedBy(Projectile.oldRot[i] + rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));

                    }
                }
                else {
                    for (int i = 0; i < 12; i++) {

                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -80).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 0, 1),
                            color));
                        ve.Add(new ColoredVertex(player.Center - Main.screenPosition + new Vector2(0, -220).RotatedBy(Projectile.oldRot[i] - rotationOffset * 2),
                            new Vector3(i / 12f, 1, 1),
                            color));
                    }
                }
            }

            if (ve.Count >= 3) {
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

            //由于我们在进行自定义绘制，因此不进行正常绘制
            return false;
        }

        //找到剑的起始和结束位置，并使用线段碰撞检测与敌人检查碰撞
        public override bool? Colliding(Microsoft.Xna.Framework.Rectangle projHitbox, Microsoft.Xna.Framework.Rectangle targetHitbox) {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;//计算投射物的起点
            if (isHit) start = Projectile.Center; //如果已经击中，则使用击中点作为起点
            float collisionPoint = 0f;//碰撞点
            {
                Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.7f);//计算投射物的终点
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);//调用线段碰撞检测
            }
        }

        //对瓦片进行类似的碰撞检测
        public override void CutTiles() {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;
            if (isHit) start = Projectile.Center; //如果已经击中，则使用击中点作为起点
            Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 0.92f);//计算投射物的终点
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);//绘制线段，并对其上的瓦片进行碰撞检测
        }

        //方便设置投射物和手臂位置的函数
        public void SetSwordPosition() {
            Vector2 mousPos = Main.MouseWorld; //获取鼠标位置
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - (float)Math.PI / 2); //获取手的位置

            armPosition.Y += Owner.gfxOffY; //添加偏移
            Owner.heldProj = Projectile.whoAmI; //设置持有的投射物为这个投射物

            Projectile.scale = Size * 2.4f * Owner.GetAdjustedItemScale(Owner.HeldItem); //稍微放大投射物，也考虑到近战尺寸的修正
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.ToRadians(90f)); //设置手臂位置（由于手臂起始时低下，所以有 90 度偏移）
            Projectile.rotation = InitialAngle + Projectile.spriteDirection * Progress; //设置投射物的旋转

            Projectile.Center = armPosition + Projectile.rotation.ToRotationVector2() * 50f; //设置投射物到手的位置，并向外偏移20像素
        }

        //准备攻击的函数
        private void PrepareStrike() {
            Size = 1f; //使剑在准备攻击时缓慢增加大小，直到达到最大值
            if (Timer >= prepTime) {
                SoundEngine.PlaySound(SoundID.Item1); //播放剑的声音，因为在生成时播放太早
                CurrentStage = AttackStage.Execute; //如果攻击超过准备时间，进入下一个阶段
            }
        }

        //实现挥动的首半部分
        private void ExecuteStrike() {
            Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 2) * Timer / (execTime * 2));

            if (Timer >= execTime * 3) {
                CurrentStage = AttackStage.Unwind; //完成攻击，进入放松阶段
            }
        }

        //实现挥动后半部分，剑消失
        private void UnwindStrike() {
            Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 10) + UNWIND * Timer / (hideTime));
            if (Timer >= hideTime) {
                Projectile.Kill(); //完成隐藏阶段，杀死投射物
            }

        }
    }
}



