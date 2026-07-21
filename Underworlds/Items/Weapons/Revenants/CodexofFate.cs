using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    // ================================================================
    //  亡魂系列·业秤共享框架 (因果判决)
    //  命中积业 → 七层业满 → 天降阎罗判决印 (KarmicVerdict) 结算重击。
    //  业力只在 owner 客户端记账 (OnHitNPC 天然 owner 侧, 各玩家各自记账);
    //  判决伤害走正常弹幕生成同步, 多人安全。宿主文件=生死冥罗录 (生死簿即业力账本)。
    // ================================================================

    /// <summary>业力层数容器 (InstancePerEntity)。层数/冷却仅在本地客户端有意义, 不做网络同步。</summary>
    public class RevenantKarmaGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>当前业力层数 (0~<see cref="RevenantKarma.MaxKarma"/>)。</summary>
        public int Karma;
        /// <summary>既决冷却: 宣判后一段时间不可再积业。</summary>
        public int SettleCooldown;
        /// <summary>无续叠计时 (超时业力缓慢消散)。</summary>
        public int DecayTimer;

        public override void PostAI(NPC npc) {
            if (SettleCooldown > 0)
                SettleCooldown--;
            if (Karma > 0) {
                DecayTimer++;
                // 8 秒无续叠后每 30 帧散去一层 (业报不会立刻遗忘, 但也不会永远等你)
                if (DecayTimer > 480 && DecayTimer % 30 == 0)
                    Karma--;
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (Karma <= 0 || Main.dedServ)
                return;

            // 业火苗可视化: 层数越高越密 (预算 ≤1 dust/2 帧); 第 6 层起混入朱红预警
            int spawnDenom = Karma >= 5 ? 2 : (Karma >= 3 ? 3 : 5);
            if (Main.rand.NextBool(spawnDenom)) {
                bool warn = Karma >= RevenantKarma.MaxKarma - 1 && Main.rand.NextBool(2);
                Color flame = warn ? new Color(250, 60, 70)
                    : (Main.rand.NextBool() ? new Color(120, 240, 210) : new Color(255, 220, 120));
                Vector2 pos = npc.Center + new Vector2(
                    Main.rand.NextFloat(-0.45f, 0.45f) * npc.width,
                    Main.rand.NextFloat(-0.1f, 0.5f) * npc.height);
                Dust d = Dust.NewDustPerfect(pos, DustID.RainbowMk2, new Vector2(0f, -1.4f - Karma * 0.12f),
                    120, flame, 0.9f + Karma * 0.08f);
                d.noGravity = true;
                d.fadeIn = 0.4f;
            }
        }
    }

    /// <summary>
    /// 业秤静态入口: 亡魂系列武器在 OnHitNPC 中调用 <see cref="AddKarma"/> 积业;
    /// 业满自动生成 <see cref="KarmicVerdict"/> 宣判 (弹幕伤害 ×1.5 的小范围结算重击)。
    /// </summary>
    public static class RevenantKarma
    {
        /// <summary>业力上限 (七七审期之数), 满层即宣判。</summary>
        public const int MaxKarma = 7;
        /// <summary>宣判后既决冷却帧。</summary>
        public const int SettleLockout = 90;
        /// <summary>宣判结算伤害倍率 (基于触发该击的弹幕伤害)。</summary>
        public const float VerdictMult = 1.5f;

        /// <summary>
        /// 命中积业 (仅 owner 客户端生效)。业满时在目标处天降判决印。
        /// </summary>
        /// <param name="proj">造成本次命中的弹幕 (宣判伤害/来源取自它)。</param>
        /// <param name="target">受击目标。</param>
        /// <param name="amount">积业层数 (普通 1, 重击 2~3)。</param>
        public static void AddKarma(Projectile proj, NPC target, int amount) {
            if (Main.dedServ || proj == null || target == null || amount <= 0)
                return;
            if (proj.owner != Main.myPlayer)
                return;
            if (!target.active || target.friendly || target.dontTakeDamage || target.life <= 0)
                return;

            var g = target.GetGlobalNPC<RevenantKarmaGlobalNPC>();
            if (g.SettleCooldown > 0)
                return;

            int before = g.Karma;
            g.Karma = Math.Min(g.Karma + amount, MaxKarma);
            g.DecayTimer = 0;

            if (g.Karma > before) {
                // 记账反馈: 音高随层数上行 (听觉进度条) + 一撮金青业火
                SoundEngine.PlaySound(SoundID.Item25 with {
                    Volume = 0.32f, Pitch = -0.25f + g.Karma * 0.09f, MaxInstances = 3
                }, target.Center);
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        DustID.RainbowMk2, new Vector2(0, -1.8f), 110,
                        g.Karma >= MaxKarma - 1 ? new Color(250, 90, 90) : new Color(160, 245, 200), 1.05f);
                    d.noGravity = true;
                }
            }

            if (g.Karma >= MaxKarma)
                Settle(proj, target, g);
        }

        /// <summary>业满宣判: 清零业力、上既决锁, 天降判决印 (真伤害弹幕, 正常联机同步)。</summary>
        private static void Settle(Projectile proj, NPC target, RevenantKarmaGlobalNPC g) {
            g.Karma = 0;
            g.SettleCooldown = SettleLockout;

            int dmg = Math.Max(1, (int)(proj.damage * VerdictMult));
            Projectile.NewProjectile(proj.GetSource_OnHit(target), target.Center, Vector2.Zero,
                ModContent.ProjectileType<KarmicVerdict>(), dmg, 6f, proj.owner);
        }
    }

    /// <summary>
    /// 阎罗判决印 — 业满宣判的结算弹幕 (真伤害 + 演出一体, 全客户端可见)。
    /// 落印节拍: 印记自上压下 (前 8 帧) → 盖印瞬间 AoE 结算 + 冲击环/辉光/震屏 → 勾决环扫过 → 业火散逸淡出。
    /// 印记本体走专属着色器 RevenantJudgmentSigil (屏幕空间贴花, 非全屏后处理, 不占名额)。
    /// </summary>
    public class KarmicVerdict : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int Life = 44;
        private const int StampFrame = 8;      // 落印帧 (伤害窗口)
        private const float HitRadius = 130f;  // 结算半径

        private ref float Timer => ref Projectile.ai[0];
        /// <summary>规模倍率 (ai[1], 默认 1; 断业刀等大宣判可放大)。</summary>
        private float Scale => Projectile.ai[1] <= 0f ? 1f : Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = (int)(HitRadius * 2);
            Projectile.height = (int)(HitRadius * 2);
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每目标只结算一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Timer >= StampFrame && Timer <= StampFrame + 3;

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Timer++;

            Lighting.AddLight(Projectile.Center, 0.9f, 0.35f, 0.3f);

            // 落印瞬间: 冲击反馈栈 (震屏 + 双层音效 + 尘环), 本地视觉路径
            if (!Main.dedServ && (int)Timer == StampFrame) {
                WeaponVFX.AddScreenShake(Projectile.Center, 4f * Scale);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.9f, Pitch = -0.2f + Main.rand.NextFloat(0.1f)
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with {
                    Volume = 0.5f, Pitch = 0.4f + Main.rand.NextFloat(0.15f)
                }, Projectile.Center);

                for (int i = 0; i < 14; i++) {
                    float ang = MathHelper.TwoPi / 14f * i;
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2,
                        ang.ToRotationVector2() * Main.rand.NextFloat(4f, 8f) * Scale, 100,
                        Main.rand.NextBool() ? new Color(250, 70, 70) : new Color(255, 220, 130), 1.4f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = Timer / Life;                                        // 0→1
            float stampT = MathHelper.Clamp(Timer / StampFrame, 0f, 1f);      // 落印进度
            float descend = 1f - MathF.Pow(stampT, 3f);                       // t³: 缓起猛落
            float fadeOut = MathHelper.Clamp(1f - (life - 0.72f) / 0.28f, 0f, 1f);

            // —— 落印前: 天降判决光柱 (自上而下收束) ——
            if (stampT < 1f) {
                float beamAlpha = (0.35f + stampT * 0.65f) * 0.9f;
                ACMShaders.DrawBeam(Projectile.Center - new Vector2(0f, 520f * (0.4f + descend)),
                    Projectile.Center + new Vector2(0f, 16f),
                    halfWidth: (26f - stampT * 14f) * Scale,
                    core: new Color(255, 230, 170), edge: new Color(230, 60, 60),
                    intensity: beamAlpha, flowSpeed: 3.2f, flowScale: 2.0f, coreSharp: 2.8f);
            }

            // —— 判决官印 (RevenantJudgmentSigil 屏幕空间贴花) ——
            Effect fx = WeaponVFX.GetEffect("RevenantJudgmentSigil");
            if (fx != null) {
                float radius = 92f * Scale * (1f + descend * 0.55f);
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue((0.25f + stampT * 0.75f) * fadeOut);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(new Color(250, 60, 70).ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(new Color(140, 245, 200).ToVector4());
                fx.Parameters["uStamp"]?.SetValue(MathHelper.Clamp((Timer - StampFrame) / (Life * 0.55f), 0f, 1f));

                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // —— 盖印后的冲击环 + 辉光 (短促) ——
            if (Timer >= StampFrame) {
                float post = MathHelper.Clamp((Timer - StampFrame) / 14f, 0f, 1f);
                if (post < 1f) {
                    WeaponVFX.DrawShockwaveRing(Projectile.Center, (20f + post * 130f) * Scale, 13f * Scale,
                        (1f - post) * 0.9f, new Color(255, 220, 130), new Color(250, 60, 70));
                    WeaponVFX.DrawRadialBloom(Projectile.Center, 0.075f * Scale, (1f - post) * 0.8f,
                        new Color(255, 110, 100), 4f);
                }
            }

            return false;
        }
    }

    // ================================================================
    //  生死冥罗录 — "记名" (魔法书)
    // ================================================================

    /// <summary>
    /// 生死冥罗录 - 记载众生死期与因果的冥府秘典 (魔法书)。
    /// 左键释放 2 道命运符文 (命中 +1 业); 每第 4 次施法翻至朱批页,
    /// 追加 1 发朱批敕令 (×1.6 快弹, 命中 +2 业并链电邻敌)。业满宣判见 <see cref="RevenantKarma"/>。
    /// </summary>
    public class CodexofFate : ModItem
    {
        /// <summary>施法计数 (owner 侧, 每 4 次翻出朱批页)。</summary>
        private int castCounter;

        public override void SetDefaults() {
            Item.damage = 52;
            Item.crit = 6;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 10;
            Item.width = 36;
            Item.height = 36;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<FateRuneProjectile>();
            Item.shootSpeed = 14f;
            Item.staff[Type] = true;
        }

        public override void HoldItem(Player player) {
            // 下一击将出朱批: 书页泛朱预告 (决策点可读)
            if (castCounter == 3 && !Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(26f, 26f),
                    DustID.RainbowMk2, new Vector2(0f, -1.2f), 120, new Color(250, 80, 80), 1.0f);
                d.noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            castCounter++;

            // 2 道命运符文
            for (int i = 0; i < 2; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(10));
                perturbedSpeed *= Main.rand.NextFloat(0.92f, 1.08f);
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }

            // 每第 4 次施法: 翻至朱批页, 追加 1 发朱批敕令
            bool rebuke = castCounter >= 4;
            if (rebuke) {
                castCounter = 0;
                Projectile.NewProjectile(source, position, velocity * 1.55f,
                    ModContent.ProjectileType<VermilionRebuke>(), (int)(damage * 1.6f), knockback, player.whoAmI);
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = 0.55f }, position);
                SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.5f, Pitch = 0.2f }, position);
            }

            // 施法翻页粒子 (朱批帧染红)
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                Dust page = Dust.NewDustPerfect(position,
                    rebuke ? DustID.RedTorch : DustID.PurpleTorch, vel,
                    100, default, Main.rand.NextFloat(0.8f, 1.2f));
                page.noGravity = true;
            }

            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position = player.Center + velocity.SafeNormalize(Vector2.Zero) * 25f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 命运符文弹幕 - 飞行的冥府符文 (命中 +1 业)。
    /// 双层带状拖尾 + 程序化 <see cref="ACMShaders.ArenaRunic"/> 符文环 (每帧仅一枚承担全屏法阵绘制)。
    /// </summary>
    public class FateRuneProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/CodexofFate";

        private ref float RotationTimer => ref Projectile.ai[0];

        // 每帧只允许一枚符文绘制全屏 ArenaRunic 法阵环 (开销护栏: 不占用全屏后处理名额, 仅本类内部节流)
        private static ulong _lastRuneRingFrame;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            RotationTimer++;
            Projectile.rotation += 0.15f;

            Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.6f);

            // 微弱追踪 (转向更平滑)
            if (RotationTimer > 15f) {
                NPC target = FindClosestNPC(350f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.045f);
                }
            }

            // 符文粒子拖尾
            if (Main.rand.NextBool(2)) {
                Dust rune = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(8, 8),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    100, default, Main.rand.NextFloat(0.8f, 1.3f));
                rune.noGravity = true;
            }
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 120);

            // 记名: +1 业
            RevenantKarma.AddKarma(Projectile, target, 1);

            // 冥紫爆发
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f));
                burst.noGravity = true;
            }

            // 命中冲击演出 (径向辉光 + 冲击环), 走 ACMWeaponBurst 暗冥紫主题
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.2f : 0.85f, owner: Projectile.owner);

            SoundEngine.PlaySound(SoundID.Item94 with {
                Volume = 0.45f, Pitch = 0.2f + Main.rand.NextFloat(-0.1f, 0.1f)
            }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 双层带状拖尾 (外宽暗冥紫 + 内窄亮紫芯)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: new Color(90, 40, 150, 150), innerColor: new Color(190, 120, 255, 200),
                uvScroll: RotationTimer * 0.02f);

            // 程序化符文环 (ArenaRunic 法阵): 每帧仅一枚承担全屏绘制, 其余退化为廉价星光
            bool drawRuneRing = false;
            if (_lastRuneRingFrame != Main.GameUpdateCount) {
                _lastRuneRingFrame = Main.GameUpdateCount;
                drawRuneRing = true;
            }

            if (drawRuneRing) {
                Effect fx = ACMShaders.ArenaRunic;
                if (fx != null) {
                    SpriteBatch sb = Main.spriteBatch;
                    ACMShaders.WorldDecalParams(Projectile.Center, 34f, out Vector2 uv, out float rFrac, out float aspect);
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uCenter"]?.SetValue(uv);
                    fx.Parameters["uRadius"]?.SetValue(rFrac);
                    fx.Parameters["uIntensity"]?.SetValue(0.7f);
                    fx.Parameters["uAspect"]?.SetValue(aspect);
                    fx.Parameters["uColorPrimary"]?.SetValue(new Color(190, 130, 255).ToVector4());
                    fx.Parameters["uColorSecondary"]?.SetValue(new Color(80, 35, 150).ToVector4());
                    fx.Parameters["uRuneFreq"]?.SetValue(12f);
                    fx.Parameters["uMode"]?.SetValue(0f);
                    fx.Parameters["uShape"]?.SetValue(0f);

                    sb.End();
                    ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                    ACMShaders.RestoreDefaultBatch(sb);
                }
            }
            else {
                // 廉价星光环 (本帧法阵名额被其它符文占用时的退化表现)
                Texture2D blankStar = ACMAsset.BlankStar;
                if (blankStar != null) {
                    Vector2 starOrigin = blankStar.Size() / 2f;
                    Color starColor = new Color(200, 150, 255) * 0.5f;
                    starColor.A = 0;
                    float starScale = 0.2f + MathF.Sin(RotationTimer * 0.3f) * 0.05f;
                    Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, RotationTimer * 0.15f, starOrigin, starScale, SpriteEffects.None, 0);
                }
            }

            // 符文核心光球 (SoftGlow 呼吸脉动)
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                Color mainGlow = new Color(180, 100, 255) * 0.6f;
                mainGlow.A = 0;
                float pulse = 0.55f + MathF.Sin(RotationTimer * 0.2f) * 0.08f;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, mainGlow, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.3f }, Projectile.Center);

            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 朱批敕令 - 每第 4 次施法翻出的朱红快弹 (×1.6): 命中 +2 业,
    /// 并向邻敌链电 (真伤害 30% ×3 跳 + <see cref="FateJudgmentField"/> 朱红链电演出)。
    /// </summary>
    public class VermilionRebuke : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/CodexofFate";

        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.8f, 0.25f, 0.2f);

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center - Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.RedTorch, -Projectile.velocity * 0.1f, 100, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 180);

            // 朱批重记: +2 业
            RevenantKarma.AddKarma(Projectile, target, 2);

            // 链电邻敌: 真伤害 30% ×3 跳 (可视化由 FateJudgmentField 的折线电链承担)
            int hops = 0;
            var used = new HashSet<int>();
            Vector2 cursor = target.Center;
            for (int hop = 0; hop < 3; hop++) {
                int best = -1;
                float bestDist = 260f;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy() || npc.whoAmI == target.whoAmI || used.Contains(i))
                        continue;
                    float d = Vector2.Distance(cursor, npc.Center);
                    if (d < bestDist) {
                        bestDist = d;
                        best = i;
                    }
                }
                if (best < 0)
                    break;
                used.Add(best);
                cursor = Main.npc[best].Center;
                Main.npc[best].SimpleStrikeNPC((int)(damageDone * 0.3f), hit.HitDirection, false, 0f, null, false, 0, true);
                hops++;
            }

            // 朱红链电演出 + 判决级反馈
            FateJudgmentField.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner, vermilion: true);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: 1.25f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, hops > 0 ? 3f : 2f);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = -0.1f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 朱红细拖尾 (外暗红 + 内亮金)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
                outerColor: new Color(150, 20, 30, 160), innerColor: new Color(255, 190, 120, 210),
                uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);

            // 朱批弹芯 (LightShot 拉长光弹)
            Texture2D lightShot = ACMAsset.LightShot;
            if (lightShot != null) {
                Vector2 origin = lightShot.Size() / 2f;
                Color c = new Color(255, 90, 80) * 0.9f;
                c.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, c,
                    Projectile.rotation, origin, new Vector2(0.62f, 0.4f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f, Pitch = 0.1f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 100, default, Main.rand.NextFloat(1.0f, 1.5f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 命运链电演出弹幕 (纯视觉, damage=0): 在命中点展开 <see cref="ACMShaders.ArenaRunic"/> 判词法阵环,
    /// 并以 <see cref="ACMShaders.DrawBeam"/> 在命中点 → 邻近敌群之间拉出折线电链。
    /// ai[0]=1 时为朱批主题 (判决红), 否则冥紫。
    /// </summary>
    public class FateJudgmentField : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 30;
        private const float RingRadius = 200f;
        private const float ChainRange = 260f;

        private bool Vermilion => Projectile.ai[0] > 0.5f;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner, bool vermilion = false) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<FateJudgmentField>(), 0, 0f, owner, vermilion ? 1f : 0f);
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

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.25f, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;             // 0→1
            float fade = MathHelper.Clamp(life < 0.2f ? life / 0.2f : 1f - (life - 0.2f) / 0.8f, 0f, 1f);

            Color ringA = Vermilion ? new Color(255, 140, 120) : new Color(190, 130, 255);
            Color ringB = Vermilion ? new Color(150, 20, 40) : new Color(80, 35, 150);
            Color beamCore = Vermilion ? new Color(255, 190, 160) : new Color(210, 170, 255);
            Color beamEdge = Vermilion ? new Color(200, 40, 50) : new Color(110, 55, 215);

            SpriteBatch sb = Main.spriteBatch;

            //—— ArenaRunic 判词法阵环 (扩张 + 呼吸) ——
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                float radius = RingRadius * (0.5f + life * 0.5f);
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(fade * 0.85f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(ringA.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(ringB.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(13f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);

                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            //—— DrawBeam 折线电链: 命中点 → 最近邻敌依次跳跃 (polyline, 最多 5 跳) ——
            var nodes = new List<Vector2> { Projectile.Center };
            var used = new HashSet<int>();
            Vector2 cursor = Projectile.Center;
            for (int hop = 0; hop < 5; hop++) {
                int best = -1;
                float bestDist = ChainRange;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || used.Contains(i))
                        continue;
                    float d = Vector2.Distance(cursor, npc.Center);
                    if (d < bestDist) {
                        bestDist = d;
                        best = i;
                    }
                }
                if (best < 0)
                    break;
                used.Add(best);
                cursor = Main.npc[best].Center;
                nodes.Add(cursor);
            }

            for (int i = 0; i < nodes.Count - 1; i++) {
                ACMShaders.DrawBeam(nodes[i], nodes[i + 1], 6f * fade,
                    beamCore, beamEdge, fade * 0.9f,
                    flowSpeed: 3.4f, flowScale: 3.2f, coreSharp: 2.6f);
            }

            //—— 中心核辉光 (峰值期申请全屏名额, 退化为柔光) ——
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.07f, fade * 0.65f,
                    Vermilion ? new Color(255, 120, 100) : new Color(170, 120, 250), 8f);

            return false;
        }
    }
}
