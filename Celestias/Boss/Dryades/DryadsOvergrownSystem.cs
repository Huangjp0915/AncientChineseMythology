using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精 P2「蔓生 Overgrown」翠色屏幕染色 (ElementalScreenTint)。
    /// 仅在树精存在且进入 P2 时生效, 随本地玩家与最近毒孢区距离增强——
    /// 让"空气因孢子而浓稠"成为体感, 强调 P2 新机制。
    /// 纯本地视觉: server-zero-draw, 受 MythologyConfig.FullscreenShadersEnabled 降级开关控制。
    /// 不占用全屏后处理名额 (ElementalScreenTint 不读 screenTarget, 走 DrawFullscreenOverlay)。
    /// </summary>
    public class DryadsOvergrownSystem : ModSystem
    {
        private float intensity;

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            float target = ComputeTarget();
            intensity = MathHelper.Lerp(intensity, target, 0.05f);
            if (intensity <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            // 毒绿: rgb + a=基础覆盖度
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.32f, 0.62f, 0.18f, 0.30f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(0.12f, 0.30f, 0.08f, 1f));
            fx.Parameters["uVignette"]?.SetValue(0.35f);
            fx.Parameters["uFogScale"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend);
        }

        private static float ComputeTarget() {
            int dryadType = ModContent.NPCType<Dryads>();
            NPC boss = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == dryadType && npc.active) { boss = npc; break; }
            }
            if (boss == null)
                return 0f;
            // 仅 P2
            if (boss.life >= boss.lifeMax * Dryads.Phase2Threshold)
                return 0f;

            Player p = Main.LocalPlayer;
            if (p == null || !p.active || p.dead)
                return 0f;

            // P2 基础氛围 + 靠近毒孢区增强
            float baseAmbiance = 0.12f;
            float proximity = 0f;
            int zoneType = ModContent.ProjectileType<DryadsSporeZone>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != zoneType)
                    continue;
                float d = Vector2.Distance(p.Center, proj.Center);
                float near = MathHelper.Clamp(1f - d / 700f, 0f, 1f);
                if (near > proximity)
                    proximity = near;
            }

            return MathHelper.Clamp(baseAmbiance + proximity * 0.30f, 0f, 0.45f);
        }
    }
}
