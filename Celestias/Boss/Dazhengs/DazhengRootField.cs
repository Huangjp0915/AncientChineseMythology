using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿「活体竞技场·根须内蚀」— 把"屏障只会收缩"升级为活的战场 (春 P2 / 冬 季节规则)。
    ///
    /// 根须向内侵蚀整片地面 (致命), 仅留 3 座<b>缓慢迁移的常青安全岛</b>; 玩家须持续转移落脚点。
    /// 安全岛位置由 <see cref="Main.GameUpdateCount"/> 确定性推导 (server/client 一致, 无需额外同步),
    /// 伤害判定服务器权威。强度由大椿经 ai[1] 调制: 0→1 渐强即 telegraph (致命前给足预警)。
    ///
    /// ai[0]=大椿 whoAmI; ai[1]=强度目标 0~1。绘制 client-only。
    /// </summary>
    public class DazhengRootField : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        public const int IslandCount = 3;
        public const float OrbitRadius = 430f;
        public const float IslandRadius = 210f;
        public const float Wobble = 70f;
        private const float LethalThreshold = 0.6f;
        private const int DamageInterval = 26;

        private int BossIndex => (int)Projectile.ai[0];
        private float TargetIntensity => MathHelper.Clamp(Projectile.ai[1], 0f, 1f);

        private float intensity;
        private float anim;
        private int damageTimer;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 12; // 由大椿每帧续命
            Projectile.ignoreWater = true;
            Projectile.hide = true;
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCs.Add(index);
        }

        /// <summary>第 i 座安全岛的世界坐标 (确定性: 由全局帧计数 + 中心推导, 多端一致)。</summary>
        public static Vector2 IslandPos(Vector2 center, int i) {
            float t = Main.GameUpdateCount;
            float baseRot = t * 0.0035f;
            float angle = baseRot + MathHelper.TwoPi / IslandCount * i;
            float r = OrbitRadius + MathF.Sin(t * 0.02f + i * 2.1f) * Wobble;
            return center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
        }

        /// <summary>玩家是否站在任一安全岛内。</summary>
        public static bool IsSafe(Vector2 worldPos, Vector2 center) {
            for (int i = 0; i < IslandCount; i++) {
                if (Vector2.DistanceSquared(worldPos, IslandPos(center, i)) <= IslandRadius * IslandRadius)
                    return true;
            }
            return false;
        }

        public override void AI() {
            anim += 1f / 60f;

            if (BossIndex < 0 || BossIndex >= Main.maxNPCs ||
                !Main.npc[BossIndex].active || Main.npc[BossIndex].type != ModContent.NPCType<Dazheng>()) {
                Projectile.Kill();
                return;
            }

            NPC boss = Main.npc[BossIndex];
            Projectile.Center = boss.Center;

            intensity = MathHelper.Lerp(intensity, TargetIntensity, 0.04f);

            // 服务器权威: 致命根须地面 (安全岛外周期伤害)
            if (Main.netMode != NetmodeID.MultiplayerClient && intensity > LethalThreshold) {
                damageTimer++;
                if (damageTimer >= DamageInterval) {
                    damageTimer = 0;
                    ApplyRootDamage(boss.Center);
                }
            }
            else {
                damageTimer = 0;
            }

            // 安全岛上的常青光照 + 致命根须暗示
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < IslandCount; i++) {
                    Vector2 ip = IslandPos(boss.Center, i);
                    Lighting.AddLight(ip, new Vector3(0.25f, 0.55f, 0.2f) * (0.6f + intensity * 0.4f));
                    if (intensity > 0.2f && Main.rand.NextBool(6)) {
                        Dust d = Dust.NewDustPerfect(ip + Main.rand.NextVector2Circular(IslandRadius, IslandRadius),
                            DustID.GrassBlades, Vector2.Zero, 120, TelegraphColors.Safe, 1.2f);
                        d.noGravity = true;
                    }
                }
                // 致命地面根须粒子 (强度越高越密)
                if (intensity > LethalThreshold && Main.rand.NextBool(2)) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    float r = Main.rand.NextFloat(120f, OrbitRadius + IslandRadius);
                    Vector2 p = boss.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                    if (!IsSafe(p, boss.Center)) {
                        Dust d = Dust.NewDustPerfect(p, DustID.JungleGrass, Vector2.Zero, 150,
                            new Color(60, 90, 30), 1.4f);
                        d.noGravity = true;
                        d.velocity = -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f);
                    }
                }
            }
        }

        private void ApplyRootDamage(Vector2 center) {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;
                if (IsSafe(p.Center, center))
                    continue;

                int dmg = 60;
                if (Main.expertMode) dmg = 90;
                if (Main.masterMode) dmg = 120;
                p.Hurt(PlayerDeathReason.ByCustomReason(
                    Terraria.Localization.NetworkText.FromLiteral(p.name + " 被大椿的根须缠绕吞噬了")),
                    dmg, 0);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || intensity <= 0.02f)
                return false;

            NPC boss = (BossIndex >= 0 && BossIndex < Main.maxNPCs) ? Main.npc[BossIndex] : null;
            if (boss == null || !boss.active)
                return false;

            SpriteBatch sb = Main.spriteBatch;

            // —— 致命根须地面 (ArenaRunic, 暗林绿 → 强度升高转暖告警) ——
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                ACMShaders.WorldDecalParams(boss.Center, OrbitRadius + IslandRadius * 0.6f,
                    out Vector2 uv, out float radFrac, out float aspect);
                Color prim = Color.Lerp(new Color(20, 60, 22), new Color(90, 70, 20), intensity);
                Color sec = Color.Lerp(new Color(60, 110, 40), new Color(150, 110, 35), intensity);
                fx.Parameters["uTime"]?.SetValue(anim);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(radFrac);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f) * 0.85f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(prim.ToVector3(), 1f));
                fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(sec.ToVector3(), 1f));
                fx.Parameters["uRuneFreq"]?.SetValue(10f);
                fx.Parameters["uMode"]?.SetValue(1f); // 牢笼罩模式: 覆盖整个内部, 表达"被根须充满"
                fx.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.AlphaBlend);
            }

            // —— 常青安全岛 (玉青加性光盘, 明确"站这里安全") ——
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Vector2 go = glow.Size() / 2f;
                float p = 0.85f + MathF.Sin(anim * 3f) * 0.15f;
                for (int i = 0; i < IslandCount; i++) {
                    Vector2 ip = IslandPos(boss.Center, i) - Main.screenPosition;
                    float scale = IslandRadius * 2f / glow.Width;
                    Color safe = TelegraphColors.Safe with { A = 0 };
                    sb.Draw(glow, ip, null, safe * (0.35f * intensity * p), 0f, go, scale, SpriteEffects.None, 0f);
                    sb.Draw(glow, ip, null, (new Color(120, 230, 140, 0)) * (0.30f * intensity), 0f, go, scale * 0.7f, SpriteEffects.None, 0f);
                    sb.Draw(glow, ip, null, (Color.White with { A = 0 }) * (0.18f * intensity), 0f, go, scale * 0.35f, SpriteEffects.None, 0f);
                }

                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }

            return false;
        }
    }
}
