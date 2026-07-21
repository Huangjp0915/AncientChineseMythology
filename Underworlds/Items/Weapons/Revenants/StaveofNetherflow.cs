using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 黄泉幽冥杖 - "渡川" (魔法杖)。
    /// 释放黄泉之川的川流能量球 (正弦蛇行 + 轻微追踪), 命中或消亡时在该点
    /// 显化幽冥漩涡 (<see cref="NetherflowVortex"/>): 向心吸引 + 持续伤害 + 吸魂回血。
    /// 命中积业, 业满宣判见 <see cref="RevenantKarma"/>。
    /// </summary>
    public class StaveofNetherflow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 46;
            Item.crit = 4;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 12;
            Item.width = 42;
            Item.height = 42;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<NetherflowOrb>();
            Item.shootSpeed = 10f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //从杖尖释放幽冥能量弹
            Vector2 staffTip = player.Center + velocity.SafeNormalize(Vector2.Zero) * 50f;
            Projectile.NewProjectile(source, staffTip, velocity, type, damage, knockback, player.whoAmI);

            //施法反馈: 幽魂尘向杖尖收束 (汇川入海的倒吸感)
            for (int i = 0; i < 6; i++) {
                Vector2 spawn = staffTip + Main.rand.NextVector2CircularEdge(28f, 28f);
                Dust cast = Dust.NewDustPerfect(
                    spawn, DustID.Wraith, (staffTip - spawn) * 0.16f,
                    120, default, Main.rand.NextFloat(1.0f, 1.4f)
                );
                cast.noGravity = true;
            }

            //在 Item43 咒语音上叠一层水流音 (黄泉之川)
            SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.4f, Pitch = 0.3f }, staffTip);

            return false;
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
    /// 川流能量球 - 黄泉之川的一段"川流": 以初始方向为轴正弦蛇行前进, 轻微追踪敌人
    /// (追踪时蛇行幅度渐减)。命中或消亡时生成 <see cref="NetherflowVortex"/> 幽冥漩涡。
    /// </summary>
    public class NetherflowOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/StaveofNetherflow";

        private ref float Timer => ref Projectile.ai[0];
        /// <summary>追踪收束度 (localAI, 0=全幅蛇行 → 1=近乎直线), 锁定目标后渐增。</summary>
        private ref float HomingFade => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.1f;

            //轻微追踪 (锁定后蛇行渐收, 让弹道可预测)
            if (Timer > 20f) {
                NPC target = FindClosestNPC(360f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.05f);
                    HomingFade = Math.Min(HomingFade + 0.04f, 1f);
                }
            }

            //川流蛇行: 垂直于前进方向的正弦位移 (不动 velocity, 保持基础前进方向)
            float wiggleAmp = 3.2f * (1f - 0.85f * HomingFade);
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perp * (MathF.Sin(Timer * 0.18f) * wiggleAmp);

            //幽冥蓝绿色光照
            float pulse = 0.5f + MathF.Sin(Timer * 0.15f) * 0.15f;
            Lighting.AddLight(Projectile.Center, 0.2f * pulse, 0.5f * pulse, 0.6f * pulse);

            //幽魂漩涡粒子
            if (Main.rand.NextBool(2)) {
                float angle = Timer * 0.3f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                Dust vortex = Dust.NewDustDirect(
                    Projectile.Center + offset, 4, 4, DustID.Wraith,
                    -offset.X * 0.1f, -offset.Y * 0.1f,
                    120, default, Main.rand.NextFloat(0.8f, 1.3f)
                );
                vortex.noGravity = true;
            }

            //暗影拖尾
            if (Main.rand.NextBool(3)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    150, default, Main.rand.NextFloat(0.7f, 1.1f)
                );
                trail.noGravity = true;
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
            //积业 (业秤共享框架, 内部已做 owner 判定/既决冷却/满层宣判)
            RevenantKarma.AddKarma(Projectile, target, 1);

            //附加冥府减益
            target.AddBuff(BuffID.ShadowFlame, 180);
            target.AddBuff(BuffID.Slow, 120);

            //命中爆发: 幽冥漩涡尘环
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi / 20f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(4f, 7f);
                Dust vortex = Dust.NewDustPerfect(
                    target.Center, DustID.Wraith, vel,
                    100, default, Main.rand.NextFloat(1.5f, 2.2f)
                );
                vortex.noGravity = true;
            }

            //暗影焰环
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Dust ring = Dust.NewDustPerfect(
                    target.Center, DustID.Shadowflame, vel,
                    100, default, Main.rand.NextFloat(1.3f, 1.8f)
                );
                ring.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.4f }, target.Center);

            //命中演出 (径向辉光 + 冲击环, 更新阶段安全)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: 1.1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            //幽冥长川尾 (双层 ribbon: 外宽暗蓝 + 内窄亮青)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 15f,
                outerColor: new Color(30, 90, 130, 150), innerColor: new Color(110, 235, 250, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            //幽冥旋涡烟雾 (取一帧 Smoke 旋转叠加)
            Texture2D smoke = ACMAsset.Smoke;
            if (smoke != null) {
                int frame = (int)(Timer * 0.3f) % 16;
                int frameW = smoke.Width / 4;
                int frameH = smoke.Height / 4;
                Rectangle sourceRect = new Rectangle((frame % 4) * frameW, (frame / 4) * frameH, frameW, frameH);
                Vector2 smokeOrigin = new Vector2(frameW / 2f, frameH / 2f);
                Color smokeColor = new Color(50, 140, 175) * 0.22f;
                smokeColor.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, sourceRect, smokeColor, Timer * 0.06f, smokeOrigin, 0.22f, SpriteEffects.None, 0);
            }

            //双层能量球呼吸: 外宽暗 + 内窄亮 (弹芯亮层代偿已移除的 RadialBloom, 不占全屏名额)
            float pulse = 0.5f + MathF.Sin(Timer * 0.15f) * 0.12f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.7f + pulse * 0.6f), new Color(35, 110, 150) * 0.5f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, (0.9f + pulse * 0.4f), new Color(140, 245, 255));

            return false;
        }

        public override void OnKill(int timeLeft) {
            //消亡爆发: 黄泉幽冥径向辉光 + 冲击环 (ACMWeaponBurst 暗冥幽蓝紫)
            ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
                ACMWeaponBurst.AbyssPurple, 1.2f, Projectile.owner);

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);

            //幽冥爆散
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust death = Dust.NewDustPerfect(
                    Projectile.Center, DustID.Wraith, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                death.noGravity = true;
            }

            //暗影焰碎片
            for (int i = 0; i < 8; i++) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                shadow.noGravity = true;
            }

            //命中或消亡处显化幽冥漩涡 (仅 owner 侧生成, 正常弹幕同步; 同屏 ≤2, 超出清最旧)
            if (Main.myPlayer == Projectile.owner) {
                int vortexType = ModContent.ProjectileType<NetherflowVortex>();
                int count = 0, oldestIdx = -1, oldestLife = int.MaxValue;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.type != vortexType || p.owner != Projectile.owner)
                        continue;
                    count++;
                    if (p.timeLeft < oldestLife) {
                        oldestLife = p.timeLeft;
                        oldestIdx = i;
                    }
                }
                if (count >= 2 && oldestIdx >= 0)
                    Main.projectile[oldestIdx].Kill();

                int vortexDamage = Math.Max(1, (int)(Projectile.damage * 0.30f));
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                    vortexType, vortexDamage, 0f, Projectile.owner);
            }
        }
    }

    /// <summary>
    /// 幽冥漩涡 - 黄泉之川在此打旋: 110px 半径持续伤害区 (≈每 15 帧一跳),
    /// 向心吸引非 Boss 敌人, 每跳 +1 业 + 暗影焰 + 为持有者吸魂 1 HP。
    /// 同 owner 同屏 ≤2 (生成端负责清最旧)。
    /// 视觉: ArenaRunic 法阵环 (静态帧号节流, 每帧仅 1 个漩涡承担满屏 pass, 其余退化为 Smoke+柔光)
    /// + 双层反旋 Smoke + 3 道绕心螺旋短光束 + 核心呼吸柔光。不使用 RadialBloom (名额留给宣判)。
    /// </summary>
    public class NetherflowVortex : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int Life = 300;
        private const float Radius = 110f;
        private const float FadeInFrames = 15f;
        private const float FadeOutFrames = 40f;

        private ref float Timer => ref Projectile.ai[0];

        //每帧只允许一个漩涡绘制满屏 ArenaRunic 法阵 (开销护栏, 参考 FateRuneProjectile._lastRuneRingFrame)
        private static ulong _lastVortexRingFrame;

        public override void SetDefaults() {
            Projectile.width = 220;
            Projectile.height = 220;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //圆形判定 (220 方框裁成 110px 半径)
            Vector2 nearest = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) <= Radius * Radius;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation += 0.02f;

            //向心吸引: 各端弹幕都在跑 AI, 直接改 npc.velocity 即可 (非 Boss 且可受击退者)
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy() || npc.boss || npc.knockBackResist <= 0f)
                    continue;
                Vector2 toCenter = Projectile.Center - npc.Center;
                if (toCenter.LengthSquared() > Radius * Radius)
                    continue;
                npc.velocity += toCenter.SafeNormalize(Vector2.Zero) * 0.6f;
            }

            //呼吸光照 (暗蓝紫底 + 亮青偏移)
            float pulse = 0.55f + MathF.Sin(Timer * 0.08f) * 0.2f;
            Lighting.AddLight(Projectile.Center, 0.3f * pulse, 0.35f * pulse, 0.7f * pulse);

            //幽魂尘沿切向绕心 (预算 ≤1/2 帧)
            if (Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float r = Main.rand.NextFloat(50f, Radius - 8f);
                Vector2 radial = ang.ToRotationVector2();
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + radial * r, DustID.Wraith,
                    radial.RotatedBy(MathHelper.PiOver2) * 2.4f - radial * 0.7f,
                    130, default, Main.rand.NextFloat(0.9f, 1.4f)
                );
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //积业 + 暗影焰
            RevenantKarma.AddKarma(Projectile, target, 1);
            target.AddBuff(BuffID.ShadowFlame, 120);

            //吸魂: 每跳为持有者回 1 HP (治疗只走 owner 端, Heal 内部广播绿字)
            if (Main.myPlayer == Projectile.owner) {
                Player owner = Main.player[Projectile.owner];
                if (owner.active && !owner.dead && owner.statLife < owner.statLifeMax2)
                    owner.Heal(1);
            }

            //吸入尘: 从敌人处被卷向漩涡中心
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(
                    target.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Wraith,
                    (Projectile.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f),
                    110, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                d.noGravity = true;
            }
        }

        /// <summary>漩涡整体淡入淡出系数 (淡入 15 帧 / 淡出 40 帧)。</summary>
        private float Fade =>
            MathHelper.Clamp(Timer / FadeInFrames, 0f, 1f) *
            MathHelper.Clamp(Projectile.timeLeft / FadeOutFrames, 0f, 1f);

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float fade = Fade;
            if (fade <= 0.01f)
                return false;

            //—— ArenaRunic 法阵环 (满屏 pass, 每帧仅 1 个漩涡承担; 抢不到的退化为 Smoke+柔光) ——
            bool drawRing = false;
            if (_lastVortexRingFrame != Main.GameUpdateCount) {
                _lastVortexRingFrame = Main.GameUpdateCount;
                drawRing = true;
            }

            if (drawRing) {
                Effect fx = ACMShaders.ArenaRunic;
                if (fx != null) {
                    float radius = Radius * (0.7f + 0.3f * MathHelper.Clamp(Timer / FadeInFrames, 0f, 1f));
                    ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uCenter"]?.SetValue(uv);
                    fx.Parameters["uRadius"]?.SetValue(rFrac);
                    fx.Parameters["uIntensity"]?.SetValue(fade * 0.8f);
                    fx.Parameters["uAspect"]?.SetValue(aspect);
                    fx.Parameters["uColorPrimary"]?.SetValue(new Color(150, 110, 240).ToVector4());
                    fx.Parameters["uColorSecondary"]?.SetValue(new Color(40, 20, 90).ToVector4());
                    fx.Parameters["uRuneFreq"]?.SetValue(10f);
                    fx.Parameters["uMode"]?.SetValue(0f);
                    fx.Parameters["uShape"]?.SetValue(0f);

                    SpriteBatch sb = Main.spriteBatch;
                    sb.End();
                    ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                    ACMShaders.RestoreDefaultBatch(sb);
                }

                //3 道绕心螺旋短弧 (外缘 → 内圈的弦线随旋转角摆动, 只在承担法阵的漩涡上画, 控制批次)
                float baseRot = Timer * 0.035f;
                for (int i = 0; i < 3; i++) {
                    float ang = baseRot + MathHelper.TwoPi / 3f * i;
                    Vector2 outer = Projectile.Center + ang.ToRotationVector2() * (Radius - 14f);
                    Vector2 inner = Projectile.Center + (ang + 1.4f).ToRotationVector2() * 28f;
                    ACMShaders.DrawBeam(outer, inner, halfWidth: 7f,
                        core: new Color(150, 235, 250), edge: new Color(90, 50, 190),
                        intensity: fade * 0.55f, flowSpeed: 2.6f, flowScale: 2.4f, coreSharp: 2.4f);
                }
            }

            //—— 双层反旋 Smoke 旋涡 (自 NetherflowOrb 迁移放大) ——
            Texture2D smoke = ACMAsset.Smoke;
            if (smoke != null) {
                int frameW = smoke.Width / 4;
                int frameH = smoke.Height / 4;
                Vector2 smokeOrigin = new Vector2(frameW / 2f, frameH / 2f);

                int frameA = (int)(Timer * 0.25f) % 16;
                Rectangle rectA = new Rectangle((frameA % 4) * frameW, (frameA / 4) * frameH, frameW, frameH);
                Color colA = new Color(60, 45, 130) * (0.4f * fade);
                colA.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, rectA, colA, Timer * 0.045f, smokeOrigin, 0.95f, SpriteEffects.None, 0);

                int frameB = ((int)(Timer * 0.25f) + 7) % 16;
                Rectangle rectB = new Rectangle((frameB % 4) * frameW, (frameB / 4) * frameH, frameW, frameH);
                Color colB = new Color(80, 150, 210) * (0.3f * fade);
                colB.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, rectB, colB, -Timer * 0.03f, smokeOrigin, 0.6f, SpriteEffects.None, 0);
            }

            //—— 核心呼吸柔光 (外暗紫 + 内亮青; 不用 RadialBloom, 名额留给宣判) ——
            float corePulse = 0.8f + MathF.Sin(Timer * 0.09f) * 0.25f;
            WeaponVFX.DrawGlowBurst(Projectile.Center, 2.3f * corePulse * fade, new Color(80, 50, 180) * 0.55f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.1f * corePulse * fade, new Color(130, 235, 250) * 0.8f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            //川流散尽: 一圈幽魂尘外抛
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi / 12f * i;
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + ang.ToRotationVector2() * 30f, DustID.Wraith,
                    ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f),
                    120, default, Main.rand.NextFloat(1.0f, 1.6f)
                );
                d.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.35f, Pitch = -0.3f }, Projectile.Center);
        }
    }
}
