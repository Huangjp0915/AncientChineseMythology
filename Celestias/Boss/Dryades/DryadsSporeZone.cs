using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 毒孢区 — 树精 P2「蔓生 Overgrown」签名机制 (真·新机制, 非 P1 更快)。
    /// 每次潜地在**旧锚点**留下一片贴地毒孢区 ~10s:
    ///  - 站入持续中毒 (Poisoned), 是贴地危险——**跳过即可避开**。
    ///  - 火把/火系武器投射物可**烧除** (curated fire 集): 给火系流派额外清场手段。
    /// 视觉走 ArenaRunic 地纹圈 (毒绿)。server-zero-draw。
    /// </summary>
    public class DryadsSporeZone : ModProjectile
    {
        // 复用同目录已有贴图, 满足贴图自动加载 (本体不绘制实贴, 走地纹)。
        public override string Texture => "AncientChineseMythology/Celestias/Boss/Dryades/Acanthosphere";

        private const int LifeTime = 600;       // ~10s
        private const float WorldRadius = 150f; // 地纹/判定半径 (贴地)

        private static readonly Color PoisonGreen = new(120, 200, 60);
        private static readonly Color PoisonDark = new(40, 90, 25);

        // 可烧除的火系玩家投射物 (curated; 火把/火系流派触发快速烧除)
        private static readonly HashSet<int> FireProjectiles = new() {
            ProjectileID.Flamelash, ProjectileID.BallofFire, ProjectileID.FlamingArrow,
            ProjectileID.HellfireArrow, ProjectileID.MolotovFire,
            ProjectileID.InfernoFriendlyBlast, ProjectileID.InfernoFriendlyBolt,
            ProjectileID.DD2FlameBurstTowerT1Shot, ProjectileID.DD2FlameBurstTowerT2Shot,
            ProjectileID.DD2FlameBurstTowerT3Shot,
        };

        private ref float Burn => ref Projectile.localAI[0]; // 0~1 烧除进度 (纯本地视觉/本地判定亦可)

        public override void SetDefaults() {
            Projectile.width = 280;
            Projectile.height = 96;  // 贴地: 矮, 可跳过
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            // 烧除检测: 火系玩家投射物重叠 → 加速烧除 (server 权威 Kill)
            Rectangle zone = Projectile.Hitbox;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.hostile || other.owner == Main.maxPlayers)
                    continue;
                if (!FireProjectiles.Contains(other.type))
                    continue;
                if (!other.Hitbox.Intersects(zone))
                    continue;
                Burn += 0.06f;
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    Dust f = Dust.NewDustDirect(other.position, other.width, other.height,
                        DustID.Torch, 0f, -1f, 60, default, 1.4f);
                    f.noGravity = true;
                }
            }

            if (Burn >= 1f) {
                BurnOut();
                return;
            }

            // 贴地毒孢粒子 (随剩余时间/烧除衰减)
            if (Main.netMode != NetmodeID.Server) {
                float life = Projectile.timeLeft / (float)LifeTime;
                float density = (1f - Burn) * (0.4f + 0.6f * life);
                int count = Main.rand.NextBool() ? 2 : 1;
                for (int i = 0; i < count; i++) {
                    if (!Main.rand.NextBool(2)) continue;
                    Vector2 p = Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-Projectile.width / 2f, Projectile.width / 2f),
                        Main.rand.NextFloat(-Projectile.height / 2f, Projectile.height / 4f));
                    Dust d = Dust.NewDustDirect(p, 0, 0, DustID.JungleSpore,
                        Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.2f, -0.3f),
                        120, default, (0.9f + Main.rand.NextFloat(0.6f)) * density);
                    d.noGravity = true;
                    d.fadeIn = 1.1f;
                }
                Lighting.AddLight(Projectile.Center, new Vector3(0.12f, 0.3f, 0.06f) * (1f - Burn));
            }
        }

        private void BurnOut() {
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f, Volume = 0.5f }, Projectile.Center);
                for (int i = 0; i < 24; i++) {
                    Vector2 p = Projectile.Center + new Vector2(
                        Main.rand.NextFloat(-Projectile.width / 2f, Projectile.width / 2f),
                        Main.rand.NextFloat(-Projectile.height / 2f, Projectile.height / 3f));
                    Dust d = Dust.NewDustDirect(p, 0, 0, DustID.Torch,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -0.5f), 60, default, 1.6f);
                    d.noGravity = true;
                }
            }
            Projectile.Kill();
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return false;

            float life = Projectile.timeLeft / (float)LifeTime;
            // 出现淡入 + 消失淡出 + 烧除衰减
            float fade = MathHelper.Clamp(life * 6f, 0f, 1f) * MathHelper.Clamp((1f - life) * 6f + 0.4f, 0f, 1f);
            float intensity = fade * (1f - Burn) * 0.85f;
            if (intensity <= 0.01f)
                return false;

            ACMShaders.WorldDecalParams(Projectile.Center, WorldRadius, out Vector2 uv, out float radUV, out float aspect);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radUV);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(PoisonGreen.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(PoisonDark.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.NonPremultiplied);
            return false;
        }
    }
}
