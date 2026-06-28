using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes
{
    /// <summary>
    /// 幽冥狐火魂弹 (Nether Foxfire Soul) —— 取代原版 <c>CultistBossLightningOrb</c> 占位弹的自定义青蓝魂火。
    ///
    /// 三种形态由 <see cref="Variant"/> 区分 (V2《虚实九影》真假博弈):
    ///   0 = 实·狐火 (青蓝芯, 实体伤害, 命中叠魂蚀)；
    ///   1 = 虚·幻影 (幽紫半透, damage=0 → 无害, 仅作真假误导)；
    ///   2 = 真身·裁决 (柔白芯, 实体伤害 + 命中叠冥律, P3 真身专用)。
    ///
    /// 纹理安全: <see cref="Texture"/> 锚定同目录尾尖贴图 (合法自动加载点), 绘制走 <see cref="ACMAsset.SoftGlow"/>
    /// 程序化魂火 (无新美术依赖)。服务端零绘制。
    /// </summary>
    public class NetherFoxfireSoul : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Boss/NetherKitsunes/NetherMissesTop";

        /// <summary>0=实狐火 1=虚幻影 2=真身裁决。</summary>
        public ref float Variant => ref Projectile.ai[0];
        private ref float WavePhase => ref Projectile.ai[1];

        private bool IsIllusion => (int)Variant == 1;
        private bool IsJudgment => (int)Variant == 2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 260;
            Projectile.alpha = 255;          // 出场渐显 (telegraph)
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            // 出场渐显: 命中前 ~0.4s 由淡转实, 给可读预警窗口
            if (Projectile.alpha > 0)
                Projectile.alpha = Math.Max(0, Projectile.alpha - 22);

            // 狐火飘忽: 沿速度法线轻微正弦摆动 (鬼火质感, 不改变整体航向)
            WavePhase += 0.22f;
            float speed = Projectile.velocity.Length();
            if (speed > 0.01f) {
                Vector2 fwd = Projectile.velocity / speed;
                Vector2 perp = new Vector2(-fwd.Y, fwd.X);
                Projectile.position += perp * MathF.Sin(WavePhase) * 1.1f;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            Color c = SoulColor();
            if (!Main.dedServ && Main.rand.NextBool(IsIllusion ? 3 : 2)) {
                Vector2 dpos = Projectile.Center + Main.rand.NextVector2Circular(6f, 6f);
                int d = Dust.NewDust(dpos, 1, 1, IsJudgment ? DustID.WhiteTorch : DustID.BlueTorch, 0, 0,
                    IsIllusion ? 170 : 120, c, IsIllusion ? 1.1f : 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(0.8f, 0.8f);
            }

            Lighting.AddLight(Projectile.Center, c.ToVector3() * (IsIllusion ? 0.25f : 0.45f));
        }

        private Color SoulColor() => (int)Variant switch {
            1 => new Color(160, 120, 230),  // 虚·幻影 幽紫
            2 => new Color(235, 245, 255),  // 真身·裁决 柔白
            _ => new Color(130, 210, 255),  // 实·狐火 青蓝
        };

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 地府身份层: 实体狐火命中叠魂蚀; 真身裁决额外叠冥律 (轻度接入)
            UnderworldField.AddSoulErosion(target, IsJudgment ? 2 : 1);
            if (IsJudgment)
                UnderworldField.AddNetherDecree(target, 1);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            Color c = SoulColor();
            for (int i = 0; i < (IsIllusion ? 5 : 9); i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                int d = Dust.NewDust(Projectile.Center, 1, 1, IsJudgment ? DustID.WhiteTorch : DustID.BlueTorch,
                    vel.X, vel.Y, 100, c, 1.5f);
                Main.dust[d].noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;

            Vector2 origin = glow.Size() * 0.5f;
            float vis = 1f - Projectile.alpha / 255f;
            float pulse = 1f + 0.15f * MathF.Sin(WavePhase * 1.7f);
            Color soul = SoulColor();
            Color outer = IsIllusion ? new Color(90, 60, 160) : new Color(80, 120, 200);

            // 魂火拖尾 (取历史点, 加性柔光叠层)
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                Vector2 op = Projectile.oldPos[i];
                if (op == Vector2.Zero)
                    continue;
                float t = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 dp = op + Projectile.Size * 0.5f - Main.screenPosition;
                Color tc = Color.Lerp(outer, soul, t) * (vis * t * (IsIllusion ? 0.28f : 0.45f));
                tc.A = 0;
                float ts = (Projectile.width / glow.Width) * (0.5f + t * 0.5f) * (IsIllusion ? 1.1f : 1.4f);
                sb.Draw(glow, dp, null, tc, 0f, origin, ts, SpriteEffects.None, 0f);
            }

            Vector2 center = Projectile.Center - Main.screenPosition;
            float coreScale = Projectile.width / (float)glow.Width;

            // 外晕
            Color halo = outer * (vis * 0.5f); halo.A = 0;
            sb.Draw(glow, center, null, halo, 0f, origin, coreScale * 2.4f * pulse, SpriteEffects.None, 0f);
            // 主魂火
            Color mid = soul * (vis * (IsIllusion ? 0.55f : 0.85f)); mid.A = 0;
            sb.Draw(glow, center, null, mid, 0f, origin, coreScale * 1.5f * pulse, SpriteEffects.None, 0f);
            // 高亮芯
            Color core = Color.Lerp(soul, Color.White, IsJudgment ? 0.7f : 0.45f) * vis; core.A = 0;
            sb.Draw(glow, center, null, core, 0f, origin, coreScale * 0.8f, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
            return false;
        }
    }
}
