using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天庭视觉效果处理器
    /// 当玩家靠近天柱时触发神圣的天庭风格效果
    /// </summary>
    public class HeavenlyEffect : ModPlayer
    {
        #region 效果状态
        /// <summary>当前天柱影响强度（0-1）</summary>
        public float PillarInfluence { get; private set; }

        /// <summary>效果淡入淡出插值</summary>
        private float effectFade;

        /// <summary>神圣光效计时器</summary>
        private float divineGlowTimer;

        /// <summary>云雾粒子计时器</summary>
        private int cloudParticleTimer;

        /// <summary>神光射线计时器</summary>
        private float lightRayTimer;

        /// <summary>环境音效冷却</summary>
        private int ambientSoundCooldown;

        /// <summary>是否处于天柱区域</summary>
        public bool IsInPillarZone { get; private set; }

        /// <summary>最近天柱索引</summary>
        private int nearestPillarIndex = -1;
        #endregion

        #region 效果参数
        /// <summary>效果完全激活的距离阈值</summary>
        private const float FullEffectDistance = 400f;

        /// <summary>效果开始的距离阈值</summary>
        private const float EffectStartDistance = HeavenPillarActor.EffectRadius;

        /// <summary>云雾生成间隔</summary>
        private const int CloudSpawnInterval = 4;

        /// <summary>神光粒子生成间隔</summary>
        private const int DivineDustInterval = 2;
        #endregion

        public override void ResetEffects() {
            // 每帧重新计算影响
            PillarInfluence = 0f;
            IsInPillarZone = false;
            nearestPillarIndex = -1;
        }

        public override void PostUpdate() {
            if (!HeavenPillarSystem.PillarsDescended) {
                effectFade = MathHelper.Lerp(effectFade, 0f, 0.05f);
                return;
            }

            // 计算天柱影响
            PillarInfluence = HeavenPillarSystem.GetPillarInfluence(Player.Center);
            IsInPillarZone = PillarInfluence > 0f;
            nearestPillarIndex = HeavenPillarSystem.GetNearestPillarIndex(Player.Center);

            // 平滑淡入淡出
            float targetFade = IsInPillarZone ? PillarInfluence : 0f;
            effectFade = MathHelper.Lerp(effectFade, targetFade, 0.08f);

            // 更新计时器
            divineGlowTimer += 0.02f * (1f + effectFade);
            lightRayTimer += 0.015f;
            cloudParticleTimer++;

            if (ambientSoundCooldown > 0) ambientSoundCooldown--;

            // 应用视觉效果
            if (effectFade > 0.05f) {
                ApplyHeavenlyEffects();
            }
        }

        /// <summary>
        /// 应用天庭视觉效果
        /// </summary>
        private void ApplyHeavenlyEffects() {
            if (Main.dedServ) return;

            // 神圣光粒效果
            SpawnDivineParticles();

            // 云雾效果
            SpawnCloudEffects();

            // 神光射线效果
            SpawnLightRayEffects();

            // 屏幕边缘神圣光晕（通过调整光照）
            ApplyDivineLighting();

            // 环境音效
            PlayAmbientSounds();
        }

        /// <summary>
        /// 生成神圣光粒
        /// </summary>
        private void SpawnDivineParticles() {
            if (cloudParticleTimer % DivineDustInterval != 0) return;

            int particleCount = (int)(3 * effectFade);
            for (int i = 0; i < particleCount; i++) {
                // 在玩家周围生成上升的神圣光粒
                Vector2 spawnPos = Player.Center + Main.rand.NextVector2Circular(300f, 200f) * effectFade;

                // 金色神圣光粒
                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.WhiteTorch;
                int dust = Dust.NewDust(spawnPos, 0, 0, dustType, 0, -2f, 150, default, Main.rand.NextFloat(0.8f, 1.5f) * effectFade);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-3f, -1f));
                Main.dust[dust].fadeIn = 1.2f;

                // 添加少量翠绿色神光（对应天柱颜色）
                if (Main.rand.NextBool(5)) {
                    int jadeDust = Dust.NewDust(spawnPos + Main.rand.NextVector2Circular(50, 50), 0, 0, DustID.JungleGrass, 0, -1.5f, 100, default, Main.rand.NextFloat(1f, 1.8f) * effectFade);
                    Main.dust[jadeDust].noGravity = true;
                    Main.dust[jadeDust].velocity *= 0.8f;
                }
            }
        }

        /// <summary>
        /// 生成云雾效果
        /// </summary>
        private void SpawnCloudEffects() {
            if (cloudParticleTimer % CloudSpawnInterval != 0) return;
            if (effectFade < 0.3f) return;

            int cloudCount = (int)(2 * effectFade);
            for (int i = 0; i < cloudCount; i++) {
                // 祥云效果 - 在玩家下方和周围
                Vector2 cloudPos = Player.Center + new Vector2(
                    Main.rand.NextFloat(-400f, 400f) * effectFade,
                    Main.rand.NextFloat(-100f, 200f)
                );

                int cloud = Dust.NewDust(cloudPos, 0, 0, DustID.Cloud, 
                    Main.rand.NextFloat(-0.5f, 0.5f), 
                    Main.rand.NextFloat(-0.3f, 0.3f), 
                    200, 
                    default, 
                    Main.rand.NextFloat(2f, 4f) * effectFade);
                Main.dust[cloud].noGravity = true;
                Main.dust[cloud].velocity *= 0.5f;
            }
        }

        /// <summary>
        /// 生成神光射线效果
        /// </summary>
        private void SpawnLightRayEffects() {
            if (effectFade < 0.5f) return;
            if (cloudParticleTimer % 8 != 0) return;

            // 从天空降下的神光射线
            float rayAngle = MathF.Sin(lightRayTimer + Main.rand.NextFloat(-0.2f, 0.2f)) * 0.3f;
            Vector2 rayStart = Player.Center + new Vector2(Main.rand.NextFloat(-200f, 200f), -400f);
            Vector2 rayDir = new Vector2(MathF.Sin(rayAngle), MathF.Cos(rayAngle));

            for (int i = 0; i < 8; i++) {
                Vector2 dustPos = rayStart + rayDir * (i * 50);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, rayDir.X * 2, rayDir.Y * 2, 100, default, Main.rand.NextFloat(1.2f, 2f) * effectFade);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 1.5f;
            }
        }

        /// <summary>
        /// 应用神圣光照效果
        /// </summary>
        private void ApplyDivineLighting() {
            if (effectFade < 0.1f) return;

            // 在玩家周围添加神圣光照
            float pulseIntensity = 0.5f + MathF.Sin(divineGlowTimer) * 0.2f;
            Vector3 divineLight = new Vector3(1f, 0.95f, 0.85f) * effectFade * pulseIntensity * 0.4f;

            // 多点光源营造神圣氛围
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4 + divineGlowTimer * 0.5f;
                Vector2 lightPos = Player.Center + angle.ToRotationVector2() * 100f;
                Lighting.AddLight(lightPos, divineLight);
            }

            // 头顶光环
            Lighting.AddLight(Player.Center + new Vector2(0, -50), divineLight * 1.3f);
        }

        /// <summary>
        /// 播放环境音效
        /// </summary>
        private void PlayAmbientSounds() {
            if (ambientSoundCooldown > 0) return;
            if (effectFade < 0.6f) return;

            // 神圣铃声或风铃效果
            if (Main.rand.NextBool(180)) {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29 with { 
                    Volume = 0.3f * effectFade, 
                    Pitch = 0.5f + Main.rand.NextFloat(-0.1f, 0.2f),
                    MaxInstances = 2
                }, Player.Center);
                ambientSoundCooldown = 120;
            }
        }

        public override void ModifyScreenPosition() {
            // 轻微的屏幕震动效果（当效果很强时）
            if (effectFade > 0.7f && Main.rand.NextBool(60)) {
                Main.screenPosition += Main.rand.NextVector2Circular(1f, 1f) * (effectFade - 0.7f) * 3f;
            }
        }
    }

    /// <summary>
    /// 天柱区域的全局背景效果
    /// </summary>
    public class HeavenlyBackgroundEffect : ModSystem
    {
        private static float skyOverlayAlpha = 0f;
        private static float cloudScrollOffset = 0f;

        public override void PostUpdateWorld() {
            if (!HeavenPillarSystem.PillarsDescended) {
                skyOverlayAlpha = MathHelper.Lerp(skyOverlayAlpha, 0f, 0.02f);
                return;
            }

            // 检查任意玩家是否在天柱区域
            float maxInfluence = 0f;
            foreach (Player player in Main.ActivePlayers) {
                float influence = HeavenPillarSystem.GetPillarInfluence(player.Center);
                if (influence > maxInfluence) {
                    maxInfluence = influence;
                }
            }

            // 平滑过渡
            float targetAlpha = maxInfluence * 0.6f;
            skyOverlayAlpha = MathHelper.Lerp(skyOverlayAlpha, targetAlpha, 0.03f);

            // 云层滚动
            cloudScrollOffset += 0.5f * (1f + skyOverlayAlpha);
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (skyOverlayAlpha < 0.05f) return;

            // 增加神圣的金色调
            Color divineColor = new Color(255, 250, 230);
            backgroundColor = Color.Lerp(backgroundColor, divineColor, skyOverlayAlpha * 0.3f);

            // 稍微提亮物块颜色
            tileColor = Color.Lerp(tileColor, Color.White, skyOverlayAlpha * 0.15f);
        }

        public override void ModifyLightingBrightness(ref float scale) {
            if (skyOverlayAlpha > 0.1f) {
                // 在天柱区域增加整体亮度
                scale += skyOverlayAlpha * 0.1f;
            }
        }
    }
}
