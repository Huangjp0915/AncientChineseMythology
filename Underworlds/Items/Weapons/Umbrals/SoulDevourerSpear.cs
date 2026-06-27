using System;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 噬魂枪 - 吞噬亡魂的地府长枪，使用手持弹幕实现长枪突刺动画
    /// 肉后初期，击杀敌人有几率恢复生命
    /// </summary>
    public class SoulDevourerSpear : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 152; //基础伤害
            Item.crit = 5; //暴击率
            Item.DamageType = DamageClass.Melee; //近战伤害类型
            Item.width = 48; //物品宽度
            Item.height = 48; //物品高度
            Item.useTime = 24; //使用时间
            Item.useAnimation = 24; //使用动画时间
            Item.useStyle = ItemUseStyleID.Shoot; //射击风格（用于手持弹幕）
            Item.knockBack = 4f; //击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item1; //使用声音
            Item.autoReuse = true; //自动连击
            Item.noMelee = true; //不使用物品本身的碰撞
            Item.noUseGraphic = true; //隐藏物品图形，使用弹幕显示
            Item.shoot = ModContent.ProjectileType<SoulDevourerSpearProjectile>(); //发射噬魂枪弹幕
            Item.shootSpeed = 3.5f; //影响突刺速度
        }

        public override bool CanUseItem(Player player) {
            //确保同时只有一个枪存在
            return player.ownedProjectileCounts[ModContent.ProjectileType<SoulDevourerSpearProjectile>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 噬魂枪弹幕 - 手持突刺型长枪弹幕
    /// 实现向前突刺再收回的动画效果
    /// </summary>
    public class SoulDevourerSpearProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Umbrals/SoulDevourerSpear";

        //攻击阶段
        private enum AttackStage { Prepare, Thrust, Retract }

        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set {
                Projectile.ai[0] = (float)value;
                Timer = 0;
            }
        }

        private ref float Timer => ref Projectile.ai[1];
        private ref float ThrustDistance => ref Projectile.localAI[0]; //当前突刺距离

        private const float MaxThrustDistance = 26f; //最大突刺距离
        private const float BaseOffset = 4f; //基础偏移（枪柄位置）

        private Player Owner => Main.player[Projectile.owner];

        //各阶段时间
        private float PrepareTime => 4f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 8f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 6f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 68;
            Projectile.height = 68;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; //无限穿透
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void OnSpawn(IEntitySource source) {
            //设置初始朝向
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void AI() {
            //如果玩家死亡或无法行动，杀死弹幕
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            //延长使用动画
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            //根据阶段执行不同逻辑
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

            //更新位置和旋转
            UpdatePositionAndRotation();

            //生成幽灵粒子
            SpawnSoulParticles();

            //光照效果（幽蓝色）
            Lighting.AddLight(Projectile.Center, 0.3f, 0.4f, 0.6f);

            Timer++;
        }

        private void HandlePrepare() {
            //蓄力阶段：略微后拉
            ThrustDistance = MathHelper.Lerp(0, -10f, Timer / PrepareTime);

            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f }, Projectile.Center);
            }
        }

        private void HandleThrust() {
            //突刺阶段：快速向前伸出
            float progress = Timer / ThrustTime;
            ThrustDistance = MathHelper.SmoothStep(-10f, MaxThrustDistance, progress);

            //突刺接近顶点时在枪尖留下噬魂裂隙 (纯视觉, 仅生成一次, 本地玩家)
            if (progress >= 0.85f && Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() * 44f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), tip, Vector2.Zero,
                        ModContent.ProjectileType<SoulDevourerRift>(), 0, 0f, Projectile.owner,
                        Projectile.rotation);
                }
            }

            if (Timer >= ThrustTime) {
                CurrentStage = AttackStage.Retract;
            }
        }

        private void HandleRetract() {
            //收回阶段：缓慢收回
            float progress = Timer / RetractTime;
            ThrustDistance = MathHelper.SmoothStep(MaxThrustDistance, 0, progress);

            if (Timer >= RetractTime) {
                Projectile.Kill();
            }
        }

        private void UpdatePositionAndRotation() {
            //计算朝向鼠标的方向
            Vector2 direction = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation();

            //更新朝向
            Projectile.spriteDirection = direction.X > 0 ? 1 : -1;
            Owner.direction = Projectile.spriteDirection;

            //设置手臂位置
            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            //获取手的位置
            Vector2 handPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);
            handPosition.Y += Owner.gfxOffY;

            //设置枪的位置（基础偏移 + 突刺距离）
            Projectile.Center = handPosition + direction * (BaseOffset + ThrustDistance);
        }

        private void SpawnSoulParticles() {
            //突刺时产生更多粒子
            if (CurrentStage == AttackStage.Thrust && Main.rand.NextBool(2)) {
                //幽灵粒子
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center + Projectile.rotation.ToRotationVector2() * 30f,
                    8, 8,
                    DustID.Wraith,
                    0f, 0f,
                    100,
                    default,
                    Main.rand.NextFloat(1.0f, 1.5f)
                );
                soul.noGravity = true;
                soul.velocity = -Projectile.rotation.ToRotationVector2() * 2f;
            }

            //暗影焰粒子
            if (Main.rand.NextBool(4)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center,
                    10, 10,
                    DustID.Shadowflame,
                    0f, 0f,
                    150,
                    default,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                shadow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //噬魂效果：攻击时有几率吸取灵魂恢复生命
            if (Main.rand.NextBool(4)) {
                int healAmount = Main.rand.Next(5, 12);
                Owner.Heal(healAmount);

                //产生灵魂吸取特效
                for (int i = 0; i < 8; i++) {
                    Vector2 velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);
                    velocity = velocity.RotatedByRandom(MathHelper.ToRadians(30));

                    Dust soul = Dust.NewDustDirect(
                        target.Center,
                        4, 4,
                        DustID.Wraith,
                        velocity.X, velocity.Y,
                        100,
                        default,
                        1.3f
                    );
                    soul.noGravity = true;
                }

                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.5f }, target.Center);
            }

            //给敌人附加暗影焰
            target.AddBuff(BuffID.ShadowFlame, 120); //2秒暗影焰

            //击中冷蓝魂火辉光演出 + 轻度小扭曲冲击 (代替成片 Dust)
            for (int i = 0; i < 3; i++) {
                Dust burst = Dust.NewDustDirect(
                    target.Center,
                    10, 10,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-4f, 4f),
                    Main.rand.NextFloat(-4f, 4f),
                    100,
                    default,
                    1.5f
                );
                burst.noGravity = true;
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: 0.9f, owner: Projectile.owner);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //使用线段碰撞检测
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 50f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 18f, ref collisionPoint);
        }

        public override void CutTiles() {
            //切割草等
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 50f);
            Utils.PlotTileLine(start, end, 18f, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() {
            //只在突刺阶段造成伤害
            return CurrentStage == AttackStage.Thrust;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //确保击退方向远离玩家
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2;
            float rotationOffset;
            SpriteEffects effects;

            if (Projectile.spriteDirection > 0) {
                //origin = new Vector2(0, texture.Height / 2f); //从左侧中心开始
                rotationOffset = MathHelper.PiOver4;
                effects = SpriteEffects.None;
            }
            else {
                //origin = new Vector2(texture.Width, texture.Height / 2f); //从右侧中心开始
                rotationOffset = MathHelper.Pi - MathHelper.PiOver4;
                effects = SpriteEffects.FlipHorizontally;
            }

            //冷蓝魂火枪身光束 (沿枪杆方向, 突刺时更强)
            Vector2 shaftDir = Projectile.rotation.ToRotationVector2();
            Vector2 tipPos = Projectile.Center + shaftDir * 46f;
            Vector2 tailPos = Projectile.Center - shaftDir * 22f;
            float beamI = CurrentStage == AttackStage.Thrust ? 1f : 0.55f;
            ACMShaders.DrawBeam(tailPos, tipPos, halfWidth: 9f,
                core: new Color(150, 230, 255, 200), edge: new Color(20, 70, 130, 0), intensity: beamI,
                flowSpeed: 2.6f, flowScale: 2.2f, coreSharp: 2.4f);

            //绘制主体
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation + rotationOffset,
                origin,
                Projectile.scale,
                effects,
                0
            );

            //突刺时枪尖径向泛光 (代替叠贴图光晕)
            if (CurrentStage == AttackStage.Thrust) {
                WeaponVFX.DrawRadialBloom(tipPos, 0.05f, 0.6f, new Color(150, 230, 255), 6f);
            }

            return false;
        }
    }

    /// <summary>
    /// 噬魂裂隙 - 突刺顶点处短暂留存的冷蓝灵魂裂缝 (纯视觉)：BeamGrad 竖向开合裂口 + 冲击环 + 柔光。
    /// </summary>
    public class SoulDevourerRift : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 26;
        private float RiftRotation => Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.25f, 0.4f, 0.55f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)LifeTime; // 0→1
            //开口: 先张开后闭合
            float open = MathF.Sin(life * MathHelper.Pi);
            float len = 40f * open;
            float intensity = open;

            //裂隙垂直于枪刺方向
            Vector2 perp = (RiftRotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 a = Projectile.Center - perp * len;
            Vector2 b = Projectile.Center + perp * len;

            ACMShaders.DrawBeam(a, b, halfWidth: 6f * open + 1.5f,
                core: new Color(180, 240, 255, 200), edge: new Color(25, 30, 90, 0), intensity: intensity,
                flowSpeed: 1.8f, flowScale: 3f, coreSharp: 3f);

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 6f + life * 30f, 6f, intensity * 0.8f,
                new Color(170, 235, 255), new Color(30, 80, 140));
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.9f * open, new Color(90, 170, 220) * intensity);
            return false;
        }
    }
}
