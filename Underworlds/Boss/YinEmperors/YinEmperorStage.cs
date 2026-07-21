using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 鬼门 —— "鬼门开阖"召阴兵的门体（本体无伤害，纯演出 + 服务器端涌兵）。
    /// 开门（ElasticOut 过冲）→ 3 波阴兵魂弹（波间留穿行缝，首波低速 wind-up）→ 阖门熄灭。
    /// ai[0] = 出弹朝向：-1=向左，+1=向右，0=向下（头顶门）；ai[1] = 相位种子。
    /// </summary>
    public class YinEmperorGhostGate : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int OpenTime = 40;
        private const int PourTime = 150;
        private const int CloseTime = 30;

        private int Facing => (int)Projectile.ai[0];
        private ref float Seed => ref Projectile.ai[1];

        private float timer;
        private int waveIndex;

        /// <summary>门体全尺寸（世界像素）。头顶门横置。</summary>
        private Vector2 GateSize => Facing == 0 ? new Vector2(430f, 240f) : new Vector2(240f, 430f);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OpenTime + PourTime + CloseTime + 10;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>开阖进度 0..1（阖门阶段回落）。</summary>
        private float OpenProgress {
            get {
                if (timer <= OpenTime)
                    return ACMUtils.ElasticOut(timer / OpenTime);
                float closeT = timer - OpenTime - PourTime;
                if (closeT > 0)
                    return MathHelper.Clamp(1f - closeT / CloseTime, 0f, 1f);
                return 1f;
            }
        }

        public override void AI() {
            timer++;

            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.7f, Volume = 1.1f }, Projectile.Center);
            }
            if (timer == OpenTime) {
                SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.5f, Volume = 0.9f }, Projectile.Center);
            }
            if (timer == OpenTime + PourTime + 2) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.8f, Volume = 0.9f }, Projectile.Center);
            }

            // 涌兵：3 波，波内留一个可读缺口，缺口位置逐波轮转
            float pourT = timer - OpenTime;
            if (pourT > 0 && pourT <= PourTime && Main.netMode != NetmodeID.MultiplayerClient) {
                if (pourT == 15 || pourT == 60 || pourT == 105) {
                    FireWave(waveIndex);
                    waveIndex++;
                    Projectile.netUpdate = true;
                }
            }

            // 门体幽光
            Lighting.AddLight(Projectile.Center, YinEmperorHelper.AbyssPurple.ToVector3() * 0.9f * OpenProgress);

            // 门内溢出的魂雾
            if (Main.netMode != NetmodeID.Server && OpenProgress > 0.5f && Main.rand.NextBool(3)) {
                Vector2 half = GateSize * 0.36f;
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-half.X, half.X), Main.rand.NextFloat(-half.Y, half.Y));
                var d = Dust.NewDustPerfect(pos, DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = GetPourDirection() * Main.rand.NextFloat(1f, 3f);
            }
        }

        private Vector2 GetPourDirection() => Facing switch {
            -1 => -Vector2.UnitX,
            1 => Vector2.UnitX,
            _ => Vector2.UnitY
        };

        private void FireWave(int wave) {
            Vector2 dir = GetPourDirection();
            // 首波 8 → 末波 11 的速度递增（transition wind-up 公平阀门）
            float speed = 8f + wave * 1.5f;
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f, Volume = 1f }, Projectile.Center);

            if (Facing == 0) {
                // 头顶门：7 列，留 2 个相邻缺口
                int slots = 7;
                int gap = (wave * 2 + (int)(Seed * 7f)) % (slots - 1);
                for (int i = 0; i < slots; i++) {
                    if (i == gap || i == gap + 1) continue;
                    Vector2 pos = Projectile.Center + new Vector2((i - (slots - 1) / 2f) * 62f, 0f);
                    SpawnSoldier(pos, dir * speed, i * 0.7f);
                }
            }
            else {
                // 侧门：5 行，留 1 个缺口（逐波轮转）
                int slots = 5;
                int gap = (wave * 2 + (int)(Seed * 5f)) % slots;
                for (int i = 0; i < slots; i++) {
                    if (i == gap) continue;
                    Vector2 pos = Projectile.Center + new Vector2(0f, (i - (slots - 1) / 2f) * 82f);
                    SpawnSoldier(pos, dir * speed, i * 0.7f);
                }
            }
        }

        private void SpawnSoldier(Vector2 pos, Vector2 vel, float phase) {
            int damage = YinEmperorHelper.GetScaledDamage(80);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<YinEmperorBolt>(), damage, 1f, Main.myPlayer,
                ai0: 1f, ai1: phase);
        }

        public override bool PreDraw(ref Color lightColor) {
            float open = OpenProgress;
            if (open <= 0.01f)
                return false;

            // 头顶门横置：着色器 UV 竖直门洞，绘制时按朝向旋转由尺寸交换实现
            YinEmperorHelper.DrawGate(Main.spriteBatch, Projectile.Center,
                Facing == 0 ? new Vector2(GateSize.Y, GateSize.X) : GateSize,
                open, 1f, Seed);
            return false;
        }
    }

    /// <summary>
    /// 酆都法庭结界 —— 开战即落下的圆形审判场（参考 DazhengArenaBarrier 的玩法骨架）。
    /// 界外持续受"法庭蔑视"惩罚 + 向内推力；视觉走专属 YinEmperorCourt 着色器
    /// （双层反向判词环 + 六座界碑锚点 + 锁链纹）。
    /// ai[0] = 阴天子 NPC 索引。半径/收缩/闪光每帧从本体读取。
    /// </summary>
    public class YinEmperorCourtBarrier : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        private const int DamageInterval = 30;
        private const float PushStartPercent = 0.93f;

        private int BossIndex => (int)Projectile.ai[0];

        private float currentRadius;
        private float fadeProgress;
        private int damageTimer;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.damage = 0;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 10;
            Projectile.alpha = 255;
            Projectile.hide = true;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCs.Add(index);
        }

        private YinEmperor Emperor {
            get {
                int idx = BossIndex;
                if (idx < 0 || idx >= Main.maxNPCs)
                    return null;
                NPC npc = Main.npc[idx];
                if (!npc.active || npc.ModNPC is not YinEmperor emp)
                    return null;
                return emp;
            }
        }

        public override void AI() {
            YinEmperor emp = Emperor;
            if (emp == null) {
                // 本体消失 → 结界快速消散
                fadeProgress -= 0.05f;
                if (fadeProgress <= 0f)
                    Projectile.Kill();
                return;
            }

            Projectile.Center = emp.ArenaCenter;
            Projectile.timeLeft = 10;

            float targetRadius = emp.CourtRadius;
            if (currentRadius <= 0f)
                currentRadius = targetRadius;
            currentRadius = MathHelper.Lerp(currentRadius, targetRadius, 0.04f);

            fadeProgress = MathHelper.Clamp(fadeProgress + 0.02f, 0f, emp.CourtIntensity);

            // 服务端：界外惩罚（法庭蔑视）
            if (Main.netMode != NetmodeID.MultiplayerClient && fadeProgress > 0.5f) {
                damageTimer++;
                if (damageTimer >= DamageInterval) {
                    damageTimer = 0;
                    ApplyContemptDamage();
                }
            }

            // 客户端：向内推力 + 界缘魂尘
            if (Main.netMode != NetmodeID.Server) {
                ApplyPushForce();
                if (fadeProgress > 0.4f && Main.rand.NextBool(4)) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + a.ToRotationVector2() * (currentRadius + Main.rand.NextFloat(-30f, 30f));
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.PurpleTorch);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = a.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.6f;
                }
            }

            // 界碑光照（六座锚点）
            for (int i = 0; i < 6; i++) {
                float a = MathHelper.TwoPi * i / 6f;
                Lighting.AddLight(Projectile.Center + a.ToRotationVector2() * currentRadius,
                    YinEmperorHelper.ImperialGold.ToVector3() * 0.35f * fadeProgress);
            }
        }

        private void ApplyContemptDamage() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                if (Vector2.Distance(p.Center, Projectile.Center) > currentRadius) {
                    int dmg = Main.masterMode ? 120 : Main.expertMode ? 90 : 60;
                    p.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(
                        p.name + " 因藐视酆都法庭而被审判")), dmg, 0);
                }
            }
        }

        private void ApplyPushForce() {
            Player local = Main.LocalPlayer;
            if (!local.active || local.dead) return;

            float dist = Vector2.Distance(local.Center, Projectile.Center);
            float warnDist = currentRadius * PushStartPercent;
            if (dist > warnDist) {
                Vector2 pushDir = (Projectile.Center - local.Center).SafeNormalize(Vector2.Zero);
                float excess = MathHelper.Clamp((dist - warnDist) / (currentRadius * (1f - PushStartPercent)), 0f, 3f);
                local.velocity += pushDir * excess * excess * 0.45f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || fadeProgress <= 0.01f)
                return false;

            YinEmperor emp = Emperor;
            Effect fx = YinEmperorHelper.CourtEffect;
            if (fx == null)
                return false;

            ACMShaders.WorldDecalParams(Projectile.Center, currentRadius,
                out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(fadeProgress);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uCollapse"]?.SetValue(emp?.CourtCollapse ?? 0f);
            fx.Parameters["uFlash"]?.SetValue(emp?.CourtFlash ?? 0f);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(YinEmperorHelper.ImperialGold.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(YinEmperorHelper.AbyssPurple.ToVector3(), 1f));

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.NonPremultiplied);
            return false;
        }
    }
}
