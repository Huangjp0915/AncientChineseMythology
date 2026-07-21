using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 河豚高压水柱 (手持弹幕): 引导期河豚本体越喷越鼓 (憋气抖动), 水柱轻微增粗;
    /// 鼓满 150 帧自动、或松手且蓄力 ≥40% 触发【喷嚏爆刺】(锥形 5 + 全向 8 根水刺, 55% 伤害);
    /// 蓄力不足松手只漏气。三段贴图 (头 Proj2 / 身 Proj1 / 尾 Proj3) 保留。
    /// ai[0]=蓄力帧, ai[1]=状态 (0 引导 / 1 喷嚏收场 / 2 漏气收场)。
    /// </summary>
    public class PufferfishProj1 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/PufferfishProj1";

        private const float MaxCharge = 150f;   // 鼓满帧数
        private const float SneezeCharge = 60f; // 松手可触发喷嚏的最低蓄力 (40%)
        private const int FormTime = 16;        // 水柱成形帧

        private static readonly Color WaterCore = new(185, 230, 255);
        private static readonly Color WaterBloom = new(120, 200, 255);
        private static readonly Color WaterDeep = new(40, 95, 185);

        private Player Owner => Main.player[Projectile.owner];

        private ref float Charge => ref Projectile.ai[0];
        private ref float State => ref Projectile.ai[1];
        private ref float FormVal => ref Projectile.localAI[0]; // 0~FormTime 水柱成形/收场进度

        private float LaserLength;
        private float _seenState; // 各端各自跟踪的状态迁移 (用于在所有客户端播放转场演出)
        private float Charge01 => MathHelper.Clamp(Charge / MaxCharge, 0f, 1f);

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 200;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 18000; // 生命由引导状态管理
            Projectile.alpha = 100;
            Projectile.light = 0.6f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10; // ≈6 tick/s, 与旧版持平
        }

        public override bool ShouldUpdatePosition() => false;

        private void SetLaserPosition() {
            // 不穿墙: 从喷口向外步进探测
            LaserLength = 20;
            Vector2 unit = Projectile.velocity.SafeNormalize(Vector2.Zero);
            float maxLen = 900f + Charge01 * 300f; // 越鼓压越足, 射程微增
            while (LaserLength <= maxLen) {
                Vector2 range = Projectile.Center + unit * LaserLength;
                if (!Collision.CanHit(Projectile.Center, 1, 1, range, 1, 1)) {
                    LaserLength -= 5;
                    return;
                }
                LaserLength += 4;
            }
        }

        public override void AI() {
            Player player = Owner;
            if (!player.active || player.dead || player.noItems || player.CCed) {
                Projectile.Kill();
                return;
            }

            // ===== 状态机 (决策仅 owner 端; State 经 ai[] 同步, 各端在迁移帧播转场演出) =====
            bool isOwner = Main.myPlayer == Projectile.owner;
            if (State == 0f) {
                // 引导期: 成形 + 蓄力 (Charge 各端自走, 偏差由 netUpdate 校正)
                if (FormVal < FormTime)
                    FormVal++;
                Charge++;

                // 水压渐强的持续声 (每 24 帧一记, 音高随鼓胀上行)
                if (Charge % 24f == 0f)
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.45f, Pitch = -0.1f + Charge01 * 0.4f }, Projectile.Center);

                if (isOwner) {
                    bool keep = player.channel;
                    // 每 20 帧续 3 魔 (≈9/s); 断魔视同松手
                    if (keep && Charge % 20f == 19f && !player.CheckMana(3, true))
                        keep = false;
                    if (keep)
                        player.manaRegenDelay = Math.Max(player.manaRegenDelay, 30);

                    if (Charge >= MaxCharge) {
                        State = 1f;
                        Projectile.netUpdate = true;
                    }
                    else if (!keep) {
                        // 松手/断魔: 蓄力够则喷嚏, 不够则漏气
                        State = Charge >= SneezeCharge ? 1f : 2f;
                        Projectile.netUpdate = true;
                    }
                }
            }
            else {
                // 收场: 水柱回缩, 伤害关闭
                Projectile.friendly = false;
                FormVal -= 1.6f;
                if (FormVal <= 0f) {
                    Projectile.Kill();
                    return;
                }
            }

            // 状态迁移帧: 全客户端演出 + owner 端刺弹生成
            if (State != _seenState) {
                if (State == 1f)
                    DoSneeze();
                else if (State == 2f)
                    DoFizzle();
                _seenState = State;
            }

            // ===== 持握与朝向 =====
            if (Main.myPlayer == Projectile.owner && State == 0f) {
                Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX);
                Vector2 oldVel = Projectile.velocity;
                // 角度平滑 (高压水管的甩动惯性)
                Projectile.velocity = Vector2.Lerp(Projectile.velocity.SafeNormalize(Vector2.UnitX), aim, 0.3f).SafeNormalize(Vector2.UnitX);
                if (Vector2.DistanceSquared(oldVel, Projectile.velocity) > 0.001f)
                    Projectile.netUpdate = true;
            }

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            player.ChangeDir(dir.X >= 0f ? 1 : -1);
            player.heldProj = Projectile.whoAmI;
            player.itemAnimation = player.itemTime = 2;
            player.itemRotation = MathF.Atan2(dir.Y * player.direction, dir.X * player.direction);

            // 喷口位置: 河豚嘴 (随鼓胀前移一点)
            Projectile.Center = player.MountedCenter + dir * (34f + Charge01 * 10f);

            SetLaserPosition();

            // ===== 引导期粒子 =====
            if (State == 0f && FormVal >= FormTime * 0.5f) {
                // 沿柱水花 (节流)
                if (Projectile.timeLeft % 8 == 0) {
                    Dust water = Dust.NewDustPerfect(
                        Projectile.Center + dir * (LaserLength * Main.rand.NextFloat()) + Main.rand.NextVector2Circular(14, 14),
                        DustID.Water, dir.RotatedByRandom(0.6) * Main.rand.NextFloat(1f, 2.5f), 110, default, 1.05f);
                    water.noGravity = true;
                }
                // 柱头飞沫
                if (Projectile.timeLeft % 10 == 0) {
                    Dust mist = Dust.NewDustPerfect(
                        Projectile.Center + dir * LaserLength + Main.rand.NextVector2Circular(24, 24),
                        DustID.Cloud, dir * Main.rand.NextFloat(1f, 3f), 140, default, 1.2f);
                    mist.noGravity = true;
                }
                // 鼓胀期从鱼身冒气泡 (越鼓越密, 憋不住了)
                if (Charge01 > 0.25f && Main.rand.NextBool(Math.Max(1, 7 - (int)(Charge01 * 6f)))) {
                    Vector2 fishPos = player.MountedCenter + dir * 10f;
                    Dust bubble = Dust.NewDustPerfect(fishPos + Main.rand.NextVector2Circular(10f, 10f),
                        DustID.BreatheBubble, new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f)), 80, default, Main.rand.NextFloat(0.7f, 1.1f));
                    bubble.noGravity = true;
                }
            }
        }

        /// <summary>喷嚏爆刺 ("阿嚏!"): 锥形 5 + 全向 8 根水刺 + 演出栈 + 幽默后坐。</summary>
        private void DoSneeze() {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 fishPos = Owner.MountedCenter + dir * 14f;

            // "阿嚏": 高音水爆 + 低频扑通
            SoundEngine.PlaySound(SoundID.Item21 with { Volume = 1f, Pitch = 0.55f }, fishPos);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.8f, Pitch = -0.2f }, fishPos);
            WeaponVFX.AddScreenShake(fishPos, 3f);

            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), fishPos,
                ACMWeaponBurst.Water, 1.8f, Projectile.owner);

            // 大水花
            for (int i = 0; i < 22; i++) {
                Dust d = Dust.NewDustPerfect(fishPos, Main.rand.NextBool() ? DustID.Water : DustID.Cloud,
                    Main.rand.NextVector2CircularEdge(8f, 8f) * Main.rand.NextFloat(0.4f, 1f), 90, default, Main.rand.NextFloat(1.2f, 1.9f));
                d.noGravity = true;
            }

            if (Projectile.owner == Main.myPlayer) {
                // 幽默后坐: 河豚把主人喷退一小步
                Owner.velocity -= dir * 3f;

                int spineDamage = Math.Max(1, (int)(Projectile.damage * 0.55f));
                var src = Projectile.GetSource_FromThis();
                int spineType = ModContent.ProjectileType<PufferfishSpine>();

                // 锥形 5 根 (朝准星, ±22°)
                for (int i = 0; i < 5; i++) {
                    float off = MathHelper.Lerp(-0.38f, 0.38f, i / 4f);
                    Vector2 vel = dir.RotatedBy(off) * Main.rand.NextFloat(13f, 16f);
                    Projectile.NewProjectile(src, fishPos, vel, spineType, spineDamage, 4f, Projectile.owner);
                }
                // 全向 8 根 (可爱地全身炸毛)
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = (MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(-0.15f, 0.15f)).ToRotationVector2()
                        * Main.rand.NextFloat(8f, 10f);
                    Projectile.NewProjectile(src, fishPos, vel, spineType, spineDamage, 4f, Projectile.owner);
                }
            }
        }

        /// <summary>漏气 (蓄力不足松手): 小水花 + 泄气音。</summary>
        private void DoFizzle() {
            Vector2 fishPos = Owner.MountedCenter + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 14f;
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = 0.4f }, fishPos);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(fishPos, DustID.Water,
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - new Vector2(0, 1f), 100, default, 1.1f);
                d.noGravity = Main.rand.NextBool();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Water, 1.2f, Projectile.owner);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Water,
                    Main.rand.NextVector2Circular(3f, 3f), 110, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (FormVal < FormTime * 0.8f || State != 0f)
                return false;
            float point = 0f;
            Vector2 startPoint = Projectile.Center;
            Vector2 endPoint = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * (int)LaserLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                startPoint, endPoint, 32f * (0.8f + Charge01 * 0.3f), ref point)
                ? true
                : base.Colliding(projHitbox, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int length = (int)LaserLength;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float form01 = MathHelper.Clamp(FormVal / FormTime, 0f, 1f);

            DrawPufferBody(dir, form01);

            if (form01 <= 0.05f)
                return false;

            // 水柱宽度: 成形 × 呼吸脉冲 × 鼓胀增粗
            float widthPulse = 1f + 0.08f * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 8f);
            float widthScale = form01 * widthPulse * (0.9f + Charge01 * 0.35f);

            Color color1 = Color.White;
            color1.A = 0; // 三段贴图黑底 → 加算

            // 头部
            Texture2D head = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/PufferfishProj2").Value;
            Main.EntitySpriteDraw(head, Projectile.Center - Main.screenPosition, null, color1,
                dir.ToRotation(), new Vector2(0, head.Height / 2), new Vector2(1, widthScale), SpriteEffects.None, 0);
            // 身体 (延长绘制)
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition + dir * head.Width,
                new Rectangle(0, 0, length, tex.Height), color1,
                dir.ToRotation(), new Vector2(0, tex.Height / 2), new Vector2(1, widthScale), SpriteEffects.None, 0);
            // 尾部
            Texture2D tail = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/PufferfishProj3").Value;
            Main.EntitySpriteDraw(tail, Projectile.Center - Main.screenPosition + dir * (head.Width + length), null, color1,
                dir.ToRotation(), new Vector2(0, tail.Height / 2), new Vector2(1, widthScale), SpriteEffects.None, 0);

            // 柱头冲击柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center + dir * LaserLength, 0.5f * form01, WaterBloom * 0.55f);

            return false;
        }

        /// <summary>河豚本体: 鼓胀 (Y 轴胀更多) + 憋气抖动 (频率随蓄力升高) + 满蓄警告红晕。</summary>
        private void DrawPufferBody(Vector2 dir, float form01) {
            Texture2D fish = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Items/Weapons/Staffs/Pufferfish").Value;
            Player player = Owner;
            Vector2 fishPos = player.MountedCenter + dir * (12f + Charge01 * 6f);

            // 鼓胀: X 轻微, Y 明显 (憋气)
            float jitterFreq = MathHelper.Lerp(6f, 22f, Charge01);
            float jitter = MathF.Sin((float)Main.GlobalTimeWrappedHourly * jitterFreq * MathHelper.TwoPi * 0.4f) * 0.035f * Charge01;
            float scaleX = (0.62f + Charge01 * 0.22f + jitter) * form01;
            float scaleY = (0.62f + Charge01 * 0.38f - jitter) * form01;

            SpriteEffects fx = dir.X >= 0f ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Color lit = Lighting.GetColor(fishPos.ToTileCoordinates());

            // 满蓄预警: 鱼身透红 (要打喷嚏了!)
            if (Charge01 > 0.75f) {
                float warn = (Charge01 - 0.75f) / 0.25f;
                lit = Color.Lerp(lit, new Color(255, 120, 110), warn * 0.45f * (0.6f + 0.4f * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 18f)));
            }

            Main.spriteBatch.Draw(fish, fishPos - Main.screenPosition, null, lit,
                dir.ToRotation(), fish.Size() * 0.5f, new Vector2(scaleX, scaleY), fx, 0f);

            // 鼓胀辉光 (蓄力读条)
            if (Charge01 > 0.1f) {
                Color glow = WaterBloom * (0.25f * Charge01);
                glow.A = 0;
                Main.spriteBatch.Draw(fish, fishPos - Main.screenPosition, null, glow,
                    dir.ToRotation(), fish.Size() * 0.5f, new Vector2(scaleX, scaleY) * 1.12f, fx, 0f);
            }
        }
    }

    /// <summary>
    /// 河豚水刺 (喷嚏爆刺): 快速短命的水色尖刺, 程序化绘制 (LightShot 拉伸)。
    /// </summary>
    public class PufferfishSpine : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static readonly Color SpineCore = new(210, 240, 255);
        private static readonly Color SpineDeep = new(60, 130, 210);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.PufferfishSpine.DisplayName",
                () => "Puffer Spine");
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 42;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.985f; // 水刺出膛后渐失压

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    -Projectile.velocity * 0.1f, 120, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Water,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 110, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    Main.rand.NextVector2Circular(1.8f, 1.8f), 120, default, 0.95f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, 4f,
                SpineDeep with { A = 130 }, SpineCore with { A = 180 });

            // 程序化尖刺: LightShot 沿速度拉伸 (--> 形状天然是刺)
            Texture2D shot = ACMAsset.LightShot;
            if (shot != null) {
                float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
                Color deep = SpineDeep * (0.8f * fade); deep.A = 0;
                Color core = SpineCore * fade; core.A = 0;
                Vector2 pos = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(shot, pos, null, deep, Projectile.rotation,
                    shot.Size() * 0.5f, new Vector2(0.75f, 0.24f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(shot, pos, null, core, Projectile.rotation,
                    shot.Size() * 0.5f, new Vector2(0.55f, 0.13f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
