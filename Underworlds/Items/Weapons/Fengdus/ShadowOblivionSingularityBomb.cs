using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 酆都系列共享 VFX 基座 (仅本系列消费): 统一调色板 + 专属着色器薄封装。
    /// 着色器经 <see cref="WeaponVFX.GetEffect"/> 按名缓存; 全屏噩梦定调走名额契约。
    /// </summary>
    public static class FengduVFX
    {
        // ===== 系列统一调色板 (虚空黑紫底座 + 帝阙金判决 + 致命红处决 + 魂青点缀) =====
        public static readonly Color VoidDark = new(25, 8, 40);
        public static readonly Color VoidMid = new(120, 60, 200);
        public static readonly Color VoidBright = new(180, 120, 255);
        public static readonly Color ImperialGold = new(230, 190, 90);
        public static readonly Color ImperialGoldHi = new(255, 230, 160);
        public static readonly Color LethalRed = new(250, 40, 56);
        public static readonly Color SoulCyan = new(80, 200, 220);

        // FengduVoidRift 为满屏 SDF decal, 同帧限 ≤2 张 (性能红线)
        private static ulong _riftFrame;
        private static int _riftCount;

        /// <summary>
        /// 屏幕空间虚空裂口 decal (FengduVoidRift.fx)。须在有活动批的阶段调用 (PreDraw 等)。
        /// 同帧超过 2 张自动跳过。mode: 0=圆形奇点裂口, 1=竖门(罗生门)。
        /// </summary>
        public static void DrawVoidRift(Vector2 worldCenter, float worldRadius, float intensity, float tear,
            int mode, Color edge, Color glow, float seed = 0f) {
            if (Main.dedServ || intensity <= 0.01f || worldRadius < 4f)
                return;
            if (_riftFrame != Main.GameUpdateCount) {
                _riftFrame = Main.GameUpdateCount;
                _riftCount = 0;
            }
            if (_riftCount >= 2)
                return;
            Effect fx = WeaponVFX.GetEffect("FengduVoidRift");
            if (fx == null)
                return;
            _riftCount++;

            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uv, out float rFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uTear"]?.SetValue(MathHelper.Clamp(tear, 0f, 1f));
            fx.Parameters["uMode"]?.SetValue((float)mode);
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uColorGlow"]?.SetValue(glow.ToVector4());
            fx.Parameters["uSeed"]?.SetValue(seed);
            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx);
        }

        /// <summary>
        /// 帝诏卷轴带 (FengduImperialDecree.fx, TriangleStrip 直带)。start=卷首(展开起点)。
        /// 须在有活动批的阶段调用; 顶点契约同 ACMShaders.DrawBeam。
        /// </summary>
        public static void DrawDecreeBand(Vector2 worldStart, Vector2 worldEnd, float halfWidth, float unroll,
            float intensity, float glyphFreq = 10f, float seed = 0f,
            Color? silk = null, Color? trim = null, Color? glyph = null) {
            if (Main.dedServ || intensity <= 0.01f || halfWidth < 0.5f)
                return;
            Effect fx = WeaponVFX.GetEffect("FengduImperialDecree");
            if (fx == null)
                return;

            Vector2 a = worldStart - Main.screenPosition;
            Vector2 b = worldEnd - Main.screenPosition;
            if ((b - a).LengthSquared() < 1f)
                return;
            var verts = ACMUtils.BuildRibbonStrip([a, b], _ => halfWidth, _ => Color.White, 0f, 1);
            if (verts.Length < 4)
                return;

            Color silkC = silk ?? new Color(30, 12, 52, 235);
            Color trimC = trim ?? ImperialGold;
            Color glyphC = glyph ?? ImperialGoldHi;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uUnroll"]?.SetValue(MathHelper.Clamp(unroll, 0f, 1f));
            fx.Parameters["uColorSilk"]?.SetValue(silkC.ToVector4());
            fx.Parameters["uColorTrim"]?.SetValue(trimC.ToVector4());
            fx.Parameters["uColorGlyph"]?.SetValue(glyphC.ToVector4());
            fx.Parameters["uGlyphFreq"]?.SetValue(glyphFreq);
            fx.Parameters["uSeed"]?.SetValue(seed);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = ACMShaders.NoiseTexture;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 生成一次全屏噩梦定调 (FengduNightmareFlash, ≤28 帧, 走全屏名额契约)。仅 owner 端生成。
        /// </summary>
        public static void SpawnNightmare(IEntitySource source, Vector2 worldPos, float strength, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<FengduNightmareFlash>(), 0, 0f, owner,
                MathHelper.Clamp(strength, 0f, 1f));
        }
    }

    /// <summary>
    /// 酆都噩梦全屏定调 (纯视觉, 本系列大招共用): FengduNightmare.fx 短暂后处理, 走名额契约。
    /// ai[0] = 强度 0~1。
    /// </summary>
    public class FengduNightmareFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 28;

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

        public override bool PreDraw(ref Color lightColor) => false;

        // 读 screenTarget 的全屏后处理统一放 PostDraw (仓库既有模式), 先取名额再应用
        public override void PostDraw(Color lightColor) {
            if (Main.dedServ || Main.gameMenu)
                return;
            float life = 1f - Projectile.timeLeft / (float)Life; // 0→1
            // 快起慢落包络: 前 15% 冲顶, 余下缓落
            float envelope = life < 0.15f ? life / 0.15f : 1f - (life - 0.15f) / 0.85f;
            float strength = MathHelper.Clamp(Projectile.ai[0], 0f, 1f) * envelope;
            if (strength < 0.02f || !ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = WeaponVFX.GetEffect("FengduNightmare");
            if (fx == null)
                return;
            ACMShaders.SetCommonParams(fx, Projectile.Center, strength);
            fx.Parameters["uPull"]?.SetValue(0.8f);
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.30f, 0.10f, 0.44f, 0.85f));
            ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, fx, bindNoise: true);
        }
    }

    /// <summary>
    /// 暗影寂灭终极奇点炸弹 - 终极投掷炸弹
    /// 投掷奇点核心: 落地/命中展开引力阱 (60 帧) 吸引敌人 → 大规模内爆;
    /// 阱在场时再次使用 = 提前引爆 (伤害按蓄力进度 0.6~1.0 缩放) — 聚怪充分 vs 立即伤害的决策点。
    /// 被内爆击杀的敌人产生连锁奇点回响。
    /// </summary>
    public class ShadowOblivionSingularityBomb : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 5000;
            Item.crit = 18;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 30;
            Item.height = 30;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 14f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<SingularityBombProj>();
            Item.shootSpeed = 14f;
            Item.consumable = false;
        }

        public override bool CanUseItem(Player player) {
            // 场上引力阱展开中 → 允许使用 (转为引爆指令); 否则限制同场 2 枚
            if (HasArmedWell(player))
                return true;
            return player.ownedProjectileCounts[ModContent.ProjectileType<SingularityBombProj>()] < 2;
        }

        private static bool HasArmedWell(Player player) {
            int type = ModContent.ProjectileType<SingularityBombProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && p.owner == player.whoAmI && p.ai[1] == 1f)
                    return true;
            }
            return false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 决策点: 有展开中的引力阱 → 本次使用变为"提前引爆"指令, 不投掷新弹
            bool detonated = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && p.owner == player.whoAmI && p.ai[1] == 1f && p.ai[2] < 1f) {
                    p.ai[2] = 1f;
                    p.netUpdate = true;
                    detonated = true;
                }
            }
            if (detonated) {
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.7f, Pitch = -0.5f }, player.Center);
                return false;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            player.velocity -= velocity.SafeNormalize(Vector2.Zero) * 2f; // 掷出反冲
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulShatteringUnderworldBomb>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 奇点炸弹弹体 - 两阶段: 引力阱 (FengduVoidRift 裂口) → 内爆 (前静默收缩 + 三层冲击环)。
    /// ai[0]=Timer, ai[1]=Phase, ai[2]=提前引爆信号。
    /// </summary>
    public class SingularityBombProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/ShadowOblivionSingularityBomb";

        private ref float Timer => ref Projectile.ai[0];
        private ref float Phase => ref Projectile.ai[1];
        private ref float DetonateSignal => ref Projectile.ai[2];
        private const int GravityDuration = 60;
        private const int CollapseFrames = 10;   // 内爆前静默/收缩帧数
        private const float GravityRadius = 500f;
        private const float GravityStrength = 8f;
        private const float ExplosionRadius = 300f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 126;
            Projectile.height = 126;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.alpha = 0;
        }

        // 飞行段允许接触判定 (命中即转引力阱); 阱展开后伤害全走 Detonate
        public override bool? CanDamage() => Phase == 0 ? null : false;

        public override void AI() {
            Timer++;

            if (Phase == 0) {
                Projectile.velocity.Y += 0.3f;
                Projectile.rotation += Projectile.velocity.X * 0.05f;

                if (Main.rand.NextBool(2)) {
                    Dust trail = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                        4, 4, DustID.PurpleTorch, -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                        120, default, 1.4f);
                    trail.noGravity = true;
                }
            }
            else if (Phase == 1) {
                Projectile.velocity = Vector2.Zero;
                Projectile.tileCollide = false;

                float wellProgress = Timer / GravityDuration;
                bool collapsing = Timer >= GravityDuration - CollapseFrames;

                // 吸引敌人 (进度越深拉力越强)
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy()) continue;
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < GravityRadius && dist > 15f) {
                        float pullMult = (1f - dist / GravityRadius) * (0.5f + wellProgress * 0.5f);
                        Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * GravityStrength * pullMult;
                        npc.velocity += pull;
                    }
                }

                // 吸积粒子: 密度 ∝ sqrt(progress), 末段 (collapse) 全剪 —— 内爆前的"吸气"静默
                if (!collapsing) {
                    int particleCount = 3 + (int)(MathF.Sqrt(wellProgress) * 10f);
                    for (int i = 0; i < particleCount; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float radius = Main.rand.NextFloat(60f, GravityRadius * (1f - wellProgress * 0.35f));
                        Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                        Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * (3f + wellProgress * 7f);
                        vel = vel.RotatedBy(0.45f); // 螺旋切向
                        int dustType = Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.PurpleTorch;
                        Dust spiral = Dust.NewDustPerfect(pos, dustType, vel, 80,
                            dustType == DustID.Shadowflame ? new Color(120, 40, 200) : default,
                            Main.rand.NextFloat(1.4f, 2.6f));
                        spiral.noGravity = true;
                    }
                }

                Lighting.AddLight(Projectile.Center, 0.4f * (1f - wellProgress), 0.1f, 0.6f * (1f - wellProgress));

                // 渐强低鸣 (collapse 段静默)
                if (!collapsing && Timer % 9 == 0) {
                    SoundEngine.PlaySound(SoundID.Item15 with {
                        Volume = 0.25f + wellProgress * 0.35f,
                        Pitch = -1f + wellProgress * 0.9f
                    }, Projectile.Center);
                }

                // 提前引爆信号 / 自然到时
                if (DetonateSignal >= 1f || Timer >= GravityDuration)
                    Detonate(MathHelper.Clamp(Timer / GravityDuration, 0f, 1f));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Phase == 0) ActivateGravityWell();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Phase == 0) ActivateGravityWell();
        }

        private void ActivateGravityWell() {
            Phase = 1;
            Timer = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
            SoundEngine.PlaySound(SoundID.Item104 with { Volume = 1.2f, Pitch = -0.8f }, Projectile.Center);

            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi / 24f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 60,
                    new Color(120, 40, 200), 2.4f);
                ring.noGravity = true;
            }
        }

        /// <summary>内爆。progress = 蓄力进度 (提前引爆时 &lt;1, 伤害 ×0.6~1.0)。</summary>
        private void Detonate(float progress) {
            Phase = 2;
            float damageScale = 0.6f + 0.4f * progress;

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 2f, Pitch = -1f + Main.rand.NextFloat(0.15f) }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.4f, Pitch = -0.5f }, Projectile.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.FengduVoid, 3f, Projectile.owner);
            if (Main.myPlayer == Projectile.owner)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<SingularityImplosionFlash>(), 0, 0f, Projectile.owner, damageScale);
            WeaponVFX.AddScreenShake(Projectile.Center, 8f + progress * 3f);

            // 伤害与连锁只在 owner 端结算 (多人安全)
            if (Main.myPlayer == Projectile.owner) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy()) continue;
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < ExplosionRadius) {
                        float distMult = 1f - (dist / ExplosionRadius) * 0.3f;
                        int dmg = (int)(Projectile.damage * 2 * distMult * damageScale);
                        int dir = npc.position.X > Projectile.Center.X ? 1 : -1;
                        npc.SimpleStrikeNPC(dmg, dir, true, 20f, null, false, 0, true);
                        npc.AddBuff(BuffID.ShadowFlame, 900);
                        npc.AddBuff(BuffID.BrokenArmor, 900);

                        // 连锁: 被击杀的敌人处生成奇点回响
                        if (npc.life <= 0) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), npc.Center, Vector2.Zero,
                                ModContent.ProjectileType<SingularityEcho>(), Projectile.damage, Projectile.knockBack * 0.5f, Projectile.owner);
                        }

                        Vector2 knockDir = (npc.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 16f;
                        npc.velocity += knockDir;
                    }
                }
            }

            // 内爆粒子: 外扩环 + 内核喷发 + 竖柱
            for (int i = 0; i < 36; i++) {
                float angle = MathHelper.TwoPi / 36f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(12f, 25f);
                int dustType = Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.PurpleTorch;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, dustType, vel, 40,
                    dustType == DustID.Shadowflame ? new Color(160, 60, 255) : default,
                    Main.rand.NextFloat(2.6f, 4.5f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 14; i++) {
                Dust upward = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30, 30),
                    DustID.Shadowflame, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(10f, 25f)),
                    40, new Color(160, 60, 255), Main.rand.NextFloat(2.4f, 4f));
                upward.noGravity = true;
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Phase == 0) {
                // 飞行段: 黑紫双层拖尾 + 本体
                WeaponVFX.DrawProjectileTrail(Projectile, 16f,
                    FengduVFX.VoidDark * 0.95f, FengduVFX.VoidBright,
                    ACMAsset.SoftGlow, uvScroll: 0.06f, subdivisions: 2);
                return true;
            }

            if (Phase == 1) {
                float wellProgress = MathHelper.Clamp(Timer / GravityDuration, 0f, 1f);
                float collapse = MathHelper.Clamp((Timer - (GravityDuration - CollapseFrames)) / (float)CollapseFrames, 0f, 1f);

                // 视界: FengduVoidRift 奇点裂口 (撕裂随进度增, 内爆前收缩 40% —— "变小再变响")
                float riftRadius = (95f + wellProgress * 75f) * (1f - 0.4f * collapse);
                FengduVFX.DrawVoidRift(Projectile.Center, riftRadius, 0.55f + wellProgress * 0.45f,
                    0.25f + wellProgress * 0.6f, 0, FengduVFX.VoidMid, FengduVFX.VoidBright,
                    seed: Projectile.whoAmI * 0.137f);

                // 奇点白紫芯 (collapse 段反而增亮 → 引爆预告)
                Texture2D blankStar = ACMAsset.BlankStar;
                if (blankStar != null) {
                    Vector2 starOrigin = blankStar.Size() / 2f;
                    float pulse = 0.09f + MathF.Sin(Timer * 0.4f) * 0.02f + wellProgress * 0.05f + collapse * 0.06f;
                    Color starColor = Color.Lerp(new Color(255, 200, 255), Color.White, collapse) * (0.6f + collapse * 0.4f);
                    starColor.A = 0;
                    Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor,
                        Timer * 0.15f, starOrigin, pulse, SpriteEffects.None, 0);
                }

                // 吸积冲击环 (向心收口 = 可读预警)
                float ringR = MathHelper.Lerp(GravityRadius * 0.5f, 36f, wellProgress);
                WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 18f, (0.3f + wellProgress * 0.5f) * (1f - collapse),
                    FengduVFX.VoidBright, FengduVFX.VoidDark);
            }

            return false;
        }

        // 签名时刻: GenericWarp 黑洞引力透镜 (60 帧短暂, 强度 charge³, 本武器唯一持续全屏后处理)
        public override void PostDraw(Color lightColor) {
            if (Main.dedServ || Main.gameMenu || Phase != 1)
                return;
            float wellProgress = MathHelper.Clamp(Timer / GravityDuration, 0f, 1f);
            float intensity = 0.35f + wellProgress * wellProgress * wellProgress * 0.65f;
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;
            ACMShaders.SetCommonParams(fx, Projectile.Center, intensity);
            fx.Parameters["uRadius"]?.SetValue(0.4f + wellProgress * 0.35f);
            fx.Parameters["uWarpScale"]?.SetValue(1.8f);
            fx.Parameters["uChroma"]?.SetValue(0.85f);
            fx.Parameters["uRadialPull"]?.SetValue(0.7f + wellProgress * 0.5f);
            fx.Parameters["uMode"]?.SetValue(4f);
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.22f, 0.08f, 0.4f, 0.6f));
            ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, fx, bindNoise: true);
        }
    }

    /// <summary>
    /// 奇点回响 - 连锁反应产生的次级奇点 (击杀连锁)。
    /// </summary>
    public class SingularityEcho : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/ShadowOblivionSingularityBomb";

        private ref float Timer => ref Projectile.ai[0];
        private const int EchoDuration = 30;
        private const float EchoRadius = 200f;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EchoDuration + 1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.alpha = 255;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float progress = Timer / EchoDuration;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < EchoRadius && dist > 10f) {
                    float pull = (1f - dist / EchoRadius) * 5f;
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * pull;
                }
            }

            // 末 6 帧静默 (微型 collapse)
            if (Timer < EchoDuration - 6) {
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(20f, EchoRadius * (1f - progress * 0.5f));
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 4f;
                    Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, vel, 80,
                        new Color(120, 40, 200), Main.rand.NextFloat(1.4f, 2.4f) * (1f - progress));
                    d.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.3f * (1f - progress), 0.1f, 0.4f * (1f - progress));

            if (Timer >= EchoDuration) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.3f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.FengduVoid, 1.4f, Projectile.owner);
                WeaponVFX.AddScreenShake(Projectile.Center, 3f);

                // 伤害仅 owner 端结算
                if (Main.myPlayer == Projectile.owner) {
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC npc = Main.npc[i];
                        if (!npc.CanBeChasedBy()) continue;
                        if (Vector2.Distance(Projectile.Center, npc.Center) < EchoRadius) {
                            npc.SimpleStrikeNPC(Projectile.damage, npc.position.X > Projectile.Center.X ? 1 : -1,
                                false, 12f, null, false, 0, true);
                            npc.AddBuff(BuffID.ShadowFlame, 300);
                        }
                    }
                }

                for (int i = 0; i < 14; i++) {
                    float angle = MathHelper.TwoPi / 14f * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(6f, 12f);
                    Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 40,
                        new Color(160, 60, 255), Main.rand.NextFloat(1.8f, 3f));
                    ring.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / EchoDuration;
            float opacity = 1f - progress;

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                float size = 0.5f + progress;
                Color dark = FengduVFX.VoidDark * 0.7f * opacity;
                dark.A = 120;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, dark,
                    0f, origin, size, SpriteEffects.None, 0);

                Color ring = FengduVFX.VoidMid * 0.45f * opacity;
                ring.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, ring,
                    0f, origin, size * 1.4f, SpriteEffects.None, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 奇点内爆演出 (纯视觉, 本地客户端): 1 帧白紫闪 → 三层时差冲击环 + 虚空染屏 + 内爆核泛光。
    /// ai[0] = 蓄力进度伤害缩放 (演出规模同步缩放)。
    /// </summary>
    public class SingularityImplosionFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 30;

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
            float scale = Projectile.ai[0] <= 0f ? 1f : MathHelper.Clamp(Projectile.ai[0], 0.6f, 1f);
            float life = MathHelper.Clamp(Projectile.timeLeft / (float)Life, 0f, 1f); // 1→0
            float age = 1f - life;                                                     // 0→1

            // 1 帧白紫闪 (首 2 帧)
            if (age < 0.08f)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 9f * scale, Color.Lerp(Color.White, FengduVFX.VoidBright, 0.4f) * 0.95f);

            // 虚空染屏 (短暂定调)
            Effect tintFx = ACMShaders.ElementalScreenTint;
            if (tintFx != null) {
                ACMShaders.SetCommonParams(tintFx, Projectile.Center, life);
                tintFx.Parameters["uTint"]?.SetValue(new Vector4(new Color(70, 24, 130).ToVector3(), 0.34f * life * scale));
                tintFx.Parameters["uTint2"]?.SetValue(new Vector4(new Color(12, 4, 26).ToVector3(), 0f));
                tintFx.Parameters["uVignette"]?.SetValue(0.55f);
                tintFx.Parameters["uFogScale"]?.SetValue(2.4f);
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawFullscreenOverlay(tintFx, BlendState.AlphaBlend);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 三层时差冲击环 (0/4/8 帧起步)
            for (int w = 0; w < 3; w++) {
                float wStart = w * (4f / Life);
                float wAge = MathHelper.Clamp((age - wStart) / (1f - wStart), 0f, 1f);
                if (wAge <= 0f) continue;
                float r = MathHelper.Lerp(20f, (270f - w * 50f) * scale, 1f - (1f - wAge) * (1f - wAge)); // ease-out 扩张
                float a = (1f - wAge) * (0.85f - w * 0.2f);
                WeaponVFX.DrawShockwaveRing(Projectile.Center, r, 14f - w * 3f, a,
                    FengduVFX.VoidBright, FengduVFX.VoidDark);
            }

            // 内爆核泛光 (走名额, 满则退化柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.26f * scale, life * 0.9f, FengduVFX.VoidBright, 12f);
            return false;
        }
    }
}
