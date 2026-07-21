using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.Revenants;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 判官寂灭勾魂枪 - JudgesSoulhook的觉醒升级版
    /// 手持三段突刺 (后拖蓄力 → poly8 急刺 130px → 弹性回收), 第三刺为"勾魂大刺"
    /// (1.35×, 170px, 勾魂治疗加倍); 击杀触发寂灭爆发。觉醒形态: 全段提速 30%,
    /// 每刺满伸展时放出勾魂波。
    /// </summary>
    public class OblivionSoulhook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 6400; // 5650 → 6400: 突刺周期 10f→17f 拉长, 折算 DPS +4% 内 (见设计文档 §6)
            Item.crit = 18;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 10f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = null; // 音效由手持弹幕分层播放
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<OblivionSoulhookProjectile>();
            Item.shootSpeed = 6f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<OblivionSoulhookProjectile>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var mp = player.GetModPlayer<RevenantEXKarmaPlayer>();
            bool big = mp.HookCombo >= 2; // 第三刺 = 勾魂大刺
            mp.HookCombo = big ? 0 : mp.HookCombo + 1;
            mp.HookComboTimer = 55; // 55f 内接续连段
            Projectile.NewProjectile(source, player.MountedCenter, velocity, type, damage, knockback,
                player.whoAmI, 0f, 0f, big ? 1f : 0f);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<JudgesSoulhook>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 寂灭勾魂枪手持突刺弹幕: Prepare 后拖 26px (pow3 late-snap) → Thrust poly(8) 急刺 →
    /// Retract 弹性回收带过冲。ai[2]=1 为勾魂大刺 (1.35×/170px/治疗加倍)。
    /// </summary>
    public class OblivionSoulhookProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/OblivionSoulhook";

        private enum AttackStage { Prepare, Thrust, Retract }
        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set { Projectile.ai[0] = (float)value; Timer = 0; }
        }
        private ref float Timer => ref Projectile.ai[1];
        private bool BigThrust => Projectile.ai[2] >= 1f;
        private ref float ThrustDistance => ref Projectile.localAI[0];
        private ref float WaveFired => ref Projectile.localAI[1];

        private const float BackswingDist = 26f;
        private const float BaseOffset = 6f;
        private Player Owner => Main.player[Projectile.owner];
        private float MaxThrustDistance => BigThrust ? 170f : 130f;

        // 觉醒形态: 全段提速 30%
        private float SpeedMul => Owner.HasBuff<KarmaAwakenBuff>() ? 0.7f : 1f;
        private float PrepareTime => 6f * SpeedMul / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 5f * SpeedMul / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 6f * SpeedMul / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) { Projectile.Kill(); return; }
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            switch (CurrentStage) {
                case AttackStage.Prepare: HandlePrepare(); break;
                case AttackStage.Thrust: HandleThrust(); break;
                case AttackStage.Retract: HandleRetract(); break;
            }
            UpdatePositionAndRotation();
            SpawnOblivionParticles();
            Lighting.AddLight(Projectile.Center, 0.4f, 1.2f, 0.6f);
            Timer++;
        }

        private void HandlePrepare() {
            // late-snap 后拖: 前段几乎不动, 尾段猛然吸回 (pow3) — 读作"吸气"
            float t = MathHelper.Clamp(Timer / PrepareTime, 0f, 1f);
            ThrustDistance = -BackswingDist * t * t * t;
            // 大刺蓄力抖动
            if (BigThrust)
                ThrustDistance += MathF.Sin(Timer * 2.2f) * 1.5f * t;
            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                // 爆发帧: 低频冲击 + 高频破空
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = BigThrust ? -0.1f : 0.2f, Volume = 1.2f }, Projectile.Center);
                if (BigThrust)
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.8f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Owner.Center, BigThrust ? 3f : 1.5f);
            }
        }

        private void HandleThrust() {
            // poly(8) 急速击出: 首帧掠过几乎全程 — 拳拳到肉
            float t = MathHelper.Clamp(Timer / ThrustTime, 0f, 1f);
            float e = 1f - MathF.Pow(1f - t, 8f);
            ThrustDistance = MathHelper.Lerp(-BackswingDist, MaxThrustDistance, e);

            // 觉醒形态: 满伸展瞬间放出勾魂波 (每刺一次)
            if (WaveFired == 0f && e > 0.9f && Owner.HasBuff<KarmaAwakenBuff>()) {
                WaveFired = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 dir = Projectile.rotation.ToRotationVector2();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Owner.MountedCenter + dir * (BaseOffset + ThrustDistance + 60f), dir * 15f,
                        ModContent.ProjectileType<OblivionSoulWave>(),
                        (int)(Projectile.damage * 0.6f), Projectile.knockBack * 0.4f, Projectile.owner);
                }
            }
            if (Timer >= ThrustTime) { CurrentStage = AttackStage.Retract; }
        }

        private void HandleRetract() {
            // 弹性回收: 先过冲到 -10 再归零 (收招的"回弹"重量)
            float t = MathHelper.Clamp(Timer / RetractTime, 0f, 1f);
            if (t < 0.6f)
                ThrustDistance = MathHelper.SmoothStep(MaxThrustDistance, -10f, t / 0.6f);
            else
                ThrustDistance = MathHelper.SmoothStep(-10f, 0f, (t - 0.6f) / 0.4f);
            if (Timer >= RetractTime) { Projectile.Kill(); }
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

        private void SpawnOblivionParticles() {
            Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 45f;

            if (CurrentStage == AttackStage.Prepare) {
                // 蓄力: 魂气向枪尖收束 (converging)
                if (Main.rand.NextBool(2)) {
                    Vector2 pos = tipPos + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(40f, 110f);
                    Dust pull = Dust.NewDustPerfect(pos, DustID.GreenTorch,
                        (tipPos - pos) * 0.09f, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                    pull.noGravity = true;
                }
            }
            else if (CurrentStage == AttackStage.Thrust) {
                for (int i = 0; i < 3; i++) {
                    Dust hook = Dust.NewDustDirect(tipPos, 10, 10,
                        BigThrust ? DustID.Torch : DustID.GreenTorch,
                        0f, 0f, 80, default, Main.rand.NextFloat(1.5f, 2.5f));
                    hook.noGravity = true;
                    hook.velocity = -Projectile.rotation.ToRotationVector2() * 3f + Main.rand.NextVector2Circular(2f, 2f);
                }
                if (Main.rand.NextBool(2)) {
                    Dust oblivion = Dust.NewDustDirect(
                        tipPos + Main.rand.NextVector2Circular(12, 12), 4, 4, DustID.Wraith,
                        0f, -2.5f, 100, default, 2f);
                    oblivion.noGravity = true;
                }
            }

            if (Main.rand.NextBool(2)) {
                Dust shadow = Dust.NewDustDirect(Projectile.Center, 12, 12, DustID.Shadowflame,
                    0f, 0f, 120, default, Main.rand.NextFloat(1f, 1.8f));
                shadow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 勾魂吸血 (大刺加倍)
            int healAmount = BigThrust ? 24 : 12;
            Owner.Heal(healAmount);
            if (Projectile.owner == Main.myPlayer)
                Owner.GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(BigThrust ? 5f : 3f);

            target.AddBuff(BuffID.Slow, 300);
            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.Ichor, 300);

            // 勾魂效果: 魂流飞向玩家
            int soulCount = BigThrust ? 24 : 14;
            for (int i = 0; i < soulCount; i++) {
                Vector2 velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 14f);
                velocity = velocity.RotatedByRandom(MathHelper.ToRadians(30));
                Dust soul = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, velocity, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                soul.noGravity = true;
            }

            // 寂灭爆发: 击杀时对范围所有敌人造成AOE
            if (target.life <= 0) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.4f }, target.Center);
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 500f) {
                        nearby.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.ShadowFlame, 300);
                    }
                }
                for (int i = 0; i < 30; i++) {
                    float angle = MathHelper.TwoPi / 30f * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                    Dust ring = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                    ring.noGravity = true;
                }
                // 升级演出: 寂灭虚空 (GenericWarp void + ElementalScreenTint), 仅本机生成
                OblivionVoidFinisher.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 7f);
            }

            // 命中冲击演出 (鬼绿径向辉光 + 冲击环; 大刺升格)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GhostGreen, scale: BigThrust ? 1.6f : 1.2f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, BigThrust ? 4f : 2f);

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.4f + Main.rand.NextFloat(-0.1f, 0.1f) }, target.Center);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (BigThrust)
                modifiers.FinalDamage *= 1.35f;
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 75f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 30f, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 75f);
            Utils.PlotTileLine(start, end, 30f, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() { return CurrentStage == AttackStage.Thrust; }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2;
            float rotationOffset;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) { rotationOffset = MathHelper.PiOver4; effects = SpriteEffects.None; }
            else { rotationOffset = MathHelper.Pi - MathHelper.PiOver4; effects = SpriteEffects.FlipHorizontally; }

            // 突刺段残影 (速度门控)
            if (CurrentStage == AttackStage.Thrust) {
                for (int g = 1; g <= 2; g++) {
                    Vector2 ghostPos = Projectile.Center - Projectile.rotation.ToRotationVector2() * g * 22f - Main.screenPosition;
                    Color ghost = (BigThrust ? new Color(255, 170, 90) : new Color(100, 255, 140)) * (0.30f - g * 0.11f);
                    ghost.A = 0;
                    Main.EntitySpriteDraw(texture, ghostPos, null, ghost, Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);
                }
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            if (CurrentStage == AttackStage.Thrust) {
                // 寂灭枪身 BeamGrad 光束 (大刺换致命暖色)
                Vector2 beamStart = Owner.MountedCenter;
                Vector2 beamEnd = Projectile.Center + Projectile.rotation.ToRotationVector2() * 50f;
                float beamI = MathHelper.Clamp((ThrustDistance + BackswingDist) / (MaxThrustDistance + BackswingDist), 0.2f, 1f);
                Color beamCore = BigThrust ? new Color(255, 210, 150) : new Color(150, 255, 190);
                Color beamEdge = BigThrust ? new Color(200, 90, 30) : new Color(30, 150, 90);
                ACMShaders.DrawBeam(beamStart, beamEnd, (BigThrust ? 22f : 16f) * beamI,
                    beamCore, beamEdge, beamI, flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.4f);

                Color glowColor = (BigThrust ? new Color(255, 190, 110) : new Color(100, 255, 140)) * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.15f, effects, 0);

                Texture2D lightShot = ACMAsset.LightShot;
                if (lightShot != null) {
                    Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 45f - Main.screenPosition;
                    Vector2 lsOrigin = lightShot.Size() / 2f;
                    Color tipGlow = (BigThrust ? new Color(255, 200, 120) : new Color(120, 255, 180)) * 0.7f;
                    tipGlow.A = 0;
                    Main.EntitySpriteDraw(lightShot, tipPos, null, tipGlow, Projectile.rotation, lsOrigin, BigThrust ? 1.05f : 0.8f, SpriteEffects.None, 0);
                }
            }
            else if (CurrentStage == AttackStage.Prepare) {
                // 蓄力尖端聚魂光点 (随蓄力增大)
                float t = MathHelper.Clamp(Timer / PrepareTime, 0f, 1f);
                Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 45f;
                WeaponVFX.DrawGlowBurst(tipPos, 0.3f + t * t * 0.7f, (BigThrust ? new Color(255, 190, 110) : new Color(90, 255, 130)) * (0.35f + t * 0.3f));
            }
            return false;
        }
    }

    /// <summary>觉醒形态·勾魂波: 满伸展瞬间放出的短程穿透魂波 (0.6×)。</summary>
    public class OblivionSoulWave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.OblivionSoulWave.DisplayName",
                () => "Oblivion Soul Wave");
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 24;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.97f;
            Lighting.AddLight(Projectile.Center, 0.3f, 0.9f, 0.5f);
            if (Main.rand.NextBool(2)) {
                Dust soul = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.GreenTorch, -Projectile.velocity * 0.15f, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                soul.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 180);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GhostGreen, scale: 0.9f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Projectile.timeLeft / 24f;
            // 新月魂波: 垂直于飞行方向的短弧 (BeamGrad)
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            ACMShaders.DrawBeam(Projectile.Center - perp * 42f, Projectile.Center + perp * 42f, 12f * fade,
                new Color(170, 255, 200), new Color(20, 140, 80), fade,
                flowSpeed: 2.4f, flowScale: 2f, coreSharp: 2.4f);
            WeaponVFX.DrawProjectileTrail(Projectile, 12f,
                new Color(20, 120, 70), new Color(170, 255, 200), uvScroll: Timer * 0.05f);
            return false;
        }
    }

    /// <summary>
    /// 寂灭虚空演出弹幕 (纯视觉, damage=0): 击杀寂灭爆发瞬间在敌群中心展开 GenericWarp 虚空吸入扭曲
    /// + 短促 ElementalScreenTint 鬼绿染屏 (≤0.15) + 冲击环。全屏后处理走单一名额; 绘制只在 PreDraw。
    /// </summary>
    public class OblivionVoidFinisher : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 40;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<OblivionVoidFinisher>(), 0, 0f, owner);
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(life < 0.18f ? life / 0.18f : 1f - (life - 0.18f) / 0.82f, 0f, 1f);
            SpriteBatch sb = Main.spriteBatch;

            // —— 冲击环 (鬼绿双环, 扩张) ——
            WeaponVFX.DrawShockwaveRing(Projectile.Center, 18f + life * 240f, 16f, fade * 0.9f,
                new Color(160, 255, 180), new Color(30, 130, 80));

            // —— GenericWarp 虚空吸入扭曲 (占单一全屏名额) ——
            Effect warp = ACMShaders.GenericWarp;
            if (warp != null && fade > 0.05f && ACMShaders.RequestFullscreenSlot()) {
                ACMShaders.SetCommonParams(warp, Projectile.Center, fade);
                warp.Parameters["uRadius"]?.SetValue(0.6f);
                warp.Parameters["uWarpScale"]?.SetValue(1.4f);
                warp.Parameters["uChroma"]?.SetValue(0.5f);
                warp.Parameters["uRadialPull"]?.SetValue(0.7f); // 向心吸入(黑洞感)
                warp.Parameters["uMode"]?.SetValue(4f);          // void
                warp.Parameters["uTint"]?.SetValue(new Vector4(0.16f, 0.4f, 0.26f, 0.7f));
                ACMShaders.ApplyScreenPostProcess(sb, warp, bindNoise: true);
            }

            // —— ElementalScreenTint 鬼绿染屏 (短促, ≤0.15, 程序化 overlay) ——
            Effect tint = ACMShaders.ElementalScreenTint;
            if (tint != null && fade > 0.05f) {
                tint.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                tint.Parameters["uIntensity"]?.SetValue(fade * 0.14f);
                tint.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
                tint.Parameters["uTint"]?.SetValue(new Vector4(0.12f, 0.45f, 0.28f, 0.85f));
                tint.Parameters["uTint2"]?.SetValue(new Vector4(0.03f, 0.12f, 0.08f, 1f));
                tint.Parameters["uVignette"]?.SetValue(0.5f);
                tint.Parameters["uFogScale"]?.SetValue(2.6f);
                sb.End();
                ACMShaders.DrawFullscreenOverlay(tint, BlendState.AlphaBlend);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            return false;
        }
    }
}
