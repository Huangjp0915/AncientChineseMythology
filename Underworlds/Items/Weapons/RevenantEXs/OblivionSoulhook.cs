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
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 判官寂灭勾魂枪 - JudgesSoulhook的终极升级版
    /// 带来终极寂灭和死亡的判官之枪，极大范围的突刺攻击
    /// 特殊机制：巨大突刺范围、大量吸血、击杀时灵魂爆发对范围敌人造成毁灭伤害
    /// </summary>
    public class OblivionSoulhook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 5650;
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
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<OblivionSoulhookProjectile>();
            Item.shootSpeed = 6f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<OblivionSoulhookProjectile>()] < 1;
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

    public class OblivionSoulhookProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/OblivionSoulhook";

        private enum AttackStage { Prepare, Thrust, Retract }
        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set { Projectile.ai[0] = (float)value; Timer = 0; }
        }
        private ref float Timer => ref Projectile.ai[1];
        private ref float ThrustDistance => ref Projectile.localAI[0];
        private const float MaxThrustDistance = 60f;
        private const float BaseOffset = 6f;
        private Player Owner => Main.player[Projectile.owner];
        private float PrepareTime => 2f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 5f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 3f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

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
            Projectile.localNPCHitCooldown = 6;
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
            ThrustDistance = MathHelper.Lerp(0, -12f, Timer / PrepareTime);
            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 1.2f }, Projectile.Center);
            }
        }

        private void HandleThrust() {
            float progress = Timer / ThrustTime;
            ThrustDistance = MathHelper.SmoothStep(-12f, MaxThrustDistance, progress);
            if (Timer >= ThrustTime) { CurrentStage = AttackStage.Retract; }
        }

        private void HandleRetract() {
            float progress = Timer / RetractTime;
            ThrustDistance = MathHelper.SmoothStep(MaxThrustDistance, 0, progress);
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

            if (CurrentStage == AttackStage.Thrust) {
                for (int i = 0; i < 3; i++) {
                    Dust hook = Dust.NewDustDirect(
                        tipPos, 10, 10, DustID.GreenTorch,
                        0f, 0f, 80, default, Main.rand.NextFloat(1.5f, 2.5f)
                    );
                    hook.noGravity = true;
                    hook.velocity = -Projectile.rotation.ToRotationVector2() * 3f + Main.rand.NextVector2Circular(2f, 2f);
                }
                // 寂灭之光
                if (Main.rand.NextBool(2)) {
                    Dust oblivion = Dust.NewDustDirect(
                        tipPos + Main.rand.NextVector2Circular(12, 12), 4, 4, DustID.Wraith,
                        0f, -2.5f, 100, default, 2f
                    );
                    oblivion.noGravity = true;
                }
            }

            if (Main.rand.NextBool(2)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center, 12, 12, DustID.Shadowflame,
                    0f, 0f, 120, default, Main.rand.NextFloat(1f, 1.8f)
                );
                shadow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 大量吸血
            int healAmount = Main.rand.Next(20, 50);
            Owner.Heal(healAmount);

            target.AddBuff(BuffID.Slow, 300);
            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.Ichor, 300);

            // 勾魂效果
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 14f);
                velocity = velocity.RotatedByRandom(MathHelper.ToRadians(30));
                Dust soul = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, velocity, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                soul.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 80, default, 2f);
                burst.noGravity = true;
            }

            // 寂灭爆发：击杀时对范围所有敌人造成AOE
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
                // 寂灭爆发视觉效果
                for (int i = 0; i < 40; i++) {
                    float angle = MathHelper.TwoPi / 40f * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                    Dust ring = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                    ring.noGravity = true;
                }
                // 升级演出: 寂灭虚空 (GenericWarp void + ElementalScreenTint), 仅本机生成
                OblivionVoidFinisher.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 7f);
            }

            // 命中冲击演出 (鬼绿径向辉光 + 冲击环)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.GhostGreen, scale: 1.2f, owner: Projectile.owner);

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.4f }, target.Center);
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

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2;
            float rotationOffset;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) { rotationOffset = MathHelper.PiOver4; effects = SpriteEffects.None; }
            else { rotationOffset = MathHelper.Pi - MathHelper.PiOver4; effects = SpriteEffects.FlipHorizontally; }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            if (CurrentStage == AttackStage.Thrust) {
                // 寂灭枪身 BeamGrad 冷绿光束 (从持握点沿枪身到枪尖)
                Vector2 beamStart = Owner.MountedCenter;
                Vector2 beamEnd = Projectile.Center + Projectile.rotation.ToRotationVector2() * 50f;
                float beamI = MathHelper.Clamp(ThrustDistance / MaxThrustDistance, 0.2f, 1f);
                ACMShaders.DrawBeam(beamStart, beamEnd, 16f * beamI,
                    new Color(150, 255, 190), new Color(30, 150, 90), beamI,
                    flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.4f);

                Color glowColor = new Color(100, 255, 140) * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.15f, effects, 0);

                Texture2D lightShot = ACMAsset.LightShot;
                if (lightShot != null) {
                    Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 45f - Main.screenPosition;
                    Vector2 lsOrigin = lightShot.Size() / 2f;
                    Color tipGlow = new Color(120, 255, 180) * 0.7f;
                    tipGlow.A = 0;
                    Main.EntitySpriteDraw(lightShot, tipPos, null, tipGlow, Projectile.rotation, lsOrigin, 0.8f, SpriteEffects.None, 0);
                }

                Texture2D softGlow = ACMAsset.SoftGlow;
                if (softGlow != null) {
                    Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 45f - Main.screenPosition;
                    Vector2 sgOrigin = softGlow.Size() / 2f;
                    Color circleGlow = new Color(80, 255, 120) * 0.5f;
                    circleGlow.A = 0;
                    float pulse = 0.9f + MathF.Sin(Timer * 0.35f) * 0.15f;
                    Main.EntitySpriteDraw(softGlow, tipPos, null, circleGlow, 0f, sgOrigin, pulse, SpriteEffects.None, 0);
                }
            }
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
