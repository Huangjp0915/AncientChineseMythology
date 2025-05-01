using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace AncientChineseMythology.Projectiles
{
    public class CrimsonbronzeSwordProj1 : ModProjectile
    {
        private const float SWINGRANGE = 1.67f * (float)Math.PI; // 挥动攻击覆盖的角度（300度）
        private const float FIRSTHALFSWING = 0.45f; // 达到目标角度之前的挥动比例（相对于 swingRange）
        private const float WINDUP = 0.15f; // 玩家攻击前手臂向后摆动的程度（相对于 swingRange）
        private const float UNWIND = 0.4f; // 剑何时开始消失

        private ref float InitialAngle => ref Projectile.ai[1]; // 瞄准的角度（带有限制）
        private ref float Timer => ref Projectile.ai[2]; // 计时器，用于跟踪每个阶段的进度
        private ref float Progress => ref Projectile.localAI[1]; // 剑相对于初始角度的位置
        private ref float Size => ref Projectile.localAI[2]; // 剑的大小

        // 定义每个阶段的时间函数，考虑到近战攻击速度
        // 注意，你可以根据投射物的需要更改这个
        private float prepTime => 10f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float execTime => 8f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float hideTime => 10f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private bool isStart = false; // 标记是否开始挥舞
        private int timerCounter = 0; // 计时器的计数器，用于隐藏剑
        private Vector2 swordCenter; // 用于存储剑的中心位置
        private bool isattacking = false; // 标记是否正在攻击

        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/CrimsonbronzeSword"; // 使用物品的纹理作为投射物的纹理

        private Player Owner => Main.player[Projectile.owner];

        private enum AttackStage // 当前执行的攻击阶段，具体见 AI 中的函数描述
        {
            Prepare,
            Execute,
            Unwind
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

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 80; // 投射物的碰撞箱宽度
            Projectile.height = 80; // 投射物的碰撞箱高度
            Projectile.friendly = true; // 投射物可以击中敌人
            Projectile.timeLeft = 10000; // 投射物失效所需的时间
            Projectile.penetrate = -1; // 投射物无限穿透
            Projectile.tileCollide = false; // 投射物不与瓦片碰撞
            Projectile.usesLocalNPCImmunity = true; // 使用局部免疫帧
            Projectile.localNPCHitCooldown = -1; // 设置为 -1 以确保投射物不会命中两次
            Projectile.ownerHitCheck = true; // 确保投射物的拥有者有视线可以瞄准目标（即不能穿越瓦片击中目标）
            Projectile.DamageType = DamageClass.MeleeNoSpeed; // 投射物为近战投射物
        }

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
            InitialAngle = targetAngle - FIRSTHALFSWING * SWINGRANGE * Projectile.spriteDirection * 1.2f; // 计算角度
            Projectile.alpha = 200;
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
            Player player = Main.player[Projectile.owner];
            // 更新投射物的位置和旋转
            Projectile.oldPos[0] = Projectile.position;
            Projectile.oldRot[0] = Projectile.rotation;

            // 更新历史位置和旋转
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                Projectile.oldPos[i] = Projectile.oldPos[i - 1];
                Projectile.oldRot[i] = Projectile.oldRot[i - 1];
            }

            Owner.itemAnimation = 2; // 延长使用动画
            Owner.itemTime = 2;

            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed)
            {
                Projectile.Kill();
                return;
            }

            // 仅保留挥舞的逻辑
            if (CurrentStage == AttackStage.Prepare)
            {
                PrepareStrike();
                Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
                float targetAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
                InitialAngle = targetAngle - FIRSTHALFSWING * SWINGRANGE * Projectile.spriteDirection * 1.2f; // 计算角度
            }
            else if (CurrentStage == AttackStage.Execute)
            {
                ExecuteStrike();
                Attack_1();
            }
            else if (CurrentStage == AttackStage.Unwind)
            {
                UnwindStrike();
            }

            if (Main.MouseWorld.X > player.Center.X)
            {
                Vector2 directionToMouse = Main.MouseWorld - Owner.Center + new Vector2(0, 100f);
                directionToMouse.Normalize();
                swordCenter = Owner.Center - directionToMouse * 40f;
            }
            else
            {
                Vector2 directionToMouse = Main.MouseWorld - Owner.Center + new Vector2(100, 150f);
                directionToMouse.Normalize();
                swordCenter = Owner.Center - directionToMouse * 40f;
            }

            timerCounter++;
            if (Projectile.alpha > 1)
                Projectile.alpha -= timerCounter / 20;

            // 在 timerCounter 未达到 60 的时间里增大到 1
            //if (timerCounter <= 60)
            //{
            //    Projectile.scale = MathHelper.Lerp(0.1f, 1f, timerCounter / 60f);
            //}

            if (timerCounter > 60)
            {
                if (timerCounter % 10 == 0)
                {
                    // 粒子效果
                    int dustIndex = Dust.NewDust(swordCenter, 0, 0, DustID.Torch, 0f, 0f, 1, default, 1f);
                    Main.dust[dustIndex].noGravity = false;
                    Main.dust[dustIndex].velocity *= 0.2f;
                }
                if (!Main.mouseRight)
                    isStart = true;
            }
            else if (!Main.mouseRight)
                Projectile.Kill();

            SetSwordPosition();
            if (isStart || Timer <= 8)
                Timer++;
        }
        
        public override bool PreDraw(ref Microsoft.Xna.Framework.Color lightColor)
        {
            // 根据方向计算剑的原点（护手）并偏移剑的旋转（因为剑的贴图是倾斜的）
            Microsoft.Xna.Framework.Vector2 origin;
            float rotationOffset;
            SpriteEffects effects;

            if (Projectile.spriteDirection > 0)
            {
                origin = new Microsoft.Xna.Framework.Vector2(0, Projectile.height);
                rotationOffset = MathHelper.ToRadians(45f);
                effects = SpriteEffects.None;
            }
            else
            {
                origin = new Microsoft.Xna.Framework.Vector2(Projectile.width, Projectile.height);
                rotationOffset = MathHelper.ToRadians(135f);
                effects = SpriteEffects.FlipHorizontally;
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            // 设置拖尾效果
            Microsoft.Xna.Framework.Color MyColor = Microsoft.Xna.Framework.Color.Gold;
            MyColor.A = 0; // 设置A为255以确保可见
                           // 计算绘制位置和大小
            Microsoft.Xna.Framework.Rectangle destinationRectangle = new Microsoft.Xna.Framework.Rectangle(
                0, 0, (int)(Projectile.width), (int)(Projectile.height)
                );
            if(timerCounter > 60)
            //先绘制拖尾
            for (int i = 0; i < 9; i++) // 循环上限小于轨迹长度
            {
                float factor = 0.5f - (float)i / 18; // 计算透明度因子
                Microsoft.Xna.Framework.Vector2 oldCenter = Projectile.oldPos[i + 1] + Projectile.Size / 2 - Main.screenPosition; // 获取旧位置的中心点
                // 绘制拖尾
                Main.EntitySpriteDraw(texture, oldCenter,
                    destinationRectangle,
                    MyColor * factor, // 颜色逐渐变淡
                    Projectile.oldRot[i] + rotationOffset, // 弹幕轨迹上的曾经的方向
                    origin, // 贴图参照原点在左上角
                    Projectile.scale * 1f, // 缩放
                    effects,
                    0); // 层级
            }
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, default, lightColor * Projectile.Opacity, Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            // 由于我们在进行自定义绘制，因此不进行正常绘制
            return false;
        }


        // 找到剑的起始和结束位置，并使用线段碰撞检测与敌人检查碰撞
        public override bool? Colliding(Microsoft.Xna.Framework.Rectangle projHitbox, Microsoft.Xna.Framework.Rectangle targetHitbox)
        {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;
            Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * ((Projectile.Size.Length()) * Projectile.scale * 1.05f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 15f * Projectile.scale, ref collisionPoint);
        }

        // 对瓦片进行类似的碰撞检测
        public override void CutTiles()
        {
            Microsoft.Xna.Framework.Vector2 start = Owner.MountedCenter;
            Microsoft.Xna.Framework.Vector2 end = start + Projectile.rotation.ToRotationVector2() * (Projectile.Size.Length() * Projectile.scale * 1.05f);
            Utils.PlotTileLine(start, end, 15 * Projectile.scale, DelegateMethods.CutTiles);
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

        }

        // 方便设置投射物和手臂位置的函数
        public void SetSwordPosition()
        {
            Player player = Main.player[Projectile.owner];
            Projectile.rotation = InitialAngle + Projectile.spriteDirection * Progress; // 设置投射物的旋转

            // 设置复合手臂，允许你独立设置手臂的旋转和前后手臂的伸展
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                player.direction == 1 ? Projectile.rotation - MathHelper.ToRadians(80f) : Projectile.rotation - MathHelper.ToRadians(110f)); // 设置手臂位置（由于手臂起始时低下，所以有 90 度偏移）
            Vector2 armPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - (float)Math.PI / 2); // 获取手的位置

            armPosition.Y += Owner.gfxOffY; // 添加偏移
            Projectile.Center = armPosition - Projectile.rotation.ToRotationVector2() * 1f; // 设置投射物到手的位置
            // 保持对 scale 的设置
            Projectile.scale = MathHelper.Lerp(0.1f, 1f, Math.Min(timerCounter / 60f, 1f));

            Owner.heldProj = Projectile.whoAmI; // 设置持有的投射物为这个投射物
        }

        // 准备攻击的函数
        private void PrepareStrike()
        {
            Progress = WINDUP * SWINGRANGE * (1f - Timer / prepTime); // 从初始角度计算旋转
            Size = MathHelper.SmoothStep(0, 1, Timer / prepTime); // 增加大小

            if (Timer >= prepTime)
            {
                SoundEngine.PlaySound(SoundID.Item1); // 播放声音
                CurrentStage = AttackStage.Execute; // 进入执行阶段
            }
        }

        // 执行挥动的函数
        private void ExecuteStrike()
        {
            Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 2) * Timer / (execTime * 2));

            if (Timer >= execTime * 3)
            {
                CurrentStage = AttackStage.Unwind; // 完成攻击，进入放松阶段
            }
        }

        // 放松的函数，剑消失
        private void UnwindStrike()
        {
            Progress = MathHelper.SmoothStep(0, SWINGRANGE, (1f - UNWIND / 10) + UNWIND * Timer / hideTime);

            if (Timer >= hideTime)
            {
                Projectile.Kill(); // 杀死投射物
            }
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if(!target.HasBuff(BuffID.OnFire))
            {
                target.AddBuff(BuffID.OnFire, 180); // 给受伤的敌人添加燃烧效果
            }
        }
        private void Attack_1()
        {
            if (!isattacking)
            {
                // 获取玩家中心位置
                Microsoft.Xna.Framework.Vector2 playerCenter = Owner.MountedCenter;

                // 计算指向鼠标的方向向量
                Microsoft.Xna.Framework.Vector2 mouseDirection = Main.MouseWorld - playerCenter; // 从玩家到鼠标的向量

                // 计算单位向量（将其标准化）
                Microsoft.Xna.Framework.Vector2 unitDirection = mouseDirection.SafeNormalize(Microsoft.Xna.Framework.Vector2.UnitY); // 计算单位向量

                // 计算发射位置 (在该单位向量基础上偏移像素)
                Microsoft.Xna.Framework.Vector2 position = playerCenter + unitDirection * 30f; // 像素的半径偏移

                // 计算发射方向指向鼠标的方向
                Microsoft.Xna.Framework.Vector2 direction = (Main.MouseWorld - position).SafeNormalize(Microsoft.Xna.Framework.Vector2.UnitY);

                // 创建新的弹幕
                int newProjectile = Projectile.NewProjectile(Owner.GetSource_FromThis(), position, direction * 21, ModContent.ProjectileType<CrimsonbronzeSwordProj2>(), (int)Projectile.damage, 0f, Projectile.owner);
                isattacking = true; // 标记为正在攻击
            }
        }
    }

    class CrimsonbronzeSwordProj2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/CrimsonbronzeSwordProj2"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults()
        {
            Projectile.knockBack = 0.6f; // 击退
            Projectile.width = 90; // 弹幕宽度
            Projectile.height = 90; // 弹幕高度
            Projectile.friendly = true; // 友方弹幕
            Projectile.tileCollide = false; // 不与瓷砖碰撞
            Projectile.DamageType = DamageClass.MeleeNoSpeed; // 投射物为近战投射物
            Projectile.penetrate = -1; // 穿透
            Projectile.ignoreWater = true; // 无视液体
            Projectile.timeLeft = 60; // 存在时间，单位为帧
            Projectile.alpha = 1; // 透明度
            Projectile.light = 0.75f; // 发光亮度
        }
        public override void OnSpawn(IEntitySource source)
        {
            int randomValue = Main.rand.Next(100);

            if (randomValue < 35)
                Projectile.damage *= 2;
            else if (randomValue < 65)
                Projectile.damage *= 3;
            else if (randomValue < 80)
                Projectile.damage *= 4;
            else if (randomValue < 90)
                Projectile.damage *= 5;
            else if (randomValue < 95)
                Projectile.damage *= 6;
            else if (randomValue < 98)
                Projectile.damage *= 7;
            else
                Projectile.damage *= 8;
        }
        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            // 绘制气体
            int num = 5;
            for (int i = 0; i < num; i++)
            {
                int dustIndex = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), 
                    Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, Color.DarkGoldenrod, 2f);
                Main.dust[dustIndex].noGravity = true;
            }
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!target.HasBuff(BuffID.OnFire))
            {
                target.AddBuff(BuffID.OnFire, 180); // 给受伤的敌人添加燃烧效果
            }
        }
        public override bool PreDraw(ref Color lightColor)//predraw返回false即可禁用原版绘制
        {
            Main.projFrames[Type] = 1;//设置帧数为1，因为我们只需要一个帧的弹幕
            ProjectileID.Sets.TrailingMode[Type] = 2;//设置尾迹模式为2，即尾迹为圆形
            ProjectileID.Sets.TrailCacheLength[Type] = 8;//设置尾迹缓存长度为5，即最多保留5个尾迹
            //同时，需要进行的绘制在这里面写就好

            Texture2D texture = TextureAssets.Projectile[Type].Value;//声明本弹幕的材质
            Rectangle rectangle = new Rectangle(//因为手动绘制需要自己填写帧图框,所以要先算出来
                0,//这个框的左上角的水平坐标(填0就好)
                texture.Height / Main.projFrames[Type] * Projectile.frame,//框的左上角的纵向坐标
                texture.Width, //框的宽度(材质宽度即可)
                texture.Height / Main.projFrames[Type]//框的高度（用材质高度除以帧数得到单帧高度）
                );

            //要制作拖尾，首先要建立一个for循环语句，从0一直走到轨迹末端
            //这里我们介绍一个能产生高亮叠加绘制的办法（A=0）
            Color MyColor = Color.White * 1f;
            MyColor.A = 0;//让A=0是为了能直接叠加颜色
            for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++)//循环上限小于轨迹长度
            {
                float factor = 1 - (float)i / ProjectileID.Sets.TrailCacheLength[Type];//计算当前位置的透明度因子
                //定义一个从新到旧由1逐渐减少到0的变量，比如i = 0时，factor = 1
                Vector2 oldcenter = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;//获取旧位置的中心点
                //由于轨迹只能记录弹幕碰撞箱左上角位置，我们要手动加上弹幕宽高一半来获取中心
                Main.EntitySpriteDraw(texture, oldcenter, rectangle, MyColor * factor,//颜色逐渐变淡
                    Projectile.oldRot[i],//弹幕轨迹上的曾经的方向
                    new Vector2(texture.Width / 2, texture.Height / 2 / Main.projFrames[Type]),
                     new Vector2(1),
                     SpriteEffects.None, 0);//最后两个参数是贴图缩放和旋转，这里不用管
            }
            //由于tr绘制是先执行的先绘制，所以要想残影不覆盖到本体上面，就要先写残影绘制

            Main.EntitySpriteDraw(  //entityspritedraw是弹幕，NPC等常用的绘制方法
                texture,//第一个参数是材质
                Projectile.Center - Main.screenPosition,//注意，绘制时的位置是以屏幕左上角为0点
                                                        //因此要用弹幕世界坐标减去屏幕左上角的坐标
                rectangle,//第三个参数就是帧图选框了
                Color.White * 0.5f,//第四个参数是颜色，这里我们用自带的lightcolor，可以受到自然光照影响
                                   //Color.White,
                Projectile.rotation,//第五个参数是贴图旋转方向
                new Vector2(texture.Width / 2, texture.Height / 2 / Main.projFrames[Type]),
                //第六个参数是贴图参照原点的坐标，这里写为贴图单帧的中心坐标，这样旋转和缩放都是围绕中心
                new Vector2(1),//第七个参数是缩放，X是水平倍率，Y是竖直倍率
                SpriteEffects.None,
                //第八个参数是设置图片翻转效果，需要手动判定并设置spriteeffects
                0//第九个参数是绘制层级，但填0就行了，不太好使
                );

            return false;//return false阻止自动绘制
        }
        // 弹幕消失时的特效
        [Obsolete]
        public override void Kill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default(Color), 2f);
                Main.dust[dustIndex].velocity *= 3f;
                if (Main.rand.NextBool(2))
                {
                    Main.dust[dustIndex].scale = 0.5f;
                    Main.dust[dustIndex].fadeIn = 1f + (float)Main.rand.Next(10) * 0.1f;
                }
            }
        }

    }
}
