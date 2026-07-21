using AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    /// <summary>
    /// 昊天巨阙 - 天柱敌怪掉落的巨剑类近战武器 (系列旗舰之一)。
    /// 机制身份: 断穹 — 手持三连段: 正手横斩→反手横斩→过顶断穹斩。
    /// 断穹斩在落点劈开一道竖直"苍穹裂隙"(专属 PillarSkyRift 着色器), 短暂金青染屏定调。
    /// 决策点: 连段节奏管理; 断穹斩 18 帧前摇是主动承担的硬直。
    /// </summary>
    public class FirmamentCleaver : ModItem
    {
        private int comboStep;      // 连段计数 (Shoot 仅 owner 端调用, 实例字段安全)
        private uint lastSwingTime; // 连段窗口计时

        public override void SetDefaults() {
            Item.damage = 245;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null; // 挥砍音由手持弹幕按段播放
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<FirmamentSwing>();
            Item.shootSpeed = 10f;
            Item.crit = 14;
            Item.scale = 1.3f;
        }

        public override bool CanUseItem(Player player) {
            // 上一段挥舞进行中不可抢段 (节奏由手持弹幕生命周期控制)
            return player.ownedProjectileCounts[ModContent.ProjectileType<FirmamentSwing>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 2.5s 未续段则重置回第一段
            if (Main.GameUpdateCount - lastSwingTime > 150)
                comboStep = 0;
            lastSwingTime = Main.GameUpdateCount;

            int step = comboStep;
            comboStep = (comboStep + 1) % 3;

            float dmgMul = step == 2 ? 1.55f : 1f;
            float swingSign = step == 1 ? -1f : 1f; // 第二段反手
            Projectile.NewProjectile(source, player.MountedCenter, velocity, type,
                (int)(damage * dmgMul), knockback, player.whoAmI, step, swingSign);

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "蕴含苍穹之力的神圣巨剑"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "三段连击：两记横斩释放天柱剑气"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect2", "第三段过顶断穹斩，在落点劈开一道贯天裂隙"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(15).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 昊天巨阙手持挥舞弹幕。ai[0]=连段 (0/1 横斩 26f, 2 断穹斩 44f), ai[1]=横斩正反手。
    /// 波形: 前摇 42% (反向蓄势) → 爆发 14% (poly(16/20) 陡峭缓出) → 收招 44% (回弹沉降)。
    /// </summary>
    public class FirmamentSwing : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Celestias/PillarofTheHeavenes/Items/FirmamentCleaver";

        private int Step => (int)Projectile.ai[0];
        private float SwingSign => Projectile.ai[1] >= 0f ? 1f : -1f;
        private bool Slam => Step >= 2;
        private int Duration => Slam ? 44 : 26;

        private ref float Timer => ref Projectile.localAI[0];

        private const float TipLen = 150f;

        private float baseAngle;   // 瞄准方向 (生成帧锁定)
        private int facing = 1;
        private float bladeAngle;  // 当前刀身指向
        private float burstGlow;   // 爆发窗口门控 (残影/拖尾只在最快帧亮起)
        private bool slashFired;
        private bool slammed;
        private readonly Vector2[] tipHistory = new Vector2[10];
        private int tipCount;

        private Player Owner => Main.player[Projectile.owner];

        private static float PolyOut(float t, float power) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), power);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每段每敌一跳
            Projectile.scale = 1.3f;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Timer == 0f) {
                baseAngle = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
                facing = Projectile.velocity.X >= 0f ? 1 : -1;
                if (!Slam)
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.1f + Step * 0.15f, Volume = 0.8f }, Owner.Center);
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;
            Owner.ChangeDir(facing);

            float t = Timer;
            Timer += 1f;
            if (t >= Duration) {
                Projectile.Kill();
                return;
            }

            if (Slam)
                SlamMotion(t);
            else
                SwingMotion(t);

            Projectile.Center = Owner.MountedCenter + bladeAngle.ToRotationVector2() * (TipLen * 0.55f);
            Projectile.rotation = bladeAngle;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, bladeAngle - MathHelper.PiOver2);

            // 刀尖轨迹记录 (拖尾)
            Vector2 tip = Owner.MountedCenter + bladeAngle.ToRotationVector2() * TipLen;
            for (int i = tipHistory.Length - 1; i > 0; i--)
                tipHistory[i] = tipHistory[i - 1];
            tipHistory[0] = tip;
            if (tipCount < tipHistory.Length)
                tipCount++;

            // 挥砍金尘 (沿刀身, 爆发窗口更密)
            if (Main.rand.NextBool(burstGlow > 0.5f ? 1 : 3)) {
                Vector2 dustPos = Owner.MountedCenter + bladeAngle.ToRotationVector2() * Main.rand.NextFloat(50f, TipLen);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                Dust d = Dust.NewDustPerfect(dustPos, dustType, bladeAngle.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * SwingSign) * 3f, 100, default, 1.6f);
                d.noGravity = true;
            }

            Lighting.AddLight(tip, new Vector3(1f, 0.92f, 0.55f) * (0.5f + burstGlow * 0.5f));
        }

        /// <summary>横斩 (26f): 反向蓄势 → poly(16) 扫过 → 回弹沉降。</summary>
        private void SwingMotion(float t) {
            float t01 = t / Duration;
            float p;
            if (t01 < 0.42f) {
                p = -0.14f * ACMUtils.SineInOut(t01 / 0.42f);
                burstGlow = 0f;
            }
            else if (t01 < 0.56f) {
                p = -0.14f + 1.14f * PolyOut((t01 - 0.42f) / 0.14f, 16f);
                burstGlow = 1f;
            }
            else {
                p = 1f - 0.06f * MathF.Sin((t01 - 0.56f) / 0.44f * MathF.PI);
                burstGlow = MathF.Max(0f, 1f - (t01 - 0.56f) * 6f);
            }

            bladeAngle = baseAngle + (-2.1f + 4.2f * p) * SwingSign;

            // 爆发瞬间: 发射苍穹剑气
            if (!slashFired && t01 >= 0.46f) {
                slashFired = true;
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 slashVel = baseAngle.ToRotationVector2() * 14f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + slashVel * 2f, slashVel,
                        ModContent.ProjectileType<FirmamentSlash>(), (int)(Projectile.damage * 0.85f), Projectile.knockBack, Projectile.owner);
                }
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.05f + Step * 0.1f, Volume = 0.7f }, Owner.Center);
                WeaponVFX.AddScreenShake(Owner.Center, 1.5f);
            }
        }

        /// <summary>断穹斩 (44f): 18f 高举蓄势 (末段 late-snap 后拉+静默) → 3f poly(20) 劈落 → 收招回弹。</summary>
        private void SlamMotion(float t) {
            float raiseAngle = facing == 1 ? -MathHelper.PiOver2 - 0.65f : -MathHelper.PiOver2 + 0.65f;
            float slamAngle = facing == 1 ? 0.95f : MathF.PI - 0.95f;

            if (t < 18f) {
                // 高举: 慢而可读; 末 4 帧 pow(8) 后拉 + 轻微抖动 = "现在要来了"
                bladeAngle = Utils.AngleLerp(baseAngle, raiseAngle, ACMUtils.SineInOut(t / 18f));
                if (t >= 14f) {
                    float snap = MathF.Pow((t - 14f) / 4f, 8f);
                    bladeAngle -= 0.18f * facing * snap;
                    bladeAngle += MathF.Sin(t * 2.7f) * 0.03f;
                }
                burstGlow = 0f;

                // 蓄力尘向刀尖收敛, 72% 处硬切静默 (尖叫前的吸气)
                if (t >= 3f && t < 13f && Main.rand.NextBool(2)) {
                    Vector2 tip = Owner.MountedCenter + bladeAngle.ToRotationVector2() * TipLen;
                    Vector2 from = tip + Main.rand.NextVector2CircularEdge(90f, 90f);
                    Dust d = Dust.NewDustPerfect(from, DustID.GoldCoin, (tip - from) * 0.12f, 110, default, 1.4f);
                    d.noGravity = true;
                }
                if (t == 17f)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.55f, Volume = 0.55f }, Owner.Center);
            }
            else if (t < 21f) {
                // 劈落: 3 帧走完全部角行程
                bladeAngle = Utils.AngleLerp(raiseAngle - 0.18f * facing, slamAngle, PolyOut((t - 18f) / 3f, 20f));
                burstGlow = 1f;
            }
            else {
                // 收招: 轻微回弹 → 沉降
                bladeAngle = slamAngle - 0.12f * facing * MathF.Sin((t - 21f) / 23f * MathF.PI);
                burstGlow = MathF.Max(0f, 1f - (t - 21f) / 6f);

                if (!slammed) {
                    slammed = true;
                    Vector2 tip = Owner.MountedCenter + slamAngle.ToRotationVector2() * TipLen;

                    // 断穹时刻: 天裂 + 震屏 + 命中演出
                    if (Projectile.owner == Main.myPlayer) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), tip, Vector2.Zero,
                            ModContent.ProjectileType<FirmamentSkyRift>(), (int)(Projectile.damage * 0.63f), Projectile.knockBack * 0.5f, Projectile.owner);
                    }
                    ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), tip, ACMWeaponBurst.HeavenlyPillar, 1.6f, Projectile.owner);
                    WeaponVFX.AddScreenShake(tip, 6f);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f, Volume = 1f }, tip);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 0.8f }, tip);

                    for (int i = 0; i < 22; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(9f, 5f) - new Vector2(0f, Main.rand.NextFloat(4f));
                        int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                        Dust d = Dust.NewDustPerfect(tip, dustType, vel, 80, default, 2.2f);
                        d.noGravity = true;
                    }
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 伤害窗严格对齐视觉爆发帧
            float t01 = Timer / Duration;
            bool active = Slam ? (Timer >= 18f && Timer <= 26f) : (t01 >= 0.40f && t01 <= 0.66f);
            if (!active)
                return false;

            Vector2 dir = bladeAngle.ToRotationVector2();
            Vector2 start = Owner.MountedCenter + dir * 20f;
            Vector2 end = Owner.MountedCenter + dir * TipLen;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 40f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.GoldFlame;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, Slam ? 1.4f : 1.1f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, Slam ? 3f : 2f);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.25f, Volume = 0.6f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 刀尖双层 ribbon (速度门控: 只在爆发窗口全亮)
            if (tipCount >= 2) {
                var pts = new List<Vector2>(tipCount);
                for (int i = 0; i < tipCount; i++)
                    pts.Add(tipHistory[i]);
                float gate = 0.25f + 0.75f * burstGlow;
                WeaponVFX.DrawRibbonTrail(pts.ToArray(), 22f * Projectile.scale,
                    PillarPalette.SkyCyan * (0.55f * gate), PillarPalette.HolyWhite * (0.8f * gate),
                    tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 hand = Owner.MountedCenter + bladeAngle.ToRotationVector2() * 10f;
            bool flip = facing < 0;
            Vector2 origin = flip ? new Vector2(tex.Width, tex.Height) : new Vector2(0, tex.Height);
            float drawRot = bladeAngle + (flip ? 3f * MathHelper.PiOver4 : MathHelper.PiOver4);
            SpriteEffects fxs = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 断穹斩蓄力: 刀身金辉渐亮 (蓄力进度可读)
            if (Slam && Timer < 18f) {
                float charge = Timer / 18f;
                Color chargeGlow = PillarPalette.Gold * (0.5f * charge * charge);
                chargeGlow.A = 0;
                Main.spriteBatch.Draw(tex, hand - Main.screenPosition, null, chargeGlow, drawRot, origin, Projectile.scale * 1.06f, fxs, 0f);
            }

            // 爆发残影 (门控)
            if (burstGlow > 0.05f) {
                Color after = PillarPalette.HolyWhite * (0.35f * burstGlow);
                after.A = 0;
                Main.spriteBatch.Draw(tex, hand - Main.screenPosition, null, after, drawRot, origin, Projectile.scale * 1.03f, fxs, 0f);
            }

            Main.spriteBatch.Draw(tex, hand - Main.screenPosition, null, lightColor, drawRot, origin, Projectile.scale, fxs, 0f);

            return false;
        }
    }

    /// <summary>
    /// 苍穹剑气 - 金色青色剑气波 (横斩爆发帧释放)
    /// </summary>
    public class FirmamentSlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Terragrim;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 240;
            Projectile.height = 240;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.alpha = 100;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.97f;

            for (int i = 0; i < 3; i++) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(20, 10);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * 0.7f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 苍穹剑气金白祥瑞双层 ribbon (GlaciateWave)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 30f * Projectile.scale,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 180),
                tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.3f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle();
            Vector2 origin = rectangle.Size() / 2f;

            // 金色拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 220, 200), Color.Gold, progress);
                trailColor *= progress * 0.7f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float scale = Projectile.scale * (0.5f + progress * 0.5f);
                Main.spriteBatch.Draw(tex, pos, rectangle, trailColor, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0f);
            }

            // 外层发光
            Color outerGlow = Color.Gold * 0.4f;
            outerGlow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, outerGlow, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0f);

            // 主体
            Color mainColor = Color.Lerp(Color.Gold, Color.White, 0.3f);
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 苍穹裂隙 - 断穹斩劈开的竖直天裂 (专属 PillarSkyRift 着色器)。
    /// 开裂 6f → 驻留 → 26f 起弥合; 伤害窗 2~12f, 每敌至多两跳。前 8 帧金青染屏 (全屏名额契约)。
    /// </summary>
    public class FirmamentSkyRift : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int Life = 40;
        private const float Height = 720f;

        private int Age => Life - Projectile.timeLeft;
        private float Seed => Projectile.whoAmI * 0.173f % 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8; // 10f 伤害窗 → 每敌至多 2 跳
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Age == 0)
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.55f, Pitch = 0.3f }, Projectile.Center);

            // 裂隙上升金尘
            for (int i = 0; i < 2; i++) {
                if (!Main.rand.NextBool(2)) continue;
                Vector2 pos = Projectile.Center - new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(Height * 0.85f));
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                Dust d = Dust.NewDustPerfect(pos, dustType, new Vector2(0f, -Main.rand.NextFloat(1f, 3f)), 110, default, 1.4f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, PillarPalette.HolyWhite.ToVector3() * 1f);
            Lighting.AddLight(Projectile.Center - new Vector2(0f, Height * 0.5f), PillarPalette.SkyCyan.ToVector3() * 0.7f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Age < 2 || Age > 12)
                return false;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - new Vector2(0f, Height), Projectile.Center + new Vector2(0f, 44f),
                46f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1.2f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            float open = ACMUtils.QuadOut(MathHelper.Clamp(age / 6f, 0f, 1f));
            float close = 1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((age - 26f) / 14f, 0f, 1f));
            float progress = open * close;
            float fade = MathHelper.Clamp(close + 0.15f, 0f, 1f);

            if (progress <= 0.01f)
                return false;

            // 大招定调: 前 8 帧金青染屏 (强度 ≤0.12, 走全屏名额契约)
            if (age <= 8) {
                WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                    shadowTint: new Color(40, 62, 92, 255),
                    highlightTint: new Color(255, 232, 170, 210),
                    intensity: 0.12f * (1f - age / 9f), saturation: 1.05f);
            }

            // 天裂主体: PillarSkyRift 着色器 (顶点直带, uv.x 沿长: 0=冲击点 → 1=顶端)
            Effect fx = WeaponVFX.GetEffect("PillarSkyRift");
            if (fx != null) {
                Vector2 basePos = Projectile.Center + new Vector2(0f, 44f) - Main.screenPosition;
                Vector2 topPos = Projectile.Center - new Vector2(0f, Height) - Main.screenPosition;
                float halfWidth = 92f * (0.35f + 0.65f * progress);
                var verts = ACMUtils.BuildRibbonStrip([basePos, topPos], _ => halfWidth, _ => Color.White, 0f, 1);
                if (verts.Length >= 4) {
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uIntensity"]?.SetValue(fade);
                    fx.Parameters["uProgress"]?.SetValue(progress);
                    fx.Parameters["uColorCore"]?.SetValue(new Color(255, 252, 228, 235).ToVector4());
                    fx.Parameters["uColorEdge"]?.SetValue(new Color(255, 215, 120, 170).ToVector4());
                    fx.Parameters["uColorHaze"]?.SetValue(new Color(140, 215, 235, 120).ToVector4());
                    fx.Parameters["uSeed"]?.SetValue(Seed);

                    SpriteBatch sb = Main.spriteBatch;
                    GraphicsDevice gd = Main.graphics.GraphicsDevice;
                    sb.End();
                    sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Texture2D noise = ACMShaders.NoiseTexture;
                    gd.Textures[0] = noise;
                    gd.Textures[1] = noise;
                    gd.SamplerStates[1] = SamplerState.LinearWrap;
                    fx.CurrentTechnique.Passes[0].Apply();
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
                    sb.End();
                    ACMShaders.RestoreDefaultBatch(sb);
                }
            }
            else {
                // 着色器缺失兜底: 共享 BeamGrad 光柱
                ACMShaders.DrawBeam(Projectile.Center - new Vector2(0f, Height), Projectile.Center + new Vector2(0f, 44f),
                    60f * progress, PillarPalette.HolyWhite, PillarPalette.SkyCyan, fade, flowSpeed: 3f, coreSharp: 2.6f);
            }

            // 裂口电弧 (前 12 帧)
            Texture2D branch = ACMAsset.LightningBranch;
            if (branch != null && age < 12) {
                float seedBase = Projectile.whoAmI * 3.1f + (age / 3) * 1.9f;
                Color arc = PillarPalette.Lightning * (0.8f * (1f - age / 12f));
                arc.A = 0;
                Vector2 drawPos = new Vector2(Projectile.Center.X + MathF.Sin(seedBase) * 16f, Projectile.Center.Y + 40f) - Main.screenPosition;
                Main.spriteBatch.Draw(branch, drawPos, null, arc, 0f,
                    new Vector2(branch.Width * 0.5f, branch.Height),
                    new Vector2(0.9f, Height / branch.Height), SpriteEffects.None, 0f);
            }

            // 落点冲击环 + 柔光
            if (age < 20) {
                float rt = age / 20f;
                WeaponVFX.DrawShockwaveRing(Projectile.Center + new Vector2(0f, 30f), 18f + ACMUtils.QuadOut(rt) * 150f, 10f,
                    (1f - rt) * 0.9f, PillarPalette.HolyWhite, PillarPalette.SkyCyan);
            }
            WeaponVFX.DrawGlowBurst(Projectile.Center + new Vector2(0f, 20f), 2.6f * progress, PillarPalette.Gold * fade);

            return false;
        }
    }
}
