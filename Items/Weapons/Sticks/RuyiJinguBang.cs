using AncientChineseMythology.Helpers;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    /// <summary>
    /// 如意金箍棒 (系列旗舰): 四段连段 (横扫→回扫→双头回环→如意巨大化砸落);
    /// 命中积攒"如意值" (0~100, 棍身紧箍环逐环点亮); 满值右键释放"定海神针·真" —
    /// 天降全屏高定海神针 (RuyiPillarDrop 专属着色器), 落地 8x + 左右奔行冲击波 + 径向泛光/染屏 (名额契约内)。
    /// 从旧版白板 Swing 占位全面重做。
    /// </summary>
    public class RuyiJinguBang : StickWeaponItem
    {
        // 不新增贴图: 物品图标复用系列既有棍弹幕贴图 (替换旧版 vanilla 银剑占位图)
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";

        protected override int ComboLength => 4; // 第四段砸落即天然节奏高点

        public override void SetDefaults() {
            Item.damage = 340;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.knockBack = 12f;
            Item.value = Item.buyPrice(10, 0, 0, 0);
            Item.rare = ItemRarityID.Orange;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<JinguBangSwingProj>();
            Item.shootSpeed = 3.5f;
        }

        public override void HoldItem(Player player) {
            // 满值手部金红火花 (纯视觉)
            if (!Main.dedServ && player.GetModPlayer<RuyiStickPlayer>().ruyiPower >= 100 && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(player.MountedCenter + new Vector2(10f * player.direction, -4f),
                    Main.rand.NextBool() ? DustID.GoldFlame : DustID.RedTorch,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.5f)), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        public override bool CanUseItem(Player player) {
            // 神针召唤中不可再用; 右键需要满如意值
            if (player.ownedProjectileCounts[ModContent.ProjectileType<JinguBangGrandPillar>()] > 0)
                return false;
            if (player.altFunctionUse == 2 && player.GetModPlayer<RuyiStickPlayer>().ruyiPower < 100)
                return false;
            return base.CanUseItem(player);
        }

        protected override void ShootAlt(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback) {
            // 落点 owner 端捕获, 经 ai 同步; 立即消耗如意值 (施法承诺)
            player.GetModPlayer<RuyiStickPlayer>().ruyiPower = 0;
            float targetX = MathHelper.Clamp(Main.MouseWorld.X, player.Center.X - 1500f, player.Center.X + 1500f);
            float targetY = MathHelper.Clamp(Main.MouseWorld.Y, player.Center.Y - 900f, player.Center.Y + 900f);
            Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<JinguBangGrandPillar>(), damage, knockback, player.whoAmI, targetX, targetY);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "RuyiCombo", "四段连段: 横扫、回扫、双头回环、如意巨大化砸落"));
            tooltips.Add(new TooltipLine(Mod, "RuyiPower", "命中积攒如意值, 棍身紧箍环逐环点亮"));
            int power = Main.LocalPlayer.GetModPlayer<RuyiStickPlayer>()?.ruyiPower ?? 0;
            tooltips.Add(new TooltipLine(Mod, "RuyiUlt", $"如意值蓄满时右键: 天降定海神针 (当前 {power}/100)") {
                OverrideColor = power >= 100 ? new Color(255, 215, 120) : null
            });
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<TrueRuyiStick>(), 1);
            recipe.AddIngredient(ItemID.ChlorophyteBar, 100);
            recipe.AddIngredient(ItemID.TurtleShell, 10);
            recipe.AddTile(TileID.MythrilAnvil);
            recipe.Register();
        }
    }

    /// <summary>金箍棒棍身着色器绘制助手 (RuyiGoldenCudgel; 真·如意棍低强度档共用)。</summary>
    internal static class RuyiCudgelVFX
    {
        public static void DrawStickWithShader(Texture2D tex, Vector2 drawPos, float rotation, Vector2 origin,
            float scale, SpriteEffects fx, Color lightColor, float intensity, float charge, float flash,
            Color gold, Color secondary) {
            if (Main.dedServ)
                return;
            Effect effect = WeaponVFX.GetEffect("RuyiGoldenCudgel");
            if (effect == null) {
                Main.EntitySpriteDraw(tex, drawPos, null, lightColor, rotation, origin, scale, fx, 0);
                return;
            }

            // uv 为贴图空间坐标, 不随 SpriteEffects 翻转变化 — 棍轴恒为 (1,-1)/√2 (柄左下 → 尖右上)
            effect.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            effect.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(charge, 0f, 1f));
            effect.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            effect.Parameters["uAxis"]?.SetValue(new Vector2(0.7071f, -0.7071f));
            effect.Parameters["uColorGold"]?.SetValue(gold.ToVector4());
            effect.Parameters["uColorRed"]?.SetValue(secondary.ToVector4());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, drawPos, null, lightColor, rotation, origin, scale, fx, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }

    /// <summary>
    /// 如意金箍棒持械弹幕: 四段连段。命中积攒如意值 (+4 / 暴击 +3 / 第四段 +6);
    /// 棍身常驻 RuyiGoldenCudgel 着色器 (强度与紧箍环随如意值)。
    /// </summary>
    internal class JinguBangSwingProj : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";

        public override LocalizedText DisplayName
            => Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.RuyiStickSpearProjectile.DisplayName");

        private static readonly SwingStep[] _steps = {
            SwingStep.Sweep(3.8f, 1f),
            SwingStep.Sweep(3.8f, 1.1f, sign: -1),
            SwingStep.Spin(1.5f, 1.5f, timeMul: 1.4f, scaleMul: 1.15f),               // 双头回环
            SwingStep.Sweep(4.8f, 1.9f, sign: 1, timeMul: 1.45f, scaleMul: 1.7f, impact: true), // 如意巨大化砸落
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 18;
        protected override Color TrailOuter => new(150, 20, 30, 160);
        protected override Color TrailInner => new(255, 210, 120, 210);
        protected override float TipLength => 112f;
        protected override float Overshoot => 0.25f;
        protected override int BurstTheme => StepIndex == 3 ? ACMWeaponBurst.Fatal : ACMWeaponBurst.Gold;
        protected override float HitShake => 2.2f;
        protected override int HitDustType => DustID.GoldFlame;
        protected override Vector3 GlowLight => new(0.6f, 0.35f, 0.12f);

        private float Charge01 => Owner.GetModPlayer<RuyiStickPlayer>().ruyiPower / 100f;

        protected override void OnStickHitNPC(NPC target, NPC.HitInfo hit) {
            // 如意值只在 owner 端积攒 (资源本地权威)
            if (Main.myPlayer == Projectile.owner) {
                int gain = 4 + (hit.Crit ? 3 : 0) + (StepIndex == 3 ? 6 : 0);
                Owner.GetModPlayer<RuyiStickPlayer>().AddRuyiPower(gain);
            }
        }

        protected override void OnStrikeStart(SwingStep step) {
            if (StepIndex == 3) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 1.05f, Pitch = -0.35f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.45f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Owner.Center, 2.5f);
                return;
            }
            base.OnStrikeStart(step);
        }

        protected override void DoGroundImpact(Vector2 tip) {
            if (StepIndex == 3) {
                // 第四段砸落: 冲击环 + 屏震 4 + Fatal 爆裂
                WeaponVFX.AddScreenShake(tip, 4f);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1.05f, Pitch = -0.3f }, tip);
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), tip, ACMWeaponBurst.Fatal, 1.4f, Projectile.owner);
                for (int i = 0; i < 16; i++) {
                    Dust d = Dust.NewDustPerfect(tip, Main.rand.NextBool() ? DustID.GoldFlame : DustID.RedTorch,
                        new Vector2(Main.rand.NextFloat(-4.5f, 4.5f), Main.rand.NextFloat(-5.5f, -1f)), 0, default, Main.rand.NextFloat(1.1f, 1.7f));
                    d.noGravity = Main.rand.NextBool();
                }
                return;
            }
            base.DoGroundImpact(tip);
        }

        protected override void DrawStick(Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            GetDrawParams(tex, out Vector2 origin, out float rotOff, out SpriteEffects fx);
            float flash = MathHelper.Clamp((LengthPulse - 1f) / Overshoot, 0f, 1f) * 0.45f;
            RuyiCudgelVFX.DrawStickWithShader(tex, StickDrawCenter() - Main.screenPosition,
                Projectile.rotation + rotOff, origin, Projectile.scale * LengthPulse, fx,
                lightColor * Projectile.Opacity,
                intensity: 0.35f + 0.65f * Charge01, charge: Charge01, flash: flash,
                gold: new Color(255, 215, 120), secondary: new Color(220, 40, 50));
        }
    }

    /// <summary>
    /// 定海神针·真 (如意金箍棒大招): 30 帧前摇 (棍上指, 金红流光收束, 末 8 帧静默) →
    /// 天降全屏高神针砸落目标 X (RuyiPillarDrop 着色器绘制) → 落地: 屏震 12 / 8x 单次判定 /
    /// 径向泛光 + 金红染屏 (全屏名额契约内, <0.6s) / 左右两道奔行冲击波。
    /// ai[0] = 落点 X, ai[1] = 参考 Y (owner 端捕获, 生成包同步); 落地 Y 由瓦片扫描各端确定性求出。
    /// </summary>
    internal class JinguBangGrandPillar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override LocalizedText DisplayName
            => Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.RuyiStickSpearProjectile.DisplayName");

        private const int WindupFrames = 30;
        private const int FallFrames = 10;
        private const int EmbedFrames = 60;
        private const int ImpactFrame = WindupFrames + FallFrames;
        private const float PillarHeight = 1500f;
        private const float HalfWidth = 170f;

        private float TargetX => Projectile.ai[0];
        private float TargetY => Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];
        private Player Owner => Main.player[Projectile.owner];

        private float _groundY = -1f;
        private bool _impactDone;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ImpactFrame + EmbedFrames + 8;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            FindLanding();
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = -0.2f }, Owner.Center);
        }

        /// <summary>从参考 Y 上方向下扫第一格实心瓦片 (各端同瓦片数据, 结果一致)。</summary>
        private void FindLanding() {
            int tx = (int)(TargetX / 16f);
            int startY = (int)((TargetY - 120f) / 16f);
            for (int ty = Math.Max(startY, 10); ty < Math.Min(startY + 160, Main.maxTilesY - 10); ty++) {
                if (WorldGen.InWorld(tx, ty) && WorldGen.SolidTile(tx, ty)) {
                    _groundY = ty * 16f;
                    return;
                }
            }
            _groundY = TargetY + 800f;
        }

        /// <summary>当前针底 Y (下坠三次缓入)。</summary>
        private float BottomY {
            get {
                if (Timer < WindupFrames)
                    return _groundY - PillarHeight - 400f;
                float t = MathHelper.Clamp((Timer - WindupFrames) / FallFrames, 0f, 1f);
                return MathHelper.Lerp(_groundY - PillarHeight - 400f, _groundY + 14f, t * t * t);
            }
        }

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (_groundY < 0f)
                FindLanding();

            if (Timer < WindupFrames) {
                // —— 前摇: 棍上举, 玩家定身聚力 ——
                owner.heldProj = Projectile.whoAmI;
                owner.itemAnimation = 2;
                owner.itemTime = 2;
                Projectile.Center = owner.MountedCenter + new Vector2(6f * owner.direction, -66f);
                owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, MathHelper.Pi + 0.25f * owner.direction);
                if (Main.myPlayer == Projectile.owner)
                    owner.velocity.X *= 0.88f;

                float w01 = Timer / (float)WindupFrames;
                // 汇聚流光, 末 8 帧静默 (爆发前的吸气)
                if (Timer < WindupFrames - 8 && Main.rand.NextFloat() < 0.35f + 0.5f * w01) {
                    Vector2 tip = Projectile.Center + new Vector2(0f, -46f);
                    Vector2 from = tip + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(90f, 200f);
                    Dust d = Dust.NewDustPerfect(from, Main.rand.NextBool() ? DustID.GoldFlame : DustID.RedTorch,
                        (tip - from) * 0.09f, 100, default, Main.rand.NextFloat(1.1f, 1.8f));
                    d.noGravity = true;
                }
                if (Timer == 10 || Timer == 20)
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = Timer / 25f }, owner.Center);
                if (Timer == WindupFrames - 8)
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.5f }, owner.Center);
            }
            else if (Timer < ImpactFrame) {
                // —— 下坠 (纯预警演出, 无判定) ——
                Projectile.Center = new Vector2(TargetX, BottomY - PillarHeight * 0.5f);
                if (Timer == WindupFrames) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 1f, Pitch = -0.5f }, owner.Center);
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = -0.4f }, owner.Center);
                }
            }
            else {
                Projectile.Center = new Vector2(TargetX, _groundY + 14f - PillarHeight * 0.5f);
                if (!_impactDone) {
                    _impactDone = true;
                    DoImpact();
                }
            }

            Lighting.AddLight(new Vector2(TargetX, MathHelper.Clamp(owner.Center.Y, BottomY - PillarHeight, BottomY)),
                new Vector3(0.8f, 0.45f, 0.2f) * (Timer >= WindupFrames ? 1f : 0.3f));

            Timer++;
        }

        private void DoImpact() {
            Vector2 impact = new(TargetX, _groundY);
            WeaponVFX.AddScreenShake(impact, 12f); // 一次性大招级 (§C.2)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.1f, Pitch = -0.4f }, impact);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1.2f, Pitch = -0.5f }, impact);
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.8f, Pitch = -0.2f }, impact);

            ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), impact, ACMWeaponBurst.Fatal, 2.2f, Projectile.owner);

            // 碎石帷幕
            for (int i = 0; i < 34; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-9f, -2f));
                Dust d = Dust.NewDustPerfect(impact + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 0f),
                    Main.rand.NextBool(3) ? DustID.Smoke : (Main.rand.NextBool() ? DustID.GoldFlame : DustID.RedTorch),
                    vel, 60, default, Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = d.type != DustID.Smoke;
            }

            // 左右奔行冲击波 (owner 端生成)
            if (Main.myPlayer == Projectile.owner) {
                for (int s = -1; s <= 1; s += 2) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        impact + new Vector2(s * 60f, -20f), new Vector2(s * 10f, 0f),
                        ModContent.ProjectileType<JinguBangQuakeWave>(), Projectile.damage, Projectile.knockBack * 0.6f,
                        Projectile.owner);
                }
            }
        }

        // 判定只在落地冲击窗口 (帧 0~8): 与视觉砸落严格对齐; 下坠是纯预警
        public override bool? CanDamage() {
            if (Timer >= ImpactFrame && Timer < ImpactFrame + 8)
                return base.CanDamage();
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle pillar = new((int)(TargetX - HalfWidth), (int)(_groundY - PillarHeight), (int)(HalfWidth * 2f), (int)PillarHeight);
            return pillar.Intersects(targetHitbox);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > TargetX ? 1 : -1;
            modifiers.FinalDamage *= 8f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // —— 前摇: 落点致命红预警 (公平阀: 40 帧可读) + 上举棍身 ——
            if (Timer < ImpactFrame && _groundY > 0f) {
                float pulse = 0.55f + 0.45f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f);
                WeaponVFX.DrawGlowBurst(new Vector2(TargetX, _groundY), 1.1f * pulse, new Color(250, 40, 56) * 0.75f);
            }

            if (Timer < WindupFrames) {
                float w01 = Timer / (float)WindupFrames;
                // 复用已自动加载的系列棍贴图 (不在绘制中 Request 新资源)
                Texture2D stick = TextureAssets.Projectile[ModContent.ProjectileType<RuyiStickSpearProjectile>()].Value;
                // 末 8 帧收束: 微缩 (爆发前变小)
                float collapse = Timer >= WindupFrames - 8 ? 1f - 0.12f * ((Timer - (WindupFrames - 8)) / 8f) : 1f;
                RuyiCudgelVFX.DrawStickWithShader(stick, Projectile.Center - Main.screenPosition,
                    -MathHelper.PiOver2 + MathHelper.ToRadians(45f), stick.Size() * 0.5f,
                    (1.3f + w01 * 0.6f) * collapse, SpriteEffects.None, Color.White,
                    intensity: 1f, charge: 1f, flash: w01 * w01, gold: new Color(255, 215, 120), secondary: new Color(220, 40, 50));
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.6f + w01 * 0.8f, new Color(255, 170, 90) * (0.4f + w01 * 0.6f));
                return false;
            }

            // —— 神针柱体 (专属着色器) ——
            float impactT = _impactDone ? MathHelper.Clamp((Timer - ImpactFrame) / 12f, 0f, 1f) : 0f;
            float fade = _impactDone ? MathHelper.Clamp((Timer - ImpactFrame - (EmbedFrames - 24)) / 24f, 0f, 1f) : 0f;
            float scroll = Timer < ImpactFrame ? -Timer * 0.12f : -ImpactFrame * 0.12f; // 落地骤停
            DrawPillar(BottomY, 1f - impactT, fade, scroll);

            // 落地: 双环冲击波 + 径向泛光 → 金红染屏 (顺序占名额, 不冲突)
            if (_impactDone) {
                float t = Timer - ImpactFrame;
                Vector2 impact = new(TargetX, _groundY);
                if (t < 26f) {
                    float rt = t / 26f;
                    WeaponVFX.DrawShockwaveRing(impact, 20f + rt * 320f, 16f, (1f - rt) * 0.95f,
                        new Color(255, 210, 120), new Color(150, 20, 30));
                    if (t > 5f) {
                        float rt2 = (t - 5f) / 21f;
                        WeaponVFX.DrawShockwaveRing(impact, 14f + rt2 * 200f, 10f, (1f - rt2) * 0.7f,
                            new Color(255, 240, 190), new Color(200, 60, 40));
                    }
                }
                if (t < 9f)
                    WeaponVFX.DrawRadialBloom(impact, 0.26f, (1f - t / 9f) * 0.9f, new Color(255, 190, 110), 10f);
                else if (t < 36f && Projectile.owner == Main.myPlayer)
                    WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                        shadowTint: new Color(150, 30, 30), highlightTint: new Color(255, 215, 130),
                        intensity: 0.12f * (1f - (t - 9f) / 27f), saturation: 1.08f);
            }
            return false;
        }

        /// <summary>用 RuyiPillarDrop 着色器画竖直 quad 针体。</summary>
        private void DrawPillar(float bottomY, float brightness, float fade, float scroll) {
            Effect fx = WeaponVFX.GetEffect("RuyiPillarDrop");
            Texture2D soft = ACMAsset.SoftGlow;
            if (fx == null || soft == null || Main.dedServ)
                return;

            float impact = _impactDone ? MathF.Max(0f, 1f - (Timer - ImpactFrame) / 12f) : 0f;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uScroll"]?.SetValue(scroll);
            fx.Parameters["uImpact"]?.SetValue(impact);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uColorGold"]?.SetValue(new Color(255, 205, 110).ToVector4());
            fx.Parameters["uColorRed"]?.SetValue(new Color(215, 45, 50).ToVector4());

            Vector2 tl = new Vector2(TargetX - HalfWidth * 1.15f, bottomY - PillarHeight) - Main.screenPosition;
            Vector2 tr = new Vector2(TargetX + HalfWidth * 1.15f, bottomY - PillarHeight) - Main.screenPosition;
            Vector2 bl = new Vector2(TargetX - HalfWidth * 1.15f, bottomY) - Main.screenPosition;
            Vector2 br = new Vector2(TargetX + HalfWidth * 1.15f, bottomY) - Main.screenPosition;

            var verts = new ColoredVertex[6];
            Color white = Color.White;
            verts[0] = new ColoredVertex(tl, new Vector3(0f, 0f, 0f), white);
            verts[1] = new ColoredVertex(tr, new Vector3(1f, 0f, 0f), white);
            verts[2] = new ColoredVertex(bl, new Vector3(0f, 1f, 0f), white);
            verts[3] = new ColoredVertex(bl, new Vector3(0f, 1f, 0f), white);
            verts[4] = new ColoredVertex(tr, new Vector3(1f, 0f, 0f), white);
            verts[5] = new ColoredVertex(br, new Vector3(1f, 1f, 0f), white);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = soft;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }

    /// <summary>
    /// 神针落地奔行冲击波: 沿地表左右奔行 (吸附地形), 2x 单次判定, 60 帧。
    /// SlashBurst 竖向喷发 + 金红尘视觉 (无独立贴图)。
    /// </summary>
    internal class JinguBangQuakeWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override LocalizedText DisplayName
            => Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.RuyiStickSpearProjectile.DisplayName");

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 76;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            Projectile.velocity.Y = 0f;
            SnapToGround();

            Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 15f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.35f, 0.12f));

            // 奔行喷发尘 (动能正比)
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f),
                    Main.rand.NextBool() ? DustID.GoldFlame : DustID.RedTorch,
                    new Vector2(Projectile.velocity.X * 0.15f, Main.rand.NextFloat(-5f, -2f)), 100, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
        }

        /// <summary>吸附地表: 脚下悬空则下落, 埋入则上抬 (各最多 5 格)。</summary>
        private void SnapToGround() {
            int tx = (int)(Projectile.Center.X / 16f);
            int ty = (int)(Projectile.Bottom.Y / 16f);
            if (!WorldGen.InWorld(tx, ty, 12)) {
                Projectile.Kill();
                return;
            }
            if (WorldGen.SolidTile(tx, ty)) {
                for (int up = 1; up <= 5; up++) {
                    if (!WorldGen.SolidTile(tx, ty - up)) {
                        Projectile.position.Y -= up * 16f;
                        return;
                    }
                }
                Projectile.Kill(); // 撞上高墙 → 波散
            }
            else {
                for (int down = 1; down <= 5; down++) {
                    if (WorldGen.SolidTile(tx, ty + down)) {
                        Projectile.position.Y += (down - 1) * 16f;
                        return;
                    }
                }
                Projectile.position.Y += 4f * 16f;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = Projectile.velocity.X > 0f ? 1 : -1;
            modifiers.FinalDamage *= 2f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 2f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, 1.1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D burst = ACMAsset.SlashBurst;
            if (burst == null)
                return false;

            float life = 1f - Projectile.timeLeft / 60f;
            float wob = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f + Projectile.whoAmI);
            Color gold = new Color(255, 200, 110) * Projectile.Opacity;
            gold.A = 0;
            Color red = new Color(220, 60, 50) * (Projectile.Opacity * 0.7f);
            red.A = 0;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            // 竖向喷发 (底部对齐地面), 随奔行方向微倾
            float lean = -Projectile.velocity.X * 0.02f;
            Vector2 scale = new(0.24f * wob, 0.16f * (1f + life * 0.3f));
            sb.Draw(burst, Projectile.Bottom - Main.screenPosition, null, gold, lean,
                new Vector2(burst.Width * 0.5f, burst.Height), scale, SpriteEffects.None, 0f);
            sb.Draw(burst, Projectile.Bottom - Main.screenPosition, null, red, lean * 1.4f,
                new Vector2(burst.Width * 0.5f, burst.Height), scale * 1.35f, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }
    }
}
