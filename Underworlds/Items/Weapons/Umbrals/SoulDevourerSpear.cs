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

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 噬魂枪 - 吞噬亡魂的地府长枪，手持突刺弹幕。
    /// 重做"喂魂大刺"：命中收集魂灵（枪杆环绕 wisp 可见，上限 4），满魂下一刺自动变为
    /// 吞魂突刺 —— 距离 ×1.9、1.6 倍伤害、命中吞魂回 12 HP。随机回血改为确定性资源循环。
    /// 三段突刺曲线锐化：late-snap 后拉前摇 → poly(8) 爆发 → 五次方收招。
    /// </summary>
    public class SoulDevourerSpear : ModItem
    {
        /// <summary>已喂魂数（0~4, owner 端资源; 大刺标记随弹幕 ai 同步）。</summary>
        internal int souls;
        public const int SoulsMax = 4;

        public override void SetDefaults() {
            Item.damage = 128; //梯队离群修复: 原 152 高出本队均值 2.5 倍 (论证见 Docs/WeaponRedo/Umbrals.md §6)
            Item.crit = 5;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<SoulDevourerSpearProjectile>();
            Item.shootSpeed = 3.5f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<SoulDevourerSpearProjectile>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool devour = souls >= SoulsMax;
            if (devour) {
                souls = 0;
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.3f }, player.Center);
            }
            Projectile.NewProjectile(source, position, velocity, type,
                devour ? (int)(damage * 1.6f) : damage, knockback, player.whoAmI, 0f, 0f, devour ? 1f : 0f);
            return false;
        }

        public override void HoldItem(Player player) {
            //魂灵 wisp 环绕玩家 (资源可见广播; 满魂更亮更急)
            if (souls <= 0 || Main.dedServ)
                return;
            bool full = souls >= SoulsMax;
            if (Main.rand.NextBool(full ? 2 : 4)) {
                float angle = Main.GlobalTimeWrappedHourly * (full ? 4f : 2.2f) + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 orbitPos = player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(26f, 40f);
                Dust d = Dust.NewDustPerfect(orbitPos, DustID.Wraith, angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f,
                    full ? 60 : 110, default, full ? 1.4f : 1f);
                d.noGravity = true;
            }
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 噬魂枪弹幕 - 手持突刺；ai[2]=1 为吞魂突刺（距离 ×1.9, 命中吞魂回血）。
    /// 曲线: 前摇 pow4 late-snap 后拉 → 爆发 poly(8) → 收招五次方。
    /// </summary>
    public class SoulDevourerSpearProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Umbrals/SoulDevourerSpear";

        private enum AttackStage { Prepare, Thrust, Retract }

        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set {
                Projectile.ai[0] = (float)value;
                Timer = 0;
            }
        }

        private ref float Timer => ref Projectile.ai[1];
        private bool Devour => Projectile.ai[2] >= 1f;
        private ref float ThrustDistance => ref Projectile.localAI[0];

        private float MaxThrustDistance => Devour ? 50f : 26f;
        private const float BaseOffset = 4f;
        private const float PullBack = -14f;

        private Player Owner => Main.player[Projectile.owner];

        private float PrepareTime => (Devour ? 10f : 8f) / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 5f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 8f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

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

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

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
            SpawnSoulParticles();

            Lighting.AddLight(Projectile.Center, Devour ? 0.5f : 0.3f, Devour ? 0.7f : 0.4f, Devour ? 0.9f : 0.6f);

            Timer++;
        }

        private void HandlePrepare() {
            //late-snap 后拉: pow4 — 大部分时间近乎不动, 最后几帧猛然吸气后撤 (MOTION.md §2)
            float t = MathHelper.Clamp(Timer / PrepareTime, 0f, 1f);
            ThrustDistance = MathF.Pow(t, 4f) * PullBack;

            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
                if (Devour) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.4f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Owner.Center, 3.5f);
                }
            }
        }

        private void HandleThrust() {
            //爆发: poly(8) ease-out — 几乎全部行程在头两帧, "刺"的一瞬
            float progress = MathHelper.Clamp(Timer / ThrustTime, 0f, 1f);
            float e = 1f - MathF.Pow(1f - progress, 8f);
            ThrustDistance = MathHelper.Lerp(PullBack, MaxThrustDistance, e);

            //突刺顶点: 噬魂裂隙 (纯视觉, 一次)
            if (progress >= 0.8f && Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() * (Devour ? 60f : 44f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), tip, Vector2.Zero,
                        ModContent.ProjectileType<SoulDevourerRift>(), 0, 0f, Projectile.owner,
                        Projectile.rotation, Devour ? 1f : 0f);
                }
            }

            if (Timer >= ThrustTime) {
                CurrentStage = AttackStage.Retract;
            }
        }

        private void HandleRetract() {
            //收招: 五次方 in-out settle
            float progress = MathHelper.Clamp(Timer / RetractTime, 0f, 1f);
            float r = progress < 0.5f ? 16f * MathF.Pow(progress, 5f) : 1f - MathF.Pow(-2f * progress + 2f, 5f) / 2f;
            ThrustDistance = MathHelper.Lerp(MaxThrustDistance, 0f, r);

            if (Timer >= RetractTime) {
                Projectile.Kill();
            }
        }

        private void UpdatePositionAndRotation() {
            Vector2 direction = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation();

            Projectile.spriteDirection = direction.X > 0 ? 1 : -1;
            Owner.direction = Projectile.spriteDirection;

            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            Vector2 handPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);
            handPosition.Y += Owner.gfxOffY;

            Projectile.Center = handPosition + direction * (BaseOffset + ThrustDistance);
        }

        private void SpawnSoulParticles() {
            //突刺时枪尖幽灵粒子 (吞魂突刺加倍)
            if (CurrentStage == AttackStage.Thrust && Main.rand.NextBool(Devour ? 1 : 2)) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center + Projectile.rotation.ToRotationVector2() * 30f, 8, 8,
                    DustID.Wraith, 0f, 0f, 100, default, Main.rand.NextFloat(1.0f, Devour ? 1.9f : 1.5f));
                soul.noGravity = true;
                soul.velocity = -Projectile.rotation.ToRotationVector2() * 2f;
            }

            if (Main.rand.NextBool(4)) {
                Dust shadow = Dust.NewDustDirect(Projectile.Center, 10, 10, DustID.Shadowflame,
                    0f, 0f, 150, default, Main.rand.NextFloat(0.8f, 1.2f));
                shadow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 120);

            if (Devour) {
                //—— 吞魂: 确定性回血 + 魂灵倒吸演出 ——
                Owner.Heal(12);
                for (int i = 0; i < 10; i++) {
                    Vector2 velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 7f);
                    velocity = velocity.RotatedByRandom(MathHelper.ToRadians(25));
                    Dust soul = Dust.NewDustDirect(target.Center, 4, 4, DustID.Wraith,
                        velocity.X, velocity.Y, 80, default, 1.6f);
                    soul.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.55f, Pitch = 0.4f }, target.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.NetherGrudge, scale: 1.5f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 4.5f);
            }
            else {
                //—— 喂魂: +1 魂灵 (音高随存魂上行 — 资源可听) ——
                if (Owner.HeldItem?.ModItem is SoulDevourerSpear spear && spear.souls < SoulDevourerSpear.SoulsMax) {
                    spear.souls++;
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.35f, Pitch = 0.15f + spear.souls * 0.12f }, target.Center);
                }
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.NetherGrudge, scale: 0.9f, owner: Projectile.owner);
            }

            for (int i = 0; i < 3; i++) {
                Dust burst = Dust.NewDustDirect(target.Center, 10, 10, DustID.Shadowflame,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.5f);
                burst.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + (Devour ? 66f : 50f));
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 18f, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 50f);
            Utils.PlotTileLine(start, end, 18f, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() {
            //只在突刺阶段造成伤害 (判定与爆发段严格对齐)
            return CurrentStage == AttackStage.Thrust;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
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

            //冷蓝魂火枪身光束 (突刺更强, 吞魂突刺最盛)
            Vector2 shaftDir = Projectile.rotation.ToRotationVector2();
            Vector2 tipPos = Projectile.Center + shaftDir * 46f;
            Vector2 tailPos = Projectile.Center - shaftDir * 22f;
            float beamI = CurrentStage == AttackStage.Thrust ? 1f : 0.55f;
            ACMShaders.DrawBeam(tailPos, tipPos, halfWidth: Devour ? 13f : 9f,
                core: new Color(150, 230, 255, 200), edge: new Color(20, 70, 130, 0), intensity: beamI,
                flowSpeed: 2.6f, flowScale: 2.2f, coreSharp: 2.4f);

            //枪杆环绕魂灵 wisp (存魂数可见 — 资源广播)
            if (Owner.HeldItem?.ModItem is SoulDevourerSpear spear && spear.souls > 0 && !Devour) {
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    for (int i = 0; i < spear.souls; i++) {
                        float phase = Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / SoulDevourerSpear.SoulsMax;
                        float along = 0.25f + 0.5f * (0.5f + 0.5f * MathF.Sin(phase * 0.7f + i));
                        Vector2 basePos = Vector2.Lerp(tailPos, tipPos, along);
                        Vector2 orbit = new Vector2(0f, MathF.Sin(phase) * 10f).RotatedBy(Projectile.rotation);
                        bool full = spear.souls >= SoulDevourerSpear.SoulsMax;
                        Color wispColor = full ? new Color(170, 245, 255, 0) : new Color(110, 190, 230, 0);
                        Main.spriteBatch.Draw(glowTex, basePos + orbit - Main.screenPosition, null,
                            wispColor * (full ? 0.9f : 0.65f), 0f, glowTex.Size() * 0.5f,
                            full ? 0.34f : 0.24f, SpriteEffects.None, 0f);
                    }
                }
            }

            //绘制主体 (吞魂突刺放大 + 冷蓝染色)
            Color bodyColor = Devour ? Color.Lerp(lightColor, new Color(150, 220, 255), 0.4f) : lightColor;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, bodyColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale * (Devour ? 1.25f : 1f), effects, 0);

            //突刺时枪尖径向泛光
            if (CurrentStage == AttackStage.Thrust) {
                WeaponVFX.DrawRadialBloom(tipPos, Devour ? 0.08f : 0.05f, Devour ? 0.85f : 0.6f,
                    new Color(150, 230, 255), 6f);
            }

            return false;
        }
    }

    /// <summary>
    /// 噬魂裂隙 - 突刺顶点处短暂留存的冷蓝灵魂裂缝 (纯视觉)：BeamGrad 竖向开合裂口 + 冲击环 + 柔光。
    /// ai[1]=1 为吞魂突刺加强版。
    /// </summary>
    public class SoulDevourerRift : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 26;
        private float RiftRotation => Projectile.ai[0];
        private bool Empowered => Projectile.ai[1] >= 1f;

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
            float open = MathF.Sin(life * MathHelper.Pi);
            float scaleMul = Empowered ? 1.6f : 1f;
            float len = 40f * open * scaleMul;
            float intensity = open;

            //裂隙垂直于枪刺方向
            Vector2 perp = (RiftRotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 a = Projectile.Center - perp * len;
            Vector2 b = Projectile.Center + perp * len;

            ACMShaders.DrawBeam(a, b, halfWidth: (6f * open + 1.5f) * scaleMul,
                core: new Color(180, 240, 255, 200), edge: new Color(25, 30, 90, 0), intensity: intensity,
                flowSpeed: 1.8f, flowScale: 3f, coreSharp: 3f);

            WeaponVFX.DrawShockwaveRing(Projectile.Center, (6f + life * 30f) * scaleMul, 6f, intensity * 0.8f,
                new Color(170, 235, 255), new Color(30, 80, 140));
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.9f * open * scaleMul, new Color(90, 170, 220) * intensity);
            return false;
        }
    }
}
