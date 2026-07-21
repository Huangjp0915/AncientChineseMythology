using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
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

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 地狱冥龙吐纳寂灭炮 - 终极远程炮
    /// 蓄力"吐纳"循环: 按住"纳"(0-90 帧, 三档), 松开"吐"出膨胀的冥龙息吐纳波。
    /// 龙息沿途灼烧, 命中挂毁灭印记延迟引爆; 满纳波头带冥龙魂首, 印记伤害 ×2.5。
    /// </summary>
    public class HellwyrmAnnihilationCannon : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 12200;
            Item.crit = 20;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 76;
            Item.height = 34;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 14f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.shoot = ModContent.ProjectileType<HellwyrmCannonHeld>();
            Item.shootSpeed = 16f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<HellwyrmCannonHeld>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.MountedCenter,
                velocity.SafeNormalize(Vector2.UnitX * player.direction),
                ModContent.ProjectileType<HellwyrmCannonHeld>(), damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulEatingCannon>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 蓄力手持炮 - "吐纳"循环。
    /// ai[0]=蓄力值 (owner 权威推进, 档位跨越时 netUpdate 校正远端预测),
    /// ai[1]=状态 (0 纳/蓄力, 1 吐/后坐收招), ai[2]=释放档位。
    /// 三档: 0-29 轻吐 / 30-69 中吐 / ≥70 满纳。蓄力 &gt;72% 后汇聚粒子全部剪除 (吸饱静默)。
    /// </summary>
    public class HellwyrmCannonHeld : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/HellwyrmAnnihilationCannon";

        private const int MaxCharge = 90;
        private const int Tier2 = 30;
        private const int Tier3 = 70;
        private const float RecoilTime = 12f;
        private const float SilenceFrac = 0.72f;
        private const float TierFlashTime = 10f;
        private const float MuzzleLen = 40f;

        private ref float Charge => ref Projectile.ai[0];
        private ref float State => ref Projectile.ai[1];
        private ref float ReleaseTier => ref Projectile.ai[2];
        private ref float RecoilTimer => ref Projectile.localAI[0];

        private int _tierReached = -1;
        private float _tierFlash;
        private bool _releaseFxDone;
        private Player Owner => Main.player[Projectile.owner];
        private float Charge01 => MathHelper.Clamp(Charge / MaxCharge, 0f, 1f);

        private static int TierOf(float charge) => charge >= Tier3 ? 2 : charge >= Tier2 ? 1 : 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.HellwyrmCannonHeld.DisplayName",
                () => "Hellwyrm Annihilation Cannon");
        }

        public override void SetDefaults() {
            Projectile.width = 76;
            Projectile.height = 34;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed
                || Owner.HeldItem?.type != ModContent.ItemType<HellwyrmAnnihilationCannon>()) {
                Projectile.Kill();
                return;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            // 瞄准: owner 端跟鼠标, 远端用同步 velocity (角度阈值节流 netUpdate)
            if (Main.myPlayer == Projectile.owner) {
                Vector2 aim = Owner.DirectionTo(Main.MouseWorld);
                if (Vector2.Distance(aim, Projectile.velocity) > 0.035f) {
                    Projectile.velocity = aim;
                    Projectile.netUpdate = true;
                }
                Owner.ChangeDir(Main.MouseWorld.X > Owner.Center.X ? 1 : -1);
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            Projectile.rotation = dir.ToRotation();
            Owner.itemRotation = (dir * Owner.direction).ToRotation();
            float armRot = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot);

            // 炮身位置: 蓄力后错 charge²×8px; 吐后按档位后坐并弹回
            float back = State == 0
                ? Charge01 * Charge01 * 8f
                : (6f + (int)ReleaseTier * 5f) * (1f - RecoilTimer / RecoilTime) * (1f - RecoilTimer / RecoilTime);
            Projectile.Center = Owner.MountedCenter + dir * (24f - back);

            if (State == 0)
                ChargingAI(dir);
            else
                RecoilAI(dir);

            if (_tierFlash > 0f)
                _tierFlash--;
        }

        private void ChargingAI(Vector2 dir) {
            // 蓄力推进: owner 权威; 远端本地同步预测, 以档位同步为校正点
            if (Charge < MaxCharge)
                Charge++;
            float charge01 = Charge01;
            Vector2 muzzle = Projectile.Center + dir * MuzzleLen;

            int tier = TierOf(Charge);
            if (tier > _tierReached) {
                _tierReached = tier;
                _tierFlash = TierFlashTime;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -0.2f * tier }, Projectile.Center);
                if (Main.myPlayer == Projectile.owner)
                    Projectile.netUpdate = true;
            }

            // "纳": 炮口前方 100-320px 汇聚粒子, 概率 ∝ sqrt(charge); >72% 完全剪除 (吸饱静默)
            if (charge01 <= SilenceFrac) {
                float p = 0.18f + 0.62f * MathF.Sqrt(charge01);
                for (int i = 0; i < 5; i++) {
                    if (Main.rand.NextFloat() > p)
                        continue;
                    Vector2 pos = muzzle + dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(100f, 320f);
                    bool shadow = Main.rand.NextBool(3);
                    Dust d = Dust.NewDustPerfect(pos, shadow ? DustID.Shadowflame : DustID.PurpleTorch,
                        (muzzle - pos) * 0.085f, 100, shadow ? new Color(120, 40, 200) : default,
                        Main.rand.NextFloat(1.2f, 2f));
                    d.noGravity = true;
                }
            }

            // 蓄力低鸣: 每 12 帧一声, 音高随蓄力从 -0.8 爬到 +0.2 (到顶后 Charge 停增 → 自然静默)
            if ((int)Charge % 12 == 0)
                SoundEngine.PlaySound(SoundID.Item13 with {
                    Volume = 0.35f + charge01 * 0.3f,
                    Pitch = -0.8f + charge01 * 1f
                }, Projectile.Center);

            Lighting.AddLight(muzzle, 0.5f + charge01 * 0.9f, 0.2f + charge01 * 0.25f, 0.7f + charge01 * 0.9f);

            if (Main.myPlayer == Projectile.owner && !Owner.channel)
                Fire(dir);
        }

        /// <summary>"吐": 松开时按档位释放吐纳波 (仅 owner 端)。</summary>
        private void Fire(Vector2 dir) {
            int tier = TierOf(Charge);
            float dmgMult = tier == 2 ? 3.4f : tier == 1 ? 1.9f : 0.9f;
            float scaleMult = tier == 2 ? 1.8f : tier == 1 ? 1.4f : 1f;
            float recoil = tier == 2 ? 15f : tier == 1 ? 9f : 4f;

            Vector2 muzzle = Projectile.Center + dir * MuzzleLen;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem), muzzle, dir * 16f,
                ModContent.ProjectileType<DragonBreathWave>(), (int)(Projectile.damage * dmgMult),
                Projectile.knockBack, Projectile.owner, 0f, tier == 2 ? 1f : 0f, scaleMult);

            Owner.velocity -= dir * recoil;
            State = 1;
            ReleaseTier = tier;
            RecoilTimer = 0f;
            Projectile.netUpdate = true;
        }

        private void RecoilAI(Vector2 dir) {
            int tier = (int)ReleaseTier;

            // 释放演出: 各端在观察到状态切换时各自触发一次 (远端经 ai 同步得知)
            if (!_releaseFxDone) {
                _releaseFxDone = true;
                Vector2 muzzle = Projectile.Center + dir * MuzzleLen;
                SoundEngine.PlaySound(SoundID.Item36 with {
                    Volume = 1.2f + tier * 0.3f,
                    Pitch = -0.6f - tier * 0.15f
                }, muzzle);
                if (tier == 2)
                    SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1f, Pitch = -0.3f }, muzzle);
                WeaponVFX.AddScreenShake(muzzle, 2f + tier * 2f);

                // 吐息炮口爆发 (一次性, 满档合计 34 粒)
                int smoke = 8 + tier * 5;
                for (int i = 0; i < smoke; i++) {
                    Vector2 vel = dir.RotatedByRandom(MathHelper.ToRadians(32)) * Main.rand.NextFloat(4f, 11f);
                    Dust d = Dust.NewDustPerfect(muzzle, DustID.Smoke, vel, 190, new Color(30, 12, 45),
                        Main.rand.NextFloat(2.2f, 3.6f));
                    d.noGravity = true;
                }
                int spark = 6 + tier * 5;
                for (int i = 0; i < spark; i++) {
                    bool red = Main.rand.NextBool(3);
                    Vector2 vel = dir.RotatedByRandom(MathHelper.ToRadians(22)) * Main.rand.NextFloat(6f, 15f);
                    Dust d = Dust.NewDustPerfect(muzzle, red ? DustID.Torch : DustID.PurpleTorch, vel, 60,
                        red ? new Color(255, 60, 80) : default, Main.rand.NextFloat(2f, 3.4f));
                    d.noGravity = true;
                }
            }

            RecoilTimer++;
            if (RecoilTimer >= RecoilTime)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            float charge01 = Charge01;
            int tier = State == 0 ? TierOf(Charge) : (int)ReleaseTier;

            // 满纳 ±1px 随机抖动 (仅绘制层)
            Vector2 jitter = State == 0 && tier == 2
                ? new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f))
                : Vector2.Zero;
            Vector2 bodyPos = Projectile.Center + jitter;
            Vector2 muzzle = bodyPos + dir * MuzzleLen;

            // 炮管水平贴图: rotation=瞄准角, 反向时垂直翻转
            SpriteEffects fx = dir.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Main.EntitySpriteDraw(tex, bodyPos - Main.screenPosition, null, lightColor,
                Projectile.rotation, tex.Size() / 2f, 1f, fx, 0);

            if (State == 0) {
                // 炮口蓄能辉核: 虚空紫 → 冥焰红
                Color coreCol = Color.Lerp(FengduVFX.VoidMid, new Color(255, 90, 100), charge01);
                WeaponVFX.DrawGlowBurst(muzzle, 0.3f + charge01 * 0.8f, coreCol * (0.3f + 0.55f * charge01));
                if (charge01 > 0.33f)
                    ACMShaders.DrawBeam(bodyPos - dir * 24f, muzzle, 6f, coreCol, FengduVFX.VoidDark,
                        (charge01 - 0.33f) * 1.2f, flowSpeed: 3f, flowScale: 1.6f);
            }
            else {
                // 吐后余焰
                float t = 1f - MathHelper.Clamp(RecoilTimer / RecoilTime, 0f, 1f);
                if (t > 0f)
                    WeaponVFX.DrawGlowBurst(muzzle, (1f + tier * 0.5f) * t, new Color(255, 110, 120) * (0.8f * t));
            }

            // 到档瞬间的炮口档位光环闪
            if (_tierFlash > 0f) {
                float f = _tierFlash / TierFlashTime;
                WeaponVFX.DrawGlowBurst(muzzle, 0.8f + (1f - f) * 0.6f,
                    Color.Lerp(FengduVFX.VoidBright, new Color(255, 90, 100), tier / 2f) * (0.7f * f));
            }
            return false;
        }
    }

    /// <summary>
    /// 冥龙吐纳波 - 巨大的膨胀冥焰龙息弹。
    /// ai[1]=1 为满纳波 (带冥龙魂首, 印记伤害 ×2.5), ai[2]=初始体积倍率 (按档 ×1/×1.4/×1.8)。
    /// </summary>
    public class DragonBreathWave : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/HellwyrmAnnihilationCannon";
        private ref float Timer => ref Projectile.ai[0];
        private bool FullBreath => Projectile.ai[1] >= 1f;
        private float ScaleMult => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.97f;

            float expansion = MathHelper.Clamp(Timer / 30f, 0f, 3f);
            Projectile.scale = (1f + expansion) * ScaleMult;

            float brightness = MathHelper.Clamp(1f - Timer / 90f, 0.2f, 1f);
            Lighting.AddLight(Projectile.Center, 1.4f * brightness, 0.35f * brightness, 1.5f * brightness);

            // 冥焰粒子: 紫焰为主 + 少量冥焰红 (环境粒子合计 ≤6/帧)
            int particleCount = Math.Min(4, 2 + (int)expansion);
            for (int i = 0; i < particleCount; i++) {
                float radius = 15f * Projectile.scale;
                Vector2 offset = Main.rand.NextVector2Circular(radius, radius);
                bool red = Main.rand.NextBool(4);
                Dust fire = Dust.NewDustDirect(
                    Projectile.Center + offset, 4, 4, red ? DustID.Torch : DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.3f + Main.rand.NextFloat(-2f, 2f),
                    -Projectile.velocity.Y * 0.3f + Main.rand.NextFloat(-2f, 2f),
                    60, red ? new Color(255, 60, 80) : default, Main.rand.NextFloat(2f, 3.5f));
                fire.noGravity = true;
            }

            if (Timer > 10 && Main.rand.NextBool(2)) {
                for (int i = 0; i < 2; i++) {
                    float smokeRadius = 20f * Projectile.scale;
                    Vector2 smokeOffset = Main.rand.NextVector2Circular(smokeRadius, smokeRadius);
                    Dust smoke = Dust.NewDustDirect(
                        Projectile.Center + smokeOffset, 8, 8, DustID.Smoke,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f),
                        200, new Color(25, 8, 40), Main.rand.NextFloat(2f, 4f));
                    smoke.noGravity = true;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = 20f * Projectile.scale + 20f;
            Vector2 closestPoint = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(Projectile.Center, closestPoint) < radius;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Ichor, 600);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.FengduVoid, 1f, Projectile.owner);
            for (int i = 0; i < 6; i++) {
                bool red = Main.rand.NextBool(3);
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, red ? DustID.Torch : DustID.PurpleTorch, vel, 60,
                    red ? new Color(255, 60, 80) : default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }

            int delayedBoom = ModContent.ProjectileType<DragonAnnihilationMark>();
            bool alreadyMarked = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == delayedBoom
                    && Main.projectile[i].owner == Projectile.owner && Main.projectile[i].ai[1] == target.whoAmI) {
                    alreadyMarked = true;
                    break;
                }
            }

            if (!alreadyMarked) {
                // 满纳波印记 ×2.5, 普通 ×2
                int markDamage = (int)(Projectile.damage * (FullBreath ? 2.5f : 2f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    delayedBoom, markDamage, 0f, Projectile.owner, 0f, target.whoAmI);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / 90f;
            float opacity = 1f - progress * 0.6f;

            // 冥龙息: BeamGrad 双段吐纳梯度 —— 尾段虚空紫 (窄) → 头段冥焰红芯 (宽)
            Vector2 dir = Projectile.velocity.SafeNormalize(Projectile.rotation.ToRotationVector2());
            float headWidth = 22f * Projectile.scale;
            float beamLen = 90f * Projectile.scale;
            Vector2 head = Projectile.Center + dir * (18f * Projectile.scale);
            Vector2 tail = Projectile.Center - dir * beamLen;
            ACMShaders.DrawBeam(tail, Projectile.Center, headWidth * 0.45f,
                FengduVFX.VoidMid, FengduVFX.VoidDark, opacity,
                flowSpeed: 2.6f, flowScale: 2.4f, coreSharp: 2f);
            ACMShaders.DrawBeam(Projectile.Center, head, headWidth,
                new Color(255, 90, 100), new Color(140, 30, 60), opacity,
                flowSpeed: 2.6f, flowScale: 2.0f, coreSharp: 1.7f);

            Texture2D emberShards = ACMAsset.EmberShards;
            if (emberShards != null) {
                Vector2 emberOrigin = emberShards.Size() / 2f;
                Color emberColor = new Color(190, 90, 235) * opacity * 0.6f;
                emberColor.A = 0;
                float emberScale = 0.22f * Projectile.scale;
                Main.EntitySpriteDraw(emberShards, Projectile.Center - Main.screenPosition, null, emberColor, Timer * 0.1f, emberOrigin, emberScale, SpriteEffects.None, 0);
            }

            // 满纳波头的"冥龙魂首"辉核: BlankStar 双层反向旋转 + 辉光脉动
            if (FullBreath) {
                Texture2D star = ACMAsset.BlankStar;
                if (star != null) {
                    Vector2 starOrigin = star.Size() / 2f;
                    float pulse = 1f + MathF.Sin(Timer * 0.35f) * 0.18f;
                    float s = MathF.Min(0.3f * Projectile.scale, 1.1f) * pulse;
                    Color c1 = new Color(255, 90, 100) * (opacity * 0.85f);
                    c1.A = 0;
                    Color c2 = FengduVFX.VoidBright * (opacity * 0.6f);
                    c2.A = 0;
                    Main.EntitySpriteDraw(star, head - Main.screenPosition, null, c1, Timer * 0.18f, starOrigin, s, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(star, head - Main.screenPosition, null, c2, -Timer * 0.12f, starOrigin, s * 1.35f, SpriteEffects.None, 0);
                }
            }

            // 龙息炽芯
            WeaponVFX.DrawGlowBurst(head, Projectile.scale * (1f + MathF.Sin(Timer * 0.3f) * 0.15f),
                new Color(255, 110, 120) * (opacity * 0.85f));
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.3f }, Projectile.Center);
            // 消散爆发 (一次性 36 粒)
            for (int i = 0; i < 24; i++) {
                bool red = Main.rand.NextBool(3);
                Vector2 vel = Main.rand.NextVector2Circular(14f, 14f);
                Dust fire = Dust.NewDustPerfect(Projectile.Center, red ? DustID.Torch : DustID.PurpleTorch, vel, 60,
                    red ? new Color(255, 60, 80) : default, Main.rand.NextFloat(2.5f, 4f));
                fire.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 smokeVel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-8f, -2f));
                Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, smokeVel, 200, new Color(25, 8, 40), Main.rand.NextFloat(3f, 5f));
                smoke.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 毁灭印记 - 附着在敌人身上，2秒后引爆 (红=引爆预警的全局语言)。
    /// </summary>
    public class DragonAnnihilationMark : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/HellwyrmAnnihilationCannon";
        private ref float Timer => ref Projectile.ai[0];
        private ref float TargetWhoAmI => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            int targetIdx = (int)TargetWhoAmI;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active) {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIdx];
            Projectile.Center = target.Center;

            float pulse = MathF.Sin(Timer * 0.3f) * 0.5f + 0.5f;
            Lighting.AddLight(target.Center, 1.6f * pulse, 0.2f * pulse, 0.4f * pulse);

            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 30f;
                Dust mark = Dust.NewDustPerfect(pos, DustID.Torch, (target.Center - pos).SafeNormalize(Vector2.Zero) * 2f, 60,
                    FengduVFX.LethalRed, 1.5f);
                mark.noGravity = true;
            }

            if (Timer >= 120) {
                Projectile.friendly = true;
                Projectile.position -= new Vector2(120, 120);
                Projectile.width = 240;
                Projectile.height = 240;
                Projectile.Damage();

                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.8f, Pitch = -0.8f }, target.Center);

                // 延迟引爆: 致命红爆发 + 暗红紫染屏 (本武器签名全屏时刻)
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), target.Center, ACMWeaponBurst.LethalRed, 2.6f, Projectile.owner);
                if (Main.myPlayer == Projectile.owner)
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<DragonDetonationFlash>(), 0, 0f, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 6f);

                // 引爆爆发 (一次性 32 粒)
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(16f, 16f);
                    Dust ring = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 40,
                        FengduVFX.LethalRed, Main.rand.NextFloat(3f, 5f));
                    ring.noGravity = true;
                }
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);
                    vel.Y -= 4f;
                    Dust smoke = Dust.NewDustPerfect(target.Center, DustID.Smoke, vel, 200, new Color(40, 12, 50), Main.rand.NextFloat(3f, 5f));
                    smoke.noGravity = true;
                }

                Lighting.AddLight(target.Center, 3.5f, 0.8f, 1.5f);
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / 120f;
            Texture2D sparkle = ACMAsset.Sparkle;
            if (sparkle != null) {
                Vector2 origin = sparkle.Size() / 2f;
                // 倒计时渐变: 虚空亮紫 → 致命红
                Color sparkColor = Color.Lerp(FengduVFX.VoidBright, FengduVFX.LethalRed, progress) * (0.4f + progress * 0.6f);
                sparkColor.A = 0;
                float scale = 0.3f + progress * 0.5f;
                Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkColor, Timer * 0.2f, origin, scale, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 冥龙引爆演出 (纯视觉, 本地客户端): ElementalScreenTint 暗红紫幕 + RadialBloom 冥焰爆心。
    /// </summary>
    public class DragonDetonationFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 24;

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
        public override bool? CanDamage() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = MathHelper.Clamp(Projectile.timeLeft / (float)Life, 0f, 1f);

            Effect tintFx = ACMShaders.ElementalScreenTint;
            if (tintFx != null) {
                ACMShaders.SetCommonParams(tintFx, Projectile.Center, life);
                tintFx.Parameters["uTint"]?.SetValue(new Vector4(new Color(140, 20, 60).ToVector3(), 0.32f * life));
                tintFx.Parameters["uTint2"]?.SetValue(new Vector4(new Color(25, 8, 40).ToVector3(), 0f));
                tintFx.Parameters["uVignette"]?.SetValue(0.46f);
                tintFx.Parameters["uFogScale"]?.SetValue(2.3f);
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawFullscreenOverlay(tintFx, BlendState.AlphaBlend);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.24f, life * 0.9f, new Color(255, 90, 110), 12f);
            return false;
        }
    }
}
