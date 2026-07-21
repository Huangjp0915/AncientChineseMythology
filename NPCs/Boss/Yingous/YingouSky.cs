using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    /// <summary>
    /// 赢勾黄泉夜幕 (V3 重绘, 类名与 LoadInstance 注册不变)。
    /// 分层: 渊底渐变 → 血色冥月晕 → 鬼火上浮motes → 顶底脉冲。
    /// 红度通道读 Boss 阶段 (ai[0] 已同步) 与血量: P3/死亡演出时血色拉满。
    /// 纯客户端视觉, 池化粒子零分配。
    /// </summary>
    internal class YingouSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float globalTime;
        private float redness;      //血色通道 0~1
        private const float MaxIntensity = 0.75f;
        internal static string name;

        //黄泉配色: 渊青黑 / 幽紫 / 鬼火青 / 冥血红
        private static readonly Color AbyssInk = new(8, 10, 18);
        private static readonly Color NetherDusk = new(26, 16, 46);
        private static readonly Color GhostTeal = new(90, 200, 165);
        private static readonly Color BloodMoon = new(150, 30, 38);

        //鬼火 motes (池化)
        private const int MoteCount = 36;
        private readonly GhostMote[] motes = new GhostMote[MoteCount];
        private bool motesInit;

        public static void LoadInstance() {
            name = "AncientChineseMythology:YingouSky";
            SkyManager.Instance[name] = new YingouSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = Math.Max(intensity, 0.01f);
            if (!motesInit) {
                motesInit = true;
                for (int i = 0; i < MoteCount; i++) motes[i].Reset(true);
            }
        }

        public override void Deactivate(params object[] args) { active = false; }
        public override bool IsActive() => active || intensity > 0.01f;
        public override void Reset() { active = false; intensity = 0f; }

        public override Color OnTileColor(Color inColor) {
            //黄泉压暗 + 微偏青紫
            float dim = intensity * 0.55f;
            Color tint = Color.Lerp(Color.White, new Color(120, 110, 150), dim);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A);
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.6f;

        public override void Update(GameTime gameTime) {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            NPC boss = GetBoss();
            if (boss != null && boss.active) {
                active = true;
                if (intensity < MaxIntensity)
                    intensity = MathF.Min(intensity + 0.008f, MaxIntensity);

                //血色: 血量越低越浓; P3/死亡演出拉满
                var phase = (Yingou.BossPhase)(int)boss.ai[0];
                float hpRed = 1f - MathHelper.Clamp((float)boss.life / boss.lifeMax, 0f, 1f);
                float targetRed = 0.2f + hpRed * 0.5f;
                if (phase == Yingou.BossPhase.Death) targetRed = 1f;
                else if (phase == Yingou.BossPhase.Transition3) targetRed = 0.9f;
                redness = MathHelper.Lerp(redness, targetRed, 0.015f);
            }
            else {
                intensity -= 0.012f;
                redness = MathHelper.Lerp(redness, 0f, 0.03f);
                if (intensity <= 0f) { intensity = 0f; Deactivate(); }
            }

            if (motesInit) {
                for (int i = 0; i < MoteCount; i++) motes[i].Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || !(maxDepth >= 0 && minDepth < 0))
                return;

            DrawGradient(spriteBatch);
            DrawNetherMoon(spriteBatch);
            DrawMotes(spriteBatch);
            DrawPulses(spriteBatch);
        }

        //层1: 渊底渐变 (顶部幽紫 → 底部渊黑)
        private void DrawGradient(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);
            sb.Draw(pixel, screen, AbyssInk * (intensity * 0.85f));

            int bands = 10;
            int h = Main.screenHeight / bands + 1;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                Color c = Color.Lerp(Color.Lerp(NetherDusk, BloodMoon, redness * 0.45f), AbyssInk, t) * (intensity * 0.4f);
                sb.Draw(pixel, new Rectangle(0, i * h, Main.screenWidth, h), c);
            }
        }

        //层2: 血色冥月晕 (高空定位, 随血色渐显)
        private void DrawNetherMoon(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 pos = new(Main.screenWidth * 0.72f, Main.screenHeight * 0.2f);
            float breath = 1f + MathF.Sin(globalTime * 0.7f) * 0.05f;
            //外晕
            Color halo = Color.Lerp(NetherDusk, BloodMoon, 0.4f + redness * 0.6f) * (intensity * (0.3f + redness * 0.35f));
            halo.A = 0;
            sb.Draw(glow, pos, null, halo, 0f, glow.Size() / 2f, 9f * breath, SpriteEffects.None, 0f);
            //内核
            Color core = Color.Lerp(new Color(200, 140, 140), BloodMoon, redness) * (intensity * 0.5f);
            core.A = 0;
            sb.Draw(glow, pos, null, core, 0f, glow.Size() / 2f, 3.2f * breath, SpriteEffects.None, 0f);
        }

        //层3: 鬼火 motes 上浮 (青绿摇曳, 低血转赤)
        private void DrawMotes(SpriteBatch sb) {
            Texture2D star = ACMAsset.BlankStar;
            Texture2D glow = ACMAsset.SoftGlow;
            if (star == null || glow == null || !motesInit) return;
            Vector2 starOrigin = star.Size() / 2f;
            Vector2 glowOrigin = glow.Size() / 2f;

            for (int i = 0; i < MoteCount; i++) {
                ref GhostMote m = ref motes[i];
                float life = MathF.Sin(m.Progress * MathHelper.Pi);
                float alpha = life * intensity * 0.5f;
                if (alpha < 0.01f) continue;

                //屏幕空间位置 (视差: 深层慢移)
                Vector2 pos = new(
                    ((m.X - Main.screenPosition.X * m.Parallax) % (Main.screenWidth + 160) + Main.screenWidth + 160) % (Main.screenWidth + 160) - 80,
                    ((m.Y - Main.screenPosition.Y * m.Parallax) % (Main.screenHeight + 160) + Main.screenHeight + 160) % (Main.screenHeight + 160) - 80);

                Color c = Color.Lerp(GhostTeal, BloodMoon, redness * 0.6f) * alpha;
                c.A = 0;
                sb.Draw(glow, pos, null, c * 0.7f, 0f, glowOrigin, m.Scale * 0.9f, SpriteEffects.None, 0f);
                sb.Draw(star, pos, null, c, m.Rot + globalTime * 0.3f, starOrigin, m.Scale * 0.11f, SpriteEffects.None, 0f);
            }
        }

        //层4: 顶部血脉冲 + 底部渊涌
        private void DrawPulses(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (MathF.Sin(globalTime * 0.9f) * 0.5f + 0.5f) * intensity * (0.05f + redness * 0.06f);
            Color top = Color.Lerp(NetherDusk, BloodMoon, redness) * pulse;
            top.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 5), top);

            Color btm = Color.Lerp(GhostTeal, NetherDusk, 0.6f) * (pulse * 0.6f);
            btm.A = 0;
            sb.Draw(pixel, new Rectangle(0, Main.screenHeight * 4 / 5, Main.screenWidth, Main.screenHeight / 5), btm);
        }

        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Yingou>()) return npc;
            }
            return null;
        }

        //鬼火 mote: 世界系缓慢上浮 + 左右摇曳, 池化复用
        private struct GhostMote
        {
            public float X, Y;       //伪世界坐标 (仅用于视差取模)
            public float Progress;   //0~1 生命
            public float Speed;
            public float Scale;
            public float Rot;
            public float Parallax;   //0.3~0.8 深度视差
            public float SwayPhase;

            public void Reset(bool randomProgress) {
                X = Main.rand.NextFloat(0f, 4000f);
                Y = Main.rand.NextFloat(0f, 3000f);
                Progress = randomProgress ? Main.rand.NextFloat() : 0f;
                Speed = Main.rand.NextFloat(0.0012f, 0.0032f);
                Scale = Main.rand.NextFloat(0.5f, 1.4f);
                Rot = Main.rand.NextFloat(MathHelper.TwoPi);
                Parallax = Main.rand.NextFloat(0.3f, 0.8f);
                SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public void Update() {
                Progress += Speed;
                Y -= 0.55f;                                        //上浮
                X += MathF.Sin(SwayPhase + Progress * 9f) * 0.4f;  //摇曳
                if (Progress >= 1f) Reset(false);
            }
        }
    }
}
