using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bosses
{
    /// <summary>
    /// 旱日 — 旱魃掉落魔法书。左键在光标处降下"灼日之炷"并点燃焦土;
    /// 每第 4 次施法引坠小焦日, 落地掀双向燎原火波。
    /// 机制为旱魃"烈日灼柱 / 焚天坠日 / 焦痕延燃"的玩家化直译 (Docs/WeaponRedo/BossScatter.md §3.1)。
    /// </summary>
    public class HanbaBook : ModItem
    {
        private int castCount; // 仅 owner 端 Shoot 消费 (基准 TidecallersDecree 同模式)

        public override void SetDefaults() {
            Item.useTime = Item.useAnimation = 32;
            Item.mana = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.autoReuse = true;
            Item.value = 2000;
            Item.rare = ItemRarityID.Red;
            Item.damage = 145;
            Item.DamageType = DamageClass.Magic;
            Item.UseSound = SoundID.Item74;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<HanbaBookProj>();
            Item.shootSpeed = 1f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            // 目标点 = 光标 (限距 1100px)
            Vector2 target = Main.MouseWorld;
            Vector2 toTarget = target - player.Center;
            const float maxRange = 1100f;
            if (toTarget.Length() > maxRange)
                target = player.Center + toTarget.SafeNormalize(Vector2.UnitX) * maxRange;

            if (++castCount >= 4) {
                castCount = 0;
                // 大招·坠日: 光标上方凝聚小焦日, 锁定当前地面线坠落
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.45f, Volume = 1.2f }, target);
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.25f, Volume = 0.8f }, target);
                Projectile.NewProjectile(source, target - new Vector2(0f, 560f), Vector2.Zero,
                    ModContent.ProjectileType<HanbaBookFallingSun>(), (int)(damage * 2.6f), knockback * 2f,
                    player.whoAmI, target.Y);
            }
            else {
                Projectile.NewProjectile(source, target, Vector2.Zero, type, damage, knockback, player.whoAmI);
            }
            return false;
        }
    }

    /// <summary>共享绘制: 程序化焦日日轮 (HanbaBookSunFlare.fx, 本武器专属)。</summary>
    internal static class HanbaBookVFX
    {
        /// <summary>须在有活动批的阶段调用 (PreDraw)。shader 缺失时退化为廉价柔光。</summary>
        public static void DrawSunDisk(Vector2 worldCenter, float radiusPx, float intensity, float collapse) {
            if (Main.dedServ || intensity <= 0.01f || radiusPx < 2f)
                return;
            Effect fx = WeaponVFX.GetEffect("HanbaBookSunFlare");
            Texture2D quad = ACMAsset.SoftGlow;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || quad == null || noise == null) {
                WeaponVFX.DrawGlowBurst(worldCenter, radiusPx / 22f, new Color(255, 170, 60) * intensity);
                return;
            }

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uCollapse"]?.SetValue(MathHelper.Clamp(collapse, 0f, 1f));
            fx.Parameters["uColorHot"]?.SetValue(new Color(255, 244, 205).ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(new Color(255, 118, 28).ToVector4());

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            float scale = radiusPx * 2f / quad.Width;
            sb.Draw(quad, worldCenter - Main.screenPosition, null, Color.White, 0f, quad.Size() * 0.5f,
                scale, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>光标点向下扫描实地 (tile 数), 命中返回地表世界 Y。</summary>
        public static bool TryFindGround(Vector2 from, int maxTiles, out float groundY) {
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            for (int i = 0; i < maxTiles; i++) {
                Tile t = Framing.GetTileSafely(tx, ty + i);
                if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    groundY = (ty + i) * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }
    }

    /// <summary>
    /// 灼日之炷 — 预警 20f (细橙线 + 聚焦收拢) → 爆发 10f (poly-snap 白热柱) → 收束 22f。
    /// 柱脚有实地时点燃焦土阴燃带。类名保留 (本地化/掉落契约)。
    /// </summary>
    public class HanbaBookProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int WarnTime = 20;
        private const int BurstTime = 10;
        private const int RecoverTime = 22;
        private const float PillarTop = 620f;    // 柱顶相对打击点上移
        private const float PillarBottom = 90f;  // 柱底相对打击点下探
        private const float HitHalfWidth = 40f;  // 判定半宽 (与爆发视觉 42 对齐)

        private float Timer => Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = WarnTime + BurstTime + RecoverTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40; // 一根柱对同一目标只结算一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.ai[0]++;

            if (Timer < WarnTime) {
                // 预警: 灰烬向打击点收拢 (密度渐增)
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    float pull = 1f - Timer / WarnTime;
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2CircularEdge(30f + 90f * pull, 30f + 90f * pull);
                    Dust d = Dust.NewDustPerfect(pos, DustID.Torch, (Projectile.Center - pos) * 0.09f, 120,
                        new Color(255, 160, 60), 1.4f);
                    d.noGravity = true;
                }
            }
            else if (Timer == WarnTime) {
                // 爆发帧: 音效双层 + 震屏 + 一次性灰烬喷腾
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.15f, Volume = 1.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.35f, Volume = 0.9f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 3f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 18; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-36f, 36f), Main.rand.NextFloat(-10f, 10f)),
                            DustID.Torch, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-9f, -3f)), 100,
                            new Color(255, 200, 90), Main.rand.NextFloat(1.6f, 2.6f));
                        d.noGravity = true;
                    }
                }
                // 柱脚点燃焦土 (owner 端生成, 30% 伤害档)
                if (Projectile.owner == Main.myPlayer
                    && HanbaBookVFX.TryFindGround(Projectile.Center, 15, out float groundY)) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        new Vector2(Projectile.Center.X, groundY - 14f), Vector2.Zero,
                        ModContent.ProjectileType<HanbaBookScorchBrand>(),
                        (int)(Projectile.damage * 0.3f), 0f, Projectile.owner);
                }
            }
            else if (Timer < WarnTime + BurstTime && !Main.dedServ && Main.rand.NextBool(2)) {
                // 爆发期: 柱身内零星白热碎屑上涌
                float y = Main.rand.NextFloat(-PillarTop * 0.9f, PillarBottom * 0.6f);
                Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), y),
                    DustID.GoldFlame, new Vector2(0f, Main.rand.NextFloat(-6f, -2f)), 120, default, 1.7f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.55f, 0.2f) * (Timer >= WarnTime ? 1.1f : 0.4f));
        }

        public override bool? CanDamage() => Timer > WarnTime && Timer <= WarnTime + BurstTime;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            var pillar = new Rectangle((int)(Projectile.Center.X - HitHalfWidth), (int)(Projectile.Center.Y - PillarTop),
                (int)(HitHalfWidth * 2f), (int)(PillarTop + PillarBottom));
            return pillar.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);
            WeaponVFX.AddScreenShake(target.Center, 2f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Scorch, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 top = Projectile.Center - new Vector2(0f, PillarTop);
            Vector2 bottom = Projectile.Center + new Vector2(0f, PillarBottom);

            if (Timer <= WarnTime) {
                // 预警细线: 橙 → 末 6f 掺红 (致命预警语义), 亮度脉动
                float t = Timer / WarnTime;
                float warnRed = MathHelper.Clamp((Timer - (WarnTime - 6f)) / 6f, 0f, 1f);
                Color core = Color.Lerp(new Color(255, 190, 90), TelegraphColors.Lethal, warnRed);
                float pulse = 0.55f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 26f);
                ACMShaders.DrawBeam(top, bottom, 2.6f, core with { A = 170 },
                    new Color(140, 45, 10, 90), pulse * (0.35f + 0.65f * t), coreSharp: 2f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.7f + 0.9f * t, new Color(255, 150, 55) * (0.5f * t));
            }
            else {
                // 爆发→收束: poly(8) snap 到全宽, 再指数回落
                float bt = MathHelper.Clamp((Timer - WarnTime) / BurstTime, 0f, 1f);
                float rt = MathHelper.Clamp((Timer - WarnTime - BurstTime) / (float)RecoverTime, 0f, 1f);
                float snap = 1f - MathF.Pow(1f - bt, 8f);
                float halfW = MathHelper.Lerp(3f, 42f, snap) * MathF.Pow(0.9f, rt * RecoverTime * 0.45f);
                float intensity = (0.55f + 0.45f * snap) * (1f - rt * rt);
                if (intensity > 0.02f) {
                    ACMShaders.DrawBeam(top, bottom, halfW,
                        new Color(255, 242, 200, 235), new Color(255, 110, 25, 130), intensity,
                        flowSpeed: 2.2f, flowScale: 1.4f, coreSharp: 3f, coreGlow: 0.9f);
                    // 柱顶小日轮 + 柱脚辉光
                    HanbaBookVFX.DrawSunDisk(top + new Vector2(0f, 30f), 30f + snap * 12f, intensity, 0f);
                    WeaponVFX.DrawGlowBurst(Projectile.Center, (1.6f + snap) * (1f - rt), new Color(255, 170, 70) * (0.8f * intensity));
                }
            }
            return false;
        }
    }

    /// <summary>焦土阴燃带 — 阴燃 30f (无判定, 可预读) 后延燃 96f, 周期灼烧 (旱魃"焦痕延燃"语言)。</summary>
    public class HanbaBookScorchBrand : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int SmolderTime = 30;

        private float Timer => Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 240;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = SmolderTime + 96;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.ai[0]++;
            if (Main.dedServ)
                return;

            bool burning = Timer >= SmolderTime;
            if (Timer == SmolderTime) {
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.1f, Volume = 0.5f }, Projectile.Center);
            }

            // 阴燃: 稀疏烟; 延燃: 火舌上涌 (节流)
            if (Main.rand.NextBool(burning ? 1 : 3)) {
                float x = Main.rand.NextFloat(-0.5f, 0.5f) * Projectile.width;
                if (burning) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(x, 8f), DustID.Torch,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-4.2f, -1.6f)), 110,
                        new Color(255, 150, 50), Main.rand.NextFloat(1.3f, 2.1f));
                    d.noGravity = true;
                }
                else {
                    Dust s = Dust.NewDustPerfect(Projectile.Center + new Vector2(x, 4f), DustID.Smoke,
                        new Vector2(0f, Main.rand.NextFloat(-1.2f, -0.4f)), 170, new Color(90, 60, 45), 1.1f);
                    s.noGravity = true;
                }
            }
            if (burning)
                Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.42f, 0.12f));
        }

        public override bool? CanDamage() => Timer >= SmolderTime;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
            WeaponVFX.AddScreenShake(target.Center, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;

            float burnT = MathHelper.Clamp((Timer - SmolderTime) / 12f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 26f, 0f, 1f);
            float baseA = MathHelper.Lerp(0.12f, 0.55f, burnT) * fade;

            // 地表火线: 三段横向拉伸柔光 (A=0 → 默认批内加法), 相位错拍闪烁
            for (int i = 0; i < 3; i++) {
                float ox = (i - 1) * Projectile.width * 0.3f;
                float flick = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * (9f + i * 2.3f) + i * 2.1f);
                Color c = (i == 1 ? new Color(255, 190, 90) : new Color(230, 95, 25)) * (baseA * flick);
                c.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Center + new Vector2(ox, 2f) - Main.screenPosition, null, c,
                    0f, glow.Size() * 0.5f, new Vector2(1.9f, 0.55f + burnT * 0.5f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 坠日 — 凝聚 40f (汇聚流线 ∝√t、72% 静默、末 6f 预坍缩) → 锁 X 加速坠落 → 落地爆炸
    /// 并掀双向燎原火波。ai[0]=起爆地面线 Y; ai[1]=状态 (0 凝聚 / 1 坠落 / 2 爆炸)。
    /// </summary>
    public class HanbaBookFallingSun : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int ChargeTime = 40;
        private const int BoomTime = 26;

        private float State => Projectile.ai[1];
        private float Timer => Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 420;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            if (State == 0f) {
                Projectile.velocity = Vector2.Zero;
                float t = Timer / ChargeTime;
                // 汇聚流线: 密度 ∝√t, 72% 处硬切静默 (蓄力语法)
                if (!Main.dedServ && t < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(t) * 0.9f) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2CircularEdge(190f, 190f);
                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame, (Projectile.Center - pos) * 0.085f, 100,
                        default, Main.rand.NextFloat(1.4f, 2.2f));
                    d.noGravity = true;
                }
                if (Timer >= ChargeTime) {
                    Projectile.ai[1] = 1f;
                    Projectile.localAI[0] = 0f;
                    Projectile.velocity = new Vector2(0f, 4f);
                    Projectile.tileCollide = true;
                    Projectile.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f, Volume = 1f }, Projectile.Center);
                }
            }
            else if (State == 1f) {
                // t² 加速坠落, 锁 X
                Projectile.velocity.X = 0f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.9f, 34f);
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                        DustID.Torch, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Projectile.velocity.Y * 0.12f), 100,
                        new Color(255, 170, 60), Main.rand.NextFloat(1.6f, 2.4f));
                    d.noGravity = true;
                }
                if (Projectile.Center.Y >= Projectile.ai[0])
                    Detonate();
            }
            else {
                Projectile.velocity = Vector2.Zero;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1.1f, 0.6f, 0.2f));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Detonate();
            return false;
        }

        private void Detonate() {
            if (State == 2f)
                return;
            Projectile.ai[1] = 2f;
            Projectile.localAI[0] = 0f;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.timeLeft = BoomTime;
            Projectile.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f, Volume = 1.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.5f, Volume = 1f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 6f);

            if (!Main.dedServ) {
                for (int i = 0; i < 20; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        Main.rand.NextVector2Circular(9f, 6f) - new Vector2(0f, 3f), 90,
                        new Color(255, 190, 80), Main.rand.NextFloat(1.8f, 3f));
                    d.noGravity = true;
                }
            }

            if (Projectile.owner == Main.myPlayer) {
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                    ACMWeaponBurst.Scorch, scale: 2.2f, owner: Projectile.owner);
                // 双向燎原火波 (各 55% 档: 0.55/2.6 ≈ 0.21 × 本体)
                int waveDamage = (int)(Projectile.damage * 0.21f);
                for (int dir = -1; dir <= 1; dir += 2) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Projectile.Center + new Vector2(dir * 40f, -10f), new Vector2(dir * 8.5f, 0f),
                        ModContent.ProjectileType<HanbaBookFireWave>(), waveDamage, 1f, Projectile.owner);
                }
            }
        }

        public override bool? CanDamage() {
            if (State == 1f)
                return true;            // 坠落体
            if (State == 2f)
                return Timer < 4f;      // 爆炸判定窗
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = State == 2f ? 230f : 46f;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            if (State == 0f) {
                float t = Timer / ChargeTime;
                float collapse = MathHelper.Clamp((Timer - (ChargeTime - 6f)) / 6f, 0f, 1f);
                float radius = MathHelper.Lerp(8f, 46f, t * t * t); // 立方生长: 无害开场, 惊人收尾
                HanbaBookVFX.DrawSunDisk(Projectile.Center, radius, 0.45f + 0.55f * t, collapse);
            }
            else if (State == 1f) {
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 22f,
                    outerColor: new Color(150, 40, 10, 150), innerColor: new Color(255, 205, 110, 210),
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
                HanbaBookVFX.DrawSunDisk(Projectile.Center, 46f, 1f, 0f);
            }
            else {
                float t = Timer / BoomTime;
                float ringR = MathHelper.Lerp(30f, 270f, 1f - MathF.Pow(1f - t, 3f));
                WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 16f, (1f - t) * 0.9f,
                    new Color(255, 200, 100), new Color(200, 70, 15));
                if (t < 0.35f)
                    WeaponVFX.DrawRadialBloom(Projectile.Center, 0.2f, (1f - t / 0.35f) * 0.8f,
                        new Color(255, 160, 55), 10f);
                HanbaBookVFX.DrawSunDisk(Projectile.Center, 46f * (1f - t), 1f - t, 1f);
            }
            return false;
        }
    }

    /// <summary>燎原火波 — 贴地行进的火墙 (高 90px, 可跳越), 简单地形吸附。ai 由速度方向决定行进侧。</summary>
    public class HanbaBookFireWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 70;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            Projectile.ai[0]++;

            // 地形吸附: 自上而下找地表, 让火波贴地爬行
            int tx = (int)(Projectile.Center.X / 16f);
            int tyStart = (int)((Projectile.Center.Y - 48f) / 16f);
            for (int i = 0; i < 12; i++) {
                Tile t = Framing.GetTileSafely(tx, tyStart + i);
                if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    float groundY = (tyStart + i) * 16f;
                    Projectile.Bottom = new Vector2(Projectile.Center.X, groundY + 4f);
                    break;
                }
            }

            if (!Main.dedServ) {
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustPerfect(
                        Projectile.Bottom + new Vector2(Main.rand.NextFloat(-20f, 20f), -Main.rand.NextFloat(0f, 60f)),
                        DustID.Torch, new Vector2(Projectile.velocity.X * 0.3f, Main.rand.NextFloat(-4.5f, -2f)), 110,
                        new Color(255, 165, 60), Main.rand.NextFloat(1.5f, 2.4f));
                    d.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.95f, 0.45f, 0.12f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Scorch, scale: 0.7f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;

            float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f) *
                         MathHelper.Clamp(Projectile.ai[0] / 8f, 0f, 1f);
            // 竖直火幕: 三层错拍拉伸柔光, 头部更亮
            for (int i = 0; i < 3; i++) {
                float flick = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (11f + i * 3.1f) + i * 1.7f);
                float yOff = -18f - i * 16f;
                Color c = (i == 0 ? new Color(255, 210, 120) : new Color(235, 100, 25)) * (0.5f * fade * flick / (1f + i * 0.4f));
                c.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Bottom + new Vector2(0f, yOff) - Main.screenPosition, null, c,
                    0f, glow.Size() * 0.5f, new Vector2(0.85f, 1.5f + i * 0.35f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
