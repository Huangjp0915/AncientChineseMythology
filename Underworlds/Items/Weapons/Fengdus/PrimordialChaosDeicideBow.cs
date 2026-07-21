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
    /// 混元吞天万羽坠神弓 - 终极远程弓
    /// 三档蓄力: 拉弦时身后显形"万羽阵列"羽箭虚影 (4/8/14), 松弦主箭射出后阵列逐支转为真实羽箭尾随齐射。
    /// 满弦主箭命中撕开混元之门, 降下堕天羽箭雨。
    /// </summary>
    public class PrimordialChaosDeicideBow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 5400;
            Item.crit = 24;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 36;
            Item.height = 80;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 10f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.shoot = ModContent.ProjectileType<DeicideBowHeld>();
            Item.shootSpeed = 28f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<DeicideBowHeld>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.MountedCenter,
                velocity.SafeNormalize(Vector2.UnitX * player.direction),
                ModContent.ProjectileType<DeicideBowHeld>(), damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<DamnedSoulguide>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 蓄力弓手持 - 三档拉弦 + 万羽阵列。
    /// ai[0]=蓄力 0-100 (0-24 快射 / 25-59 二档 / ≥60 满弦), ai[1]=状态 (0 拉弦, 1 齐射收招),
    /// ai[2]=释放档位。蓄力 &gt;72% 后阵列不再新增 (静默张力)。
    /// 羽箭虚影位置为各客户端本地演算, 真实羽箭只在 owner 端按虚影位置生成。
    /// </summary>
    public class DeicideBowHeld : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/PrimordialChaosDeicideBow";

        private const int MaxCharge = 100;
        private const int Tier2 = 25;
        private const int Tier3 = 60;
        private const float SilenceFrac = 0.72f;
        private const int MaxFeathers = 14;
        private const float TierFlashTime = 9f;

        private ref float Charge => ref Projectile.ai[0];
        private ref float State => ref Projectile.ai[1];
        private ref float ReleaseTier => ref Projectile.ai[2];
        private ref float VolleyTimer => ref Projectile.localAI[0];

        private readonly Vector2[] _featherPos = new Vector2[MaxFeathers];
        private readonly float[] _featherAge = new float[MaxFeathers];
        private int _featherCount;
        private int _featherFired;
        private int _tierReached = -1;
        private float _tierFlash;
        private Vector2 _releaseDir;
        private Vector2 _releaseFrom;

        private Player Owner => Main.player[Projectile.owner];
        private float Charge01 => MathHelper.Clamp(Charge / MaxCharge, 0f, 1f);

        private static int TierOf(float charge) => charge >= Tier3 ? 2 : charge >= Tier2 ? 1 : 0;
        private static int FeatherTarget(int tier) => tier == 2 ? 14 : tier == 1 ? 8 : 4;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.DeicideBowHeld.DisplayName",
                () => "Primordial Chaos Deicide Bow");
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 80;
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
                || Owner.HeldItem?.type != ModContent.ItemType<PrimordialChaosDeicideBow>()) {
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
            Projectile.Center = Owner.MountedCenter + dir * 16f;
            Owner.itemRotation = (dir * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);

            if (State == 0)
                ChargingAI(dir);
            else
                VolleyAI(dir);

            if (_tierFlash > 0f)
                _tierFlash--;

            Lighting.AddLight(Projectile.Center, 0.3f + 0.4f * Charge01, 0.2f + 0.3f * Charge01, 0.6f + 0.6f * Charge01);
        }

        /// <summary>身后扇形阵列的整列槽位 (随瞄准方向实时摆动, 奇偶双排错列)。</summary>
        private Vector2 SlotPos(int i, Vector2 dir) {
            int n = Math.Max(_featherCount, 1);
            float t = n == 1 ? 0.5f : i / (float)(n - 1);
            float ang = (t - 0.5f) * MathHelper.ToRadians(110f);
            float radius = 62f + (i % 2) * 26f;
            return Owner.MountedCenter - dir.RotatedBy(ang) * radius;
        }

        private void ChargingAI(Vector2 dir) {
            // 蓄力: 全端本地推进做预测, owner 权威; 档位跨越时 netUpdate 校正
            if (Charge < MaxCharge)
                Charge++;
            float charge01 = Charge01;

            int tier = TierOf(Charge);
            if (tier > _tierReached) {
                _tierReached = tier;
                _tierFlash = TierFlashTime;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.15f * tier }, Projectile.Center);
                if (Main.myPlayer == Projectile.owner)
                    Projectile.netUpdate = true;
            }

            // 万羽显形: 每 2 帧新增 1 支直至档位目标数; >72% 后不再新增 (静默张力)
            int target = FeatherTarget(tier);
            if (charge01 <= SilenceFrac && _featherCount < target && (int)Charge % 2 == 0) {
                int i = _featherCount++;
                _featherPos[i] = SlotPos(i, dir) + Main.rand.NextVector2Circular(90f, 70f);
                _featherAge[i] = 0f;
            }

            // 虚影从散开缓动聚拢到整列
            for (int i = 0; i < _featherCount; i++) {
                _featherPos[i] = Vector2.Lerp(_featherPos[i], SlotPos(i, dir), 0.1f);
                if (_featherAge[i] < 1f)
                    _featherAge[i] += 0.08f;
            }

            // 阵列环境微光 (≤2/帧)
            if (_featherCount > 0 && charge01 <= SilenceFrac && Main.rand.NextBool(2)) {
                Vector2 p = _featherPos[Main.rand.Next(_featherCount)];
                Dust d = Dust.NewDustPerfect(p + Main.rand.NextVector2Circular(8f, 8f), DustID.PurpleTorch,
                    Vector2.UnitY * -0.4f, 120, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }

            if (Main.myPlayer == Projectile.owner && !Owner.channel)
                Fire(dir);
        }

        /// <summary>松弦释放主箭 (仅 owner 端), 之后进入齐射收招段。</summary>
        private void Fire(Vector2 dir) {
            int tier = TierOf(Charge);
            float speed = tier == 2 ? 34f : tier == 1 ? 26f : 20f;
            float dmgMult = tier == 2 ? 4.5f : tier == 1 ? 2.2f : 1f;
            // 档位经 ai[1] 传递给主箭: 0=不开门 / 0.5=半时长门 / 1=满弦全门
            float gate = tier == 2 ? 1f : tier == 1 ? 0.5f : 0f;

            Vector2 muzzle = Projectile.Center + dir * 8f;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem), muzzle, dir * speed,
                ModContent.ProjectileType<ChaosDeicideArrow>(), (int)(Projectile.damage * dmgMult),
                Projectile.knockBack, Projectile.owner, 0f, gate);

            SoundEngine.PlaySound(SoundID.Item5 with {
                Volume = 0.9f + tier * 0.15f,
                Pitch = -0.4f + tier * 0.3f
            }, muzzle);
            if (tier == 2) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.2f }, muzzle);
                WeaponVFX.AddScreenShake(muzzle, 2f);
            }

            _releaseDir = dir;
            _releaseFrom = muzzle;
            State = 1;
            ReleaseTier = tier;
            VolleyTimer = 0f;
            _featherFired = 0;
            Projectile.netUpdate = true;
        }

        private void VolleyAI(Vector2 dir) {
            VolleyTimer++;
            int tier = (int)ReleaseTier;
            // 快射 (蓄力 <25) 无阵列齐射, 虚影直接消散
            int toFire = tier >= 1 ? _featherCount : 0;

            // 远端未经历 Fire(): 用同步的瞄准方向兜底
            if (_releaseDir == Vector2.Zero) {
                _releaseDir = dir;
                _releaseFrom = Projectile.Center + dir * 8f;
            }

            // 未发射的虚影维持聚拢队形
            for (int i = _featherFired; i < _featherCount; i++)
                _featherPos[i] = Vector2.Lerp(_featherPos[i], SlotPos(i, dir), 0.1f);

            // 每 2 帧一支: 虚影转为真实羽箭 (仅 owner 生成, 其余端只演消散)
            if (toFire > 0 && _featherFired < toFire && (int)VolleyTimer % 2 == 0) {
                int i = _featherFired;
                Vector2 from = _featherPos[i];

                if (Main.myPlayer == Projectile.owner) {
                    // 微前置预瞄: 各羽箭朝主箭弹道前方递进的汇聚点 + ±3° 微散
                    Vector2 lead = _releaseFrom + _releaseDir * (200f + i * 45f);
                    Vector2 shot = (lead - from).SafeNormalize(_releaseDir)
                        .RotatedByRandom(MathHelper.ToRadians(3f));
                    float speed = (tier == 2 ? 34f : 26f) * 0.8f;
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem), from, shot * speed,
                        ModContent.ProjectileType<ChaosRainArrow>(), (int)(Projectile.damage * 0.35f),
                        Projectile.knockBack * 0.3f, Projectile.owner);
                }

                // 虚影离位余韵 (事件性, 3 粒)
                for (int k = 0; k < 3; k++) {
                    Dust d = Dust.NewDustPerfect(from, DustID.PurpleTorch,
                        _releaseDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 5f), 100, default, 1.3f);
                    d.noGravity = true;
                }
                if (i % 2 == 0)
                    SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.35f, Pitch = 0.3f + i * 0.02f }, from);

                _featherFired++;
            }

            if (VolleyTimer >= 8f + toFire * 2f)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            Vector2 perp = new(-dir.Y, dir.X);
            float charge01 = Charge01;
            int tier = State == 0 ? TierOf(Charge) : (int)ReleaseTier;

            // 满弦 ±1.2px 微颤 (仅绘制层)
            Vector2 jitter = State == 0 && tier == 2
                ? Main.rand.NextVector2Circular(1.2f, 1.2f)
                : Vector2.Zero;
            Vector2 bowPos = Projectile.Center + jitter;

            // 弓弦: 两端 → 搭箭点, 越满越亮
            Vector2 tipUp = bowPos + perp * 30f + dir * 2f;
            Vector2 tipDown = bowPos - perp * 30f + dir * 2f;
            Vector2 nock = bowPos - dir * (2f + 12f * ACMUtils.QuadOut(charge01));
            float stringGlow = 0.3f + 0.5f * charge01;
            ACMShaders.DrawBeam(tipUp, nock, 2f, new Color(235, 225, 255), FengduVFX.VoidMid, stringGlow);
            ACMShaders.DrawBeam(tipDown, nock, 2f, new Color(235, 225, 255), FengduVFX.VoidMid, stringGlow);

            // 满弦弦光: 弓身两端短亮线
            if (State == 0 && tier == 2) {
                ACMShaders.DrawBeam(tipUp, tipUp + dir * 16f, 2.6f, FengduVFX.VoidBright, FengduVFX.VoidDark, 0.85f);
                ACMShaders.DrawBeam(tipDown, tipDown + dir * 16f, 2.6f, FengduVFX.VoidBright, FengduVFX.VoidDark, 0.85f);
            }

            // 万羽阵列虚影 + 搭箭 (加法半透)
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D lsh = ACMAsset.LightShot;
            if (lsh != null) {
                float featherRot = dir.ToRotation();
                float dissolve = State == 1 && tier < 1
                    ? MathHelper.Clamp(1f - VolleyTimer / 8f, 0f, 1f)
                    : 1f;
                for (int i = _featherFired; i < _featherCount; i++) {
                    float a = MathHelper.Clamp(_featherAge[i], 0f, 1f) * dissolve;
                    if (a <= 0.02f)
                        continue;
                    sb.Draw(lsh, _featherPos[i] - Main.screenPosition, null,
                        FengduVFX.VoidBright * (0.55f * a), featherRot, lsh.Size() * 0.5f,
                        new Vector2(0.5f, 0.14f), SpriteEffects.None, 0);
                }

                // 弦上搭着的主箭虚体 (随蓄力从紫到混元白)
                if (State == 0 && Charge > 2f) {
                    Color arrowCol = Color.Lerp(FengduVFX.VoidMid, new Color(240, 230, 255), charge01);
                    sb.Draw(lsh, nock + dir * 18f - Main.screenPosition, null,
                        arrowCol * (0.45f + 0.5f * charge01), featherRot, lsh.Size() * 0.5f,
                        new Vector2(0.6f, 0.14f), SpriteEffects.None, 0);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            // 弓本体 (贴图纵向, 旋转到瞄准方向, 反向垂直翻转)
            SpriteEffects fx = dir.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Main.spriteBatch.Draw(tex, bowPos - Main.screenPosition, null, lightColor,
                Projectile.rotation + MathHelper.PiOver2, tex.Size() * 0.5f, 1f, fx, 0f);

            // 到档瞬间光环闪
            if (_tierFlash > 0f) {
                float f = _tierFlash / TierFlashTime;
                WeaponVFX.DrawGlowBurst(bowPos + dir * 6f, 0.7f + (1f - f) * 0.5f, FengduVFX.VoidBright * (0.7f * f));
            }
            return false;
        }
    }

    /// <summary>
    /// 混元弑神冰矢 - 主弹幕 (直飞)。
    /// ai[1] 档位: 0=不开门 / 0.5=半时长混元之门 / 1=满弦 (全门 + 噩梦定调 + 震屏)。
    /// </summary>
    public class ChaosDeicideArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/PrimordialChaosDeicideBow";
        private ref float Timer => ref Projectile.ai[0];
        private float Gate => Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 22;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.6f, 0.45f, 1.4f);

            for (int i = 0; i < 3; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(8, 8),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    80, default, Main.rand.NextFloat(1.5f, 2.5f));
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                    4, 4, DustID.BlueTorch, 0f, -1f, 60, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 600);
            target.AddBuff(BuffID.ShadowFlame, 600);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.AbyssPurple, 1f, Projectile.owner);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 60, default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.2f }, Projectile.Center);

            // 撕裂混元之门的"破口"泛光
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.AbyssPurple, 2.2f, Projectile.owner);

            // 开门按档位: 满弦全门 + 噩梦定调, 二档半时长门, 一档不开门 (门生成仅 owner 端)
            if (Gate >= 0.5f && Main.myPlayer == Projectile.owner) {
                float zoneStart = Gate >= 1f ? 0f : 90f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<ChaosRainZone>(), Projectile.damage, 0f, Projectile.owner, zoneStart);
            }
            if (Gate >= 1f) {
                FengduVFX.SpawnNightmare(Projectile.GetSource_FromThis(), Projectile.Center, 0.55f, Projectile.owner);
                WeaponVFX.AddScreenShake(Projectile.Center, 6f);
            }

            // 消散爆发 (一次性 34 粒)
            for (int i = 0; i < 22; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f);
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                vel.Y -= 4f;
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, vel, 40, default, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 弑神冰矢: 双层 ribbon 拖尾 (外虚空黑紫 + 内混元白)
            WeaponVFX.DrawProjectileTrail(Projectile, 18f,
                FengduVFX.VoidDark * 0.95f, new Color(235, 225, 255),
                ACMAsset.SoftGlow, uvScroll: 0.06f, subdivisions: 3);

            // BeamGrad 主箭锋线 (混元白芯 + 虚空紫缘)
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - dir * 46f, Projectile.Center + dir * 14f, 11f,
                new Color(240, 230, 255), FengduVFX.VoidMid, 0.95f,
                flowSpeed: 2.8f, flowScale: 2.2f, coreSharp: 3f);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.2f, new Color(200, 175, 255) * 0.8f);
            return false;
        }
    }

    /// <summary>
    /// 混元之门 - 在命中点持续降下堕天羽箭。ai[0] 初值可设 90 (半时长门)。
    /// </summary>
    public class ChaosRainZone : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/PrimordialChaosDeicideBow";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            // ai[0] 初值 90 的半时长门在此提前到期 (剩余时长减半)
            if (Timer >= 180f) {
                Projectile.Kill();
                return;
            }

            // 羽箭雨仅 owner 端生成 (多人安全)
            if (Main.myPlayer == Projectile.owner && Timer % 6 == 0) {
                int arrowType = ModContent.ProjectileType<ChaosRainArrow>();
                for (int i = 0; i < 2; i++) {
                    Vector2 spawnPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-200f, 200f), -600f);
                    Vector2 vel = (Projectile.Center + Main.rand.NextVector2Circular(80f, 30f) - spawnPos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(18f, 26f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, vel, arrowType,
                        (int)(Projectile.damage * 0.4f), 2f, Projectile.owner);
                }
            }

            float progress = Timer / 180f;
            Lighting.AddLight(Projectile.Center, 0.6f * (1f - progress), 0.3f * (1f - progress), 1.2f * (1f - progress));

            for (int i = 0; i < 3; i++) {
                float angle = Timer * 0.1f + i * MathHelper.TwoPi / 3f;
                float radius = 80f + MathF.Sin(Timer * 0.05f) * 30f;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.3f) * radius;
                Dust vortex = Dust.NewDustPerfect(pos, DustID.PurpleTorch, (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f, 80, default, 1.5f);
                vortex.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = MathHelper.Clamp(Timer / 180f, 0f, 1f);
            // 用 timeLeft 推真实存活帧数, 兼容 ai[0] 初值 90 的半时长门
            float fadeIn = MathHelper.Clamp((180f - Projectile.timeLeft) / 18f, 0f, 1f);
            float opacity = fadeIn * (1f - progress * 0.5f);

            // 混元之门: 系列统一的虚空裂口语言 (FengduVoidRift decal)
            FengduVFX.DrawVoidRift(Projectile.Center, 150f, MathHelper.Clamp(opacity, 0f, 1f), 0.35f,
                0, FengduVFX.VoidMid, FengduVFX.VoidBright, seed: Projectile.whoAmI * 0.137f);

            // 门心暗芯 + 紫晕
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                Color dark = FengduVFX.VoidDark * (0.8f * opacity);
                dark.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, dark, 0f, origin, 2.4f + MathF.Sin(Timer * 0.12f) * 0.3f, SpriteEffects.None, 0);
                Color halo = FengduVFX.VoidBright * (opacity * 0.45f);
                halo.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, halo, 0f, origin, 3.4f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 堕天羽箭 - 从天而降的混元箭雨 / 万羽阵列齐射的真实羽箭。
    /// </summary>
    public class ChaosRainArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/PrimordialChaosDeicideBow";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.3f, 0.15f, 0.7f);

            Dust trail = Dust.NewDustDirect(
                Projectile.Center - Projectile.velocity * 0.3f, 4, 4, DustID.PurpleTorch,
                -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                100, default, Main.rand.NextFloat(1f, 1.5f));
            trail.noGravity = true;

            if (Main.rand.NextBool(3)) {
                Dust feather = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                    4, 4, DustID.BlueTorch, 0f, -0.5f, 80, default, 1f);
                feather.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 60, default, Main.rand.NextFloat(1.2f, 2f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 堕天羽箭: 轻量双层 ribbon 飘带 (外虚空紫 + 内混元白)
            WeaponVFX.DrawProjectileTrail(Projectile, 9f,
                FengduVFX.VoidMid * 0.8f, new Color(220, 210, 255),
                ACMAsset.SoftGlow, uvScroll: 0.08f, subdivisions: 1);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.7f, new Color(150, 110, 235) * 0.6f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    80, default, Main.rand.NextFloat(1f, 1.8f));
                death.noGravity = true;
            }
        }
    }
}
