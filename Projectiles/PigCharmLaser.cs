using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 贪食洪流 (猪符咒引导激光): 金色洪流无限穿透; 引导越久猪灵越贪 (伤害/束宽 3s 内爬坡至 ×1.5),
    /// 微粒沿束流被"吃"回猪口; 松手且爬坡 ≥60% → 束端饱嗝爆 (60% AoE) + 玩家后坐。
    /// ai[0]=爬坡计时, ai[1]=有效束长 (owner 写, 同步供各端绘制/判定)。
    /// </summary>
    public class PigCharmLaser : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float RampMax = 180f;      // 3s 爬满
        private const float BurpThreshold = 0.6f;
        private const int FadeTime = 8;

        private ref float Ramp => ref Projectile.ai[0];
        private ref float BeamLength => ref Projectile.ai[1];
        /// <summary>ai[2]: 0=引导中, >0=收招淡出倒计时。</summary>
        private ref float Ending => ref Projectile.ai[2];

        private float Ramp01 => MathHelper.Clamp(Ramp / RampMax, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;          // 洪流穿透一切 (修复旧版命中一个即灭)
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.DamageType = DamageClass.Magic;
        }

        private Vector2 AimDir => Projectile.velocity.SafeNormalize(Vector2.UnitX);

        //velocity 仅承载瞄准方向, 不产生位移 (位置每帧锚定玩家)
        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = player.Center;

            //owner 写入瞄准方向 (存 velocity, 天然同步; 变化明显才 netUpdate)
            if (Projectile.owner == Main.myPlayer && Ending == 0f) {
                Vector2 newAim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                if (Vector2.DistanceSquared(newAim, AimDir) > 0.0016f)
                    Projectile.netUpdate = true;
                Projectile.velocity = newAim;
            }
            Vector2 aim = AimDir;
            Projectile.rotation = aim.ToRotation();

            //持杖姿态
            player.ChangeDir(aim.X >= 0f ? 1 : -1);
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.itemRotation = MathF.Atan2(aim.Y * player.direction, aim.X * player.direction);

            if (Ending > 0f) {
                //收招淡出
                Ending++;
                if (Ending >= FadeTime)
                    Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60; //引导期常刷新, 不因寿命中断

            //束长: 逐步砖块检测 (owner 写入 ai[1] 同步)
            if (Projectile.owner == Main.myPlayer) {
                float maxDist = MathHelper.Clamp(Vector2.Distance(Main.MouseWorld, player.Center), 120f, 1100f);
                float effective = maxDist;
                for (float d = 16f; d < maxDist; d += 8f) {
                    Vector2 checkPos = player.Center + aim * d;
                    if (Collision.SolidCollision(checkPos, 1, 1)) {
                        effective = d - 1f;
                        break;
                    }
                }
                if (MathF.Abs(effective - BeamLength) > 40f)
                    Projectile.netUpdate = true;
                BeamLength = MathF.Max(effective, 60f);
            }

            //爬坡 + 魔力消耗 (每 8f 1 点 ≈ 7.5/s)
            Ramp = MathF.Min(Ramp + 1f, RampMax);
            Projectile.localAI[0]++;
            bool manaOk = true;
            if (Projectile.owner == Main.myPlayer && (int)Projectile.localAI[0] % 8 == 0)
                manaOk = player.CheckMana(1, true);

            //贪食吞纳: 微粒自束端被吸回猪口 (密度随爬坡)
            if (!Main.dedServ && Main.rand.NextFloat() < 0.35f + Ramp01 * 0.45f) {
                float along = Main.rand.NextFloat(0.35f, 1f);
                Vector2 spawn = player.Center + aim * BeamLength * along + Main.rand.NextVector2Circular(10f, 10f);
                Dust d = Dust.NewDustPerfect(spawn, DustID.GoldFlame, -aim * (6f + Ramp01 * 5f), 110, default,
                    0.9f + Ramp01 * 0.5f);
                d.noGravity = true;
            }
            //高爬坡的贪食吞咽声
            if (Ramp01 > 0.5f && (int)Projectile.localAI[0] % 26 == 0)
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.35f, Pitch = -0.35f + Ramp01 * 0.2f },
                    player.Center);

            //沿束光照
            for (float d = 0f; d < BeamLength; d += 90f)
                Lighting.AddLight(player.Center + aim * d, new Vector3(0.5f, 0.42f, 0.15f) * (0.5f + Ramp01 * 0.5f));

            //松手 / 缺蓝 → 收招 (owner 判定, Ending 经 ai[2] 同步)
            if (Projectile.owner == Main.myPlayer && (!player.channel || !manaOk)) {
                BeginEnd(player);
            }
        }

        private void BeginEnd(Player player) {
            Ending = 1f;
            Projectile.netUpdate = true;

            if (Ramp01 >= BurpThreshold) {
                //饱嗝爆: 束端 60% AoE + 幽默后坐
                Vector2 end = player.Center + AimDir * BeamLength;
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 1f, Pitch = -0.55f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.6f, Pitch = 0.2f }, end);
                WeaponVFX.AddScreenShake(player.Center, 3f);
                player.velocity -= AimDir * 3f;

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), end, Vector2.Zero,
                    ModContent.ProjectileType<CharmNovaProj>(), (int)(Projectile.damage * 0.6f),
                    Projectile.knockBack * 1.5f, Projectile.owner, 140f, CharmVFX.Pig * 16f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), end, Vector2.Zero,
                    ModContent.ProjectileType<CharmSealFX>(), 0, 0f, Projectile.owner, CharmVFX.Pig, 1.1f);
            }
            else {
                //没吃饱: 可爱地漏气
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.5f, Pitch = 0.4f }, player.Center);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //贪食爬坡: 3s 内至 ×1.5
            modifiers.FinalDamage *= 1f + 0.5f * Ramp01;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中点金辉 (低频率: 本地免疫 12f 成拍)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, 0.5f + Ramp01 * 0.4f, Projectile.owner);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Ending > 0f)
                return false;
            Player player = Main.player[Projectile.owner];
            Vector2 start = player.Center;
            Vector2 end = start + AimDir * BeamLength;
            float width = 14f + 8f * Ramp01;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, width, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Player player = Main.player[Projectile.owner];
            Vector2 aim = AimDir;
            float fade = Ending > 0f ? 1f - Ending / FadeTime : 1f;
            if (fade <= 0.03f)
                return false;

            Vector2 start = player.Center + aim * 18f;
            Vector2 end = player.Center + aim * BeamLength;

            //洪流主束: 宽度/流速随爬坡, 负向流动 = 向猪口回吸的吞纳感
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f)
                + (Ramp01 > 0.7f ? MathF.Sin(Projectile.localAI[0] * 1.1f) * 0.05f : 0f);
            float halfWidth = (11f + 7f * Ramp01) * fade;
            ACMShaders.DrawBeam(start, end, halfWidth,
                new Color(255, 232, 150, 215), new Color(212, 132, 40, 130), pulse * fade,
                flowSpeed: -(2.2f + Ramp01 * 1.6f), flowScale: 2.5f, coreSharp: 2.3f);

            //束端吞噬口 (亮斑 + 逆流环)
            WeaponVFX.DrawGlowBurst(end, (0.65f + Ramp01 * 0.4f) * fade, new Color(255, 215, 120) * fade);

            //—— 猪灵化形 (口部): 圆拱身 + 双耳 + 鼻笔, 随爬坡鼓胀 (Y 更甚 = 憋气感) ——
            float inflate = 1f + Ramp01 * 0.55f;
            float jitter = Ramp01 > 0.6f ? MathF.Sin(Projectile.localAI[0] * (0.6f + Ramp01)) * 0.03f * Ramp01 : 0f;
            float sx = inflate * (1f + jitter);
            float sy = (1f + (inflate - 1f) * 1.35f) * (1f - jitter);
            Vector2 mouth = player.Center + aim * 26f;
            Vector2 up = aim.RotatedBy(-MathHelper.PiOver2);

            Span<Vector2> body = stackalloc Vector2[4];
            body[0] = mouth - aim * 18f * sx + up * 10f * sy;
            body[1] = mouth - aim * 6f * sx + up * 16f * sy;
            body[2] = mouth + aim * 6f * sx + up * 14f * sy;
            body[3] = mouth + aim * 14f * sx + up * 4f * sy;
            CharmVFX.DrawStroke(body.ToArray(), 9f * inflate, CharmVFX.Pig, fade * 0.95f);
            Span<Vector2> belly = stackalloc Vector2[3];
            belly[0] = mouth - aim * 16f * sx - up * 8f * sy;
            belly[1] = mouth - aim * 2f * sx - up * 13f * sy;
            belly[2] = mouth + aim * 12f * sx - up * 6f * sy;
            CharmVFX.DrawStroke(belly.ToArray(), 8f * inflate, CharmVFX.Pig, fade * 0.85f);
            Span<Vector2> snout = stackalloc Vector2[2];
            snout[0] = mouth + aim * 12f * sx;
            snout[1] = mouth + aim * 22f * sx;
            CharmVFX.DrawStroke(snout.ToArray(), 6f * inflate, CharmVFX.Pig, fade);

            //口部辉光
            WeaponVFX.DrawGlowBurst(mouth + aim * 16f, 0.5f * inflate * fade,
                new Color(255, 226, 140) * fade);
            return false;
        }
    }
}
