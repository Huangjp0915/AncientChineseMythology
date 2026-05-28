using AncientChineseMythology;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors.Items
{
    internal static class VigorWeaponFx
    {
        public static readonly Color RuneGold = new(220, 180, 60);
        public static readonly Color RuneBlue = new(80, 140, 255);
        public static readonly Color WhiteGold = new(255, 245, 200);
        public static readonly Color IronGray = new(18, 16, 22);

        public static int SinMarkBuff => ModContent.BuffType<SinMarkDebuff>();
        public static int VerdictSealSlowBuff => ModContent.BuffType<VerdictSealSlowDebuff>();

        public static void ApplySinMark(NPC target, int duration = 480) {
            target.AddBuff(SinMarkBuff, duration);
        }

        public static void ApplyVerdictSealSlow(NPC target, int duration = 420) {
            target.AddBuff(VerdictSealSlowBuff, duration);
        }

        public static void ReleaseVerdictShockwave(IEntitySource source, Player player, Vector2 center, int baseDamage, float knockback) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int waveDamage = (int)(player.GetTotalDamage(DamageClass.Melee).ApplyTo(baseDamage) * 0.72f);
            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<VerdictSealShockwave>(), waveDamage, knockback * 0.85f, player.whoAmI);

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 vel = angle.ToRotationVector2() * 9f;
                Projectile.NewProjectile(source, center, vel,
                    ModContent.ProjectileType<VerdictSealShockwavePulse>(),
                    (int)(waveDamage * 0.55f), knockback * 0.55f, player.whoAmI);
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.95f, Pitch = -0.08f }, center);
            SpawnVerdictBurst(center, 1.45f);

            if (player.whoAmI == Main.myPlayer)
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(9, 16);
        }

        public static void ModifySinMarkCrit(NPC target, ref NPC.HitModifiers modifiers) {
            if (!target.HasBuff(SinMarkBuff)) return;
            modifiers.SetCrit();
            modifiers.CritDamage += 0.65f;
        }

        public static void SpawnVerdictBurst(Vector2 center, float scale = 1f) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 12; i++) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustPerfect(center, dustType, Main.rand.NextVector2Circular(7f * scale, 7f * scale),
                    50, default, Main.rand.NextFloat(1.6f, 2.4f) * scale);
                d.noGravity = true;
            }

            if (Main.rand.NextBool(2)) {
                Dust seal = Dust.NewDustPerfect(center, DustID.GoldCoin, Vector2.Zero, 0, RuneGold, 2.2f * scale);
                seal.noGravity = true;
                seal.fadeIn = 1.2f;
            }
        }

        public static void SpawnHoldAura(Vector2 center) {
            if (Main.netMode == NetmodeID.Server) return;

            int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.BlueTorch;
            int dust = Dust.NewDust(center, 0, 0, dustType, 0f, 0f, 90, default, Main.rand.NextFloat(1.1f, 1.8f));
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity *= 0.3f;
        }
    }

    /// <summary>罪孽标记 — 被断罪巨剑命中的敌人承受裁决暴击。</summary>
    public class SinMarkDebuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            if (Main.rand.NextBool(7))
                VigorWeaponFx.SpawnHoldAura(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.35f));
        }
    }

    /// <summary>裁决封印 — 被印锤命中的敌人移动大幅受限。</summary>
    public class VerdictSealSlowDebuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            const float maxSpeed = 2.8f;
            if (npc.velocity.Length() > maxSpeed)
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;

            npc.velocity *= 0.94f;

            if (Main.rand.NextBool(6)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(8f, npc.width * 0.35f);
                VigorWeaponFx.SpawnHoldAura(npc.Center + offset);
            }
        }
    }

    /// <summary>
    /// 断罪巨剑 — 神威 apex 近战
    /// 挥砍施加罪孽标记，对已标记目标必定暴击并追加裁决伤害
    /// </summary>
    public class SinSeveringBlade : ModItem
    {
        private int attackType;

        public override void SetDefaults() {
            Item.damage = 1180;
            Item.crit = 14;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 70;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SinSeveringBladeSwing>();
            Item.shootSpeed = 3f;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(5))
                VigorWeaponFx.SpawnHoldAura(player.Center + Main.rand.NextVector2Circular(52f, 52f));
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
            attackType = (attackType + 1) % 2;
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "SinSeverLore", "天将神威陨落后铸成的断罪巨剑"));
            tooltips.Add(new TooltipLine(Mod, "SinSeverEffect", "挥砍对敌人施加罪孽标记"));
            tooltips.Add(new TooltipLine(Mod, "SinSeverEffect2", "对已标记目标必定暴击，斩浪同样生效"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;
    }

    /// <summary>断罪巨剑挥砍 — 持握旋转，斩击中段释放断罪斩浪</summary>
    public class SinSeveringBladeSwing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;

        private const float SWING_RANGE = MathF.PI * 1.55f;
        private const float PREP_FRAC = 0.18f;
        private const float EXEC_FRAC = 0.55f;

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer => ref Projectile.ai[1];
        private ref float InitAngle => ref Projectile.ai[2];
        private ref float RawProgress => ref Projectile.localAI[0];
        private int AttackDir => (int)Projectile.ai[0] == 0 ? 1 : -1;

        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }

        private bool _cleaveFired;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.timeLeft = 10000;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float toMouse = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
            int dir = Projectile.spriteDirection * AttackDir;

            if (dir > 0) {
                toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 2.8f, MathF.PI / 5f);
                InitAngle = toMouse - SWING_RANGE * 0.55f;
            }
            else {
                if (toMouse < 0) toMouse += MathHelper.TwoPi;
                toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.78f, MathF.PI * 1.4f);
                InitAngle = toMouse + SWING_RANGE * 0.55f;
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            float totalTime = Owner.itemAnimationMax;
            float prepEnd = totalTime * PREP_FRAC;
            float execDur = totalTime * EXEC_FRAC;
            float unwindDur = totalTime * (1f - PREP_FRAC - EXEC_FRAC);

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.05f }, Owner.position);
                        CurrentStage = Stage.Execute;
                    }
                    break;

                case Stage.Execute:
                    RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(Timer / execDur, 1f));

                    if (!_cleaveFired && Timer >= execDur * 0.42f) {
                        _cleaveFired = true;
                        Vector2 cleaveDir = Owner.DirectionTo(Main.MouseWorld);
                        int cleaveDamage = (int)(Owner.GetTotalDamage(DamageClass.Melee).ApplyTo(Owner.HeldItem.damage) * 1.25f);
                        Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                            Owner.Center, cleaveDir * 18f,
                            ModContent.ProjectileType<SinSeveringCleave>(),
                            cleaveDamage,
                            Owner.HeldItem.knockBack * 0.7f, Owner.whoAmI);
                        SoundEngine.PlaySound(SoundID.Item62 with { Pitch = 0.2f, Volume = 0.9f }, Owner.position);

                        if (Owner.whoAmI == Main.myPlayer)
                            Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);
                    }

                    if (Timer >= execDur) CurrentStage = Stage.Unwind;
                    break;

                case Stage.Unwind:
                    RawProgress = MathHelper.Lerp(SWING_RANGE, SWING_RANGE * 1.04f, Math.Min(Timer / unwindDur, 1f));
                    if (Timer >= unwindDur) Projectile.Kill();
                    break;
            }

            Projectile.rotation = InitAngle + Projectile.spriteDirection * AttackDir * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            Projectile.Center = arm;
            Projectile.scale = 1.4f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;

            if (CurrentStage == Stage.Execute && Main.rand.NextBool(3))
                VigorWeaponFx.SpawnHoldAura(Projectile.Center + Projectile.rotation.ToRotationVector2() * 28f);

            Timer++;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 s = Owner.MountedCenter;
            Vector2 e = s + Projectile.rotation.ToRotationVector2()
                        * Projectile.Size.Length() * Projectile.scale * 1.1f;
            float col = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), s, e, 28f * Projectile.scale, ref col);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            VigorWeaponFx.ModifySinMarkCrit(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            VigorWeaponFx.ApplySinMark(target);
            VigorWeaponFx.SpawnVerdictBurst(target.Center);

            if (hit.Crit)
                SoundEngine.PlaySound(SoundID.Item70 with { Pitch = 0.35f, Volume = 0.75f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            int dir = Projectile.spriteDirection * AttackDir;
            float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (CurrentStage == Stage.Execute) {
                Texture2D wave = ACMAsset.GlaciateWave;
                if (wave != null) {
                    for (int i = 1; i < 14 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                        float a = (1f - i / 14f) * 0.68f;
                        float rot = Projectile.oldRot[i] + rotOff;
                        sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                            VigorWeaponFx.RuneGold * a, rot, wave.Size() * 0.5f,
                            Projectile.scale * 0.48f, SpriteEffects.None, 0);
                        sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                            VigorWeaponFx.RuneBlue * (a * 0.42f), rot + 0.08f, wave.Size() * 0.5f,
                            Projectile.scale * 0.32f, SpriteEffects.None, 0);
                    }
                }

                float pulse = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.24f);
                Texture2D sg = ACMAsset.SoftGlow;
                if (sg != null) {
                    sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                        VigorWeaponFx.WhiteGold * 0.45f * pulse, Projectile.rotation + rotOff,
                        sg.Size() * 0.5f, Projectile.scale * 2f, SpriteEffects.None, 0);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = TextureAssets.Item[ItemID.BreakerBlade].Value;
            SpriteEffects fx = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = dir > 0 ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotOff, origin, Projectile.scale, fx, 0);
            return false;
        }
    }

    /// <summary>断罪斩浪 — 挥砍中段释放的金蓝符文剑气</summary>
    public class SinSeveringCleave : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/GlaciateWave";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = 88;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.972f;

            float life = 1f - Projectile.timeLeft / 50f;
            Lighting.AddLight(Projectile.Center, VigorWeaponFx.RuneGold.ToVector3() * (0.5f * (1f - life)));

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(22f, 14f),
                    dustType, -Projectile.velocity * 0.1f, 50, default, 1.8f);
                d.noGravity = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            VigorWeaponFx.ModifySinMarkCrit(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            VigorWeaponFx.ApplySinMark(target);
            VigorWeaponFx.SpawnVerdictBurst(target.Center, 1.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Texture2D sg = ACMAsset.SoftGlow;

            float life = 1f - Projectile.timeLeft / 50f;
            float scaleX = MathHelper.Lerp(1.65f, 0.5f, ACMUtils.QuadIn(life));
            float scaleY = MathHelper.Lerp(0.58f, 0.18f, ACMUtils.QuadIn(life));
            float alpha = ACMUtils.QuadOut(1f - life) * 0.92f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (tex != null) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float trail = 1f - i / (float)Projectile.oldPos.Length;
                    Color c = Color.Lerp(VigorWeaponFx.RuneGold, VigorWeaponFx.RuneBlue, 0.35f) * (alpha * trail * 0.55f);
                    c.A = 0;
                    sb.Draw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null, c,
                        Projectile.oldRot[i], tex.Size() * 0.5f, new Vector2(scaleX, scaleY) * trail, SpriteEffects.None, 0);
                }

                Color main = Color.Lerp(VigorWeaponFx.WhiteGold, VigorWeaponFx.RuneGold, 0.4f) * alpha;
                main.A = 0;
                sb.Draw(tex, Projectile.Center - Main.screenPosition, null, main,
                    Projectile.rotation, tex.Size() * 0.5f, new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
            }

            if (sg != null) {
                Color glow = VigorWeaponFx.RuneBlue * (alpha * 0.35f);
                glow.A = 0;
                sb.Draw(sg, Projectile.Center - Main.screenPosition, null, glow,
                    Projectile.rotation, sg.Size() * 0.5f, new Vector2(scaleX * 1.4f, scaleY * 2.2f), SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>
    /// 辉金虚空斩裂刃 — 神威 apex 高速清图近战
    /// 每次挥砍释放三道穿透型金紫虚空斩浪
    /// </summary>
    public class AureateVoidrender : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1120;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item71;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AureateVoidrenderSwing>();
            Item.shootSpeed = 20f;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Excalibur;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 spawn = player.Center + direction * 24f;

            Projectile.NewProjectile(source, spawn, direction * Item.shootSpeed, type, damage, knockback, player.whoAmI);
            Projectile.NewProjectile(source, spawn, direction.RotatedBy(0.12f) * Item.shootSpeed, type, (int)(damage * 0.9f), knockback, player.whoAmI, ai0: 1);
            Projectile.NewProjectile(source, spawn, direction.RotatedBy(-0.12f) * Item.shootSpeed, type, (int)(damage * 0.9f), knockback, player.whoAmI, ai0: 1);

            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (!Main.rand.NextBool(2)) return;

            Vector2 dustPos = new(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
            int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
            Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 80, default, 1.4f);
            d.noGravity = true;
            d.velocity = player.velocity * 0.2f + Main.rand.NextVector2Circular(2f, 2f);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "VoidrenderLore", "神威裁决后凝成的金装虚空刃"));
            tooltips.Add(new TooltipLine(Mod, "VoidrenderEffect", "高速挥砍释放三道穿透型金紫虚空斩浪"));
        }
    }

    /// <summary>辉金虚空斩浪 — 穿透多个敌人的金紫虚空剑气</summary>
    public class AureateVoidrenderSwing : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/GlaciateWave";

        private static readonly Color PurpleVoid = new(120, 70, 200);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 48;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            float life = 1f - Projectile.timeLeft / 48f;
            Lighting.AddLight(Projectile.Center,
                Color.Lerp(VigorWeaponFx.RuneGold, PurpleVoid, 0.45f).ToVector3() * (0.5f * (1f - life)));

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + perp * Main.rand.NextFloat(-16f, 16f),
                    dustType, -Projectile.velocity * 0.08f, 60, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 5; i++) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                Dust d = Dust.NewDustPerfect(target.Center, dustType, Main.rand.NextVector2Circular(4f, 4f), 50, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(0f, tex.Height / 2f);
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);

            float life = 1f - Projectile.timeLeft / 48f;
            float scaleX = MathHelper.Lerp(1.55f, 0.55f, ACMUtils.QuadIn(life));
            float scaleY = MathHelper.Lerp(0.52f, 0.16f, ACMUtils.QuadIn(life));
            float alpha = ACMUtils.QuadOut(1f - life) * 0.9f;
            int layers = (int)Projectile.ai[0] == 1 ? 3 : 4;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                for (int layer = -layers; layer <= layers; layer++) {
                    float layerAlpha = (1f - MathF.Abs(layer) / (layers + 1f)) * progress * alpha * 0.42f;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f + perp * layer * 7f * progress - Main.screenPosition;
                    Color trailColor = Color.Lerp(VigorWeaponFx.RuneGold, PurpleVoid, progress) * layerAlpha;
                    trailColor.A = 0;
                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                        new Vector2(scaleX * (0.9f + progress * 0.25f), scaleY), SpriteEffects.None, 0);
                }
            }

            for (int layer = -2; layer <= 2; layer++) {
                float layerAlpha = (1f - MathF.Abs(layer) / 3f) * alpha * 0.72f;
                Vector2 drawPos = Projectile.Center + perp * layer * 9f - Main.screenPosition;
                Color waveColor = Color.Lerp(VigorWeaponFx.WhiteGold, PurpleVoid, 0.35f) * layerAlpha;
                waveColor.A = 0;
                sb.Draw(tex, drawPos, null, waveColor, Projectile.rotation, origin,
                    new Vector2(scaleX * 1.5f, scaleY * 0.95f), SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>
    /// 裁决印锤 — 神威 apex 重锤爆发
    /// 命中释放全场裁决震波，并施加封印缓速
    /// </summary>
    public class VerdictSealHammer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1250;
            Item.crit = 8;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 10f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.DD2_MonkStaffGroundImpact;
            Item.autoReuse = true;
            Item.scale = 1.15f;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(6))
                VigorWeaponFx.SpawnHoldAura(player.Center + Main.rand.NextVector2Circular(48f, 48f));
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (!Main.rand.NextBool(2))
                return;

            Vector2 dustPos = new(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
            int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
            Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 90, default, Main.rand.NextFloat(1.3f, 2f));
            d.noGravity = true;
            d.velocity = player.velocity * 0.15f + Main.rand.NextVector2Circular(2.5f, 2.5f);
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            VigorWeaponFx.ApplyVerdictSealSlow(target);
            VigorWeaponFx.ReleaseVerdictShockwave(player.GetSource_OnHit(target), player, target.Center, Item.damage, Item.knockBack);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "VerdictSealLore", "神威陨落后凝成的裁决重锤"));
            tooltips.Add(new TooltipLine(Mod, "VerdictSealEffect", "命中释放全场裁决震波"));
            tooltips.Add(new TooltipLine(Mod, "VerdictSealEffect2", "震波与重击对敌人施加封印缓速"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PaladinsHammer;
    }

    /// <summary>裁决震波 — 以命中点为中心向外扩散的全场环形冲击。</summary>
    public class VerdictSealShockwave : ModProjectile
    {
        private ref float WaveAge => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 26;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            WaveAge++;
            Projectile.velocity = Vector2.Zero;

            float progress = WaveAge / 26f;
            Lighting.AddLight(Projectile.Center,
                Color.Lerp(VigorWeaponFx.RuneGold, VigorWeaponFx.RuneBlue, progress).ToVector3() * (0.55f * (1f - progress * 0.35f)));

            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 24f + WaveAge * 14f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(pos, 0, 0, dustType, 0, 0, 70, default, Main.rand.NextFloat(1.2f, 1.9f));
                d.noGravity = true;
                d.velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.5f, 3.5f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            VigorWeaponFx.ApplyVerdictSealSlow(target);
            VigorWeaponFx.SpawnVerdictBurst(target.Center, 0.95f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float innerRadius = 18f + WaveAge * 11f;
            float outerRadius = innerRadius + 34f;
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center));
            return dist >= innerRadius && dist <= outerRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[ProjectileID.DD2PhoenixBowShot].Value;
            float progress = WaveAge / 26f;
            float scale = 0.45f + WaveAge * 0.16f;
            Color drawColor = Color.Lerp(VigorWeaponFx.RuneGold, VigorWeaponFx.RuneBlue, progress);
            drawColor *= 0.72f * (1f - progress);
            drawColor.A = 0;

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null,
                drawColor, WaveAge * 0.08f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>裁决震波脉冲 — 八向扩散的符文冲击段。</summary>
    public class VerdictSealShockwavePulse : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.93f;

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(10, 6),
                    0, 0, dustType, 0, 0, 80, default, 1.4f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.12f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            VigorWeaponFx.ApplyVerdictSealSlow(target, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[ProjectileID.DD2PhoenixBowShot].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float alpha = Projectile.timeLeft / 22f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float trail = 1f - i / (float)Projectile.oldPos.Length;
                Color c = Color.Lerp(VigorWeaponFx.RuneGold, VigorWeaponFx.RuneBlue, trail) * (alpha * trail * 0.45f);
                c.A = 0;
                Main.spriteBatch.Draw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null, c,
                    Projectile.oldRot[i], origin, 0.55f * trail, SpriteEffects.None, 0);
            }

            Color main = Color.Lerp(VigorWeaponFx.WhiteGold, VigorWeaponFx.RuneBlue, 0.35f) * (alpha * 0.75f);
            main.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, main,
                Projectile.rotation, origin, 0.65f, SpriteEffects.None, 0);
            return false;
        }
    }
}
