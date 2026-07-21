using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 风场模式 — 由 <see cref="Aoshun"/> 每帧发布，驱动整批风线/碎屑的集体行为。
    /// 风向即预警：风开始向焦点汇聚 = 它要出手了。
    /// </summary>
    public enum AoshunWindMode
    {
        Off,        // 无风（战斗外）
        Ambient,    // 环境风：整体横向流动 + 阵风起伏
        Converge,   // 汇聚：全部风线加速涌向焦点（挥刃/穿刺/唤雷前摇）
        Ring,       // 环流：绕焦点旋转（电网/风暴之眼）
        Burst,      // 爆散：从焦点向外炸开（爆发瞬间）
        Collapse    // 坍缩：强吸入焦点并螺旋（深潜/死亡演出）
    }

    /// <summary>
    /// 敖顺·风场粒子池 — "风的可见化"核心（纯客户端视觉，零每帧分配）。
    /// 固定 96 槽风线/碎屑，按 <see cref="Publish"/> 的模式集体演化；
    /// 风线画成沿速度拉伸的 LightShot 亮条，碎屑画成旋转 EmberShards 小块。
    /// 由 <see cref="AoshunStormScreenSystem.PostDrawTiles"/> 驱动更新与绘制（位于实体之下，不遮挡躲避信息）。
    /// TrailQuality: Med 半量、Off 关闭。
    /// </summary>
    public static class AoshunWindField
    {
        private struct WindParticle
        {
            public Vector2 Pos;      // 世界坐标
            public Vector2 Vel;
            public float Life;       // 剩余寿命(帧)
            public float MaxLife;
            public float Scale;
            public float Spin;       // 碎屑自旋
            public float Rot;
            public bool Debris;      // true=EmberShards 碎屑, false=LightShot 风线
            public int Shard;        // 碎屑取样 0~8
        }

        private const int PoolSize = 96;
        private static readonly WindParticle[] pool = new WindParticle[PoolSize];

        private static AoshunWindMode mode = AoshunWindMode.Off;
        private static Vector2 focus;          // 模式焦点(世界)
        private static float strength = 1f;    // 0~1 全局强度
        private static Vector2 ambientDir = Vector2.UnitX;
        private static ulong lastPublishFrame;
        private static int spawnCursor;

        /// <summary>由 Boss 每帧发布风场状态（纯本地视觉，服务端不调用）。</summary>
        public static void Publish(AoshunWindMode newMode, Vector2 focusWorld, float newStrength, Vector2 windDir) {
            mode = newMode;
            focus = focusWorld;
            strength = MathHelper.Clamp(newStrength, 0f, 1f);
            if (windDir.LengthSquared() > 0.001f)
                ambientDir = Vector2.Normalize(windDir);
            lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>当前发布的风向（供全屏着色器共用同一气流语言）。</summary>
        public static Vector2 CurrentWindDir => ambientDir;

        public static void Clear() {
            for (int i = 0; i < PoolSize; i++)
                pool[i].Life = 0f;
            mode = AoshunWindMode.Off;
        }

        /// <summary>每帧更新（由屏幕系统在 PostDrawTiles 调用；已保证非服务端）。</summary>
        public static void Update() {
            // Boss 停止发布后风自然平息
            if (Main.GameUpdateCount - lastPublishFrame > 4)
                mode = AoshunWindMode.Off;
            if (Main.gamePaused)
                return;

            int alive = 0;
            for (int i = 0; i < PoolSize; i++) {
                ref WindParticle p = ref pool[i];
                if (p.Life <= 0f)
                    continue;
                alive++;

                StepParticle(ref p);
                p.Pos += p.Vel;
                p.Rot += p.Spin;
                p.Life -= 1f;

                // 飞出屏幕过远直接回收
                Vector2 screenRel = p.Pos - Main.screenPosition;
                if (screenRel.X < -700 || screenRel.X > Main.screenWidth + 700 ||
                    screenRel.Y < -700 || screenRel.Y > Main.screenHeight + 700)
                    p.Life = 0f;
            }

            if (mode == AoshunWindMode.Off || strength <= 0.02f)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;

            int cap = MythologyConfig.Trail == TrailQualityLevel.Med ? PoolSize / 2 : PoolSize;
            cap = (int)(cap * MathHelper.Clamp(0.3f + strength * 0.7f, 0f, 1f));
            // 每帧至多补 3 个，避免模式切换瞬间爆量
            int budget = 3;
            for (int n = 0; n < PoolSize && alive < cap && budget > 0; n++) {
                spawnCursor = (spawnCursor + 1) % PoolSize;
                ref WindParticle p = ref pool[spawnCursor];
                if (p.Life > 0f)
                    continue;
                SpawnParticle(ref p);
                alive++;
                budget--;
            }
        }

        private static void StepParticle(ref WindParticle p) {
            switch (mode) {
                case AoshunWindMode.Ambient: {
                    // 横向风 + 正弦阵风（确定性，不逐帧随机）
                    float gust = MathF.Sin(Main.GameUpdateCount * 0.02f + p.Pos.Y * 0.003f) * 0.35f;
                    Vector2 target = ambientDir * (7f + gust * 5f) + new Vector2(0, MathF.Sin(p.Pos.X * 0.004f) * 1.4f);
                    p.Vel = Vector2.Lerp(p.Vel, target * (0.6f + strength * 0.6f), 0.05f);
                    break;
                }
                case AoshunWindMode.Converge: {
                    // 比例吸力 + 少量切向 → 有旋的涌入（MOTION.md §6 蓄力语法）
                    Vector2 toFocus = focus - p.Pos;
                    float dist = toFocus.Length();
                    if (dist < 60f) { p.Life = Math.Min(p.Life, 6f); break; }
                    Vector2 dir = toFocus / dist;
                    Vector2 tang = new(-dir.Y, dir.X);
                    p.Vel = Vector2.Lerp(p.Vel, dir * (9f + strength * 9f) + tang * 3.2f, 0.08f);
                    break;
                }
                case AoshunWindMode.Ring: {
                    Vector2 toFocus = focus - p.Pos;
                    float dist = toFocus.Length();
                    Vector2 dir = dist > 1f ? toFocus / dist : Vector2.UnitX;
                    Vector2 tang = new(-dir.Y, dir.X);
                    // 切向环流为主，微弱向心保持环带
                    p.Vel = Vector2.Lerp(p.Vel, tang * (8f + strength * 6f) + dir * 0.9f, 0.06f);
                    break;
                }
                case AoshunWindMode.Burst: {
                    Vector2 fromFocus = p.Pos - focus;
                    Vector2 dir = fromFocus.SafeNormalize(Vector2.UnitX);
                    p.Vel = Vector2.Lerp(p.Vel, dir * (13f + strength * 10f), 0.12f);
                    break;
                }
                case AoshunWindMode.Collapse: {
                    Vector2 toFocus = focus - p.Pos;
                    float dist = toFocus.Length();
                    if (dist < 40f) { p.Life = Math.Min(p.Life, 4f); break; }
                    Vector2 dir = toFocus / dist;
                    Vector2 tang = new(-dir.Y, dir.X);
                    // 强吸 + 强旋 = 风暴被收回体内
                    p.Vel = Vector2.Lerp(p.Vel, dir * (14f + strength * 12f) + tang * 7f, 0.10f);
                    break;
                }
                default:
                    p.Vel *= 0.94f;
                    break;
            }
        }

        private static void SpawnParticle(ref WindParticle p) {
            p.Debris = Main.rand.NextBool(4); // 1/4 是被吸起的碎屑
            p.Shard = Main.rand.Next(9);
            p.MaxLife = p.Life = Main.rand.NextFloat(70f, 150f);
            p.Scale = Main.rand.NextFloat(0.55f, 1.15f);
            p.Spin = p.Debris ? Main.rand.NextFloat(-0.18f, 0.18f) : 0f;
            p.Rot = Main.rand.NextFloat(MathHelper.TwoPi);

            Vector2 screenTL = Main.screenPosition;
            switch (mode) {
                case AoshunWindMode.Converge:
                case AoshunWindMode.Collapse:
                    // 从焦点外围 300~700px 生成，向内涌
                    p.Pos = focus + Main.rand.NextVector2Unit() * Main.rand.NextFloat(300f, 700f);
                    p.Vel = (focus - p.Pos).SafeNormalize(Vector2.UnitX) * 3f;
                    break;
                case AoshunWindMode.Ring:
                    p.Pos = focus + Main.rand.NextVector2Unit() * Main.rand.NextFloat(220f, 560f);
                    p.Vel = Vector2.Zero;
                    break;
                case AoshunWindMode.Burst:
                    p.Pos = focus + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 140f);
                    p.Vel = (p.Pos - focus).SafeNormalize(Vector2.UnitX) * 8f;
                    p.Life = p.MaxLife = Main.rand.NextFloat(35f, 70f);
                    break;
                default: {
                    // 环境风: 从迎风侧屏幕外进入
                    bool fromLeft = ambientDir.X >= 0f;
                    float x = fromLeft ? screenTL.X - Main.rand.NextFloat(80f, 300f)
                                       : screenTL.X + Main.screenWidth + Main.rand.NextFloat(80f, 300f);
                    p.Pos = new Vector2(x, screenTL.Y + Main.rand.NextFloat(Main.screenHeight));
                    p.Vel = ambientDir * Main.rand.NextFloat(4f, 8f);
                    break;
                }
            }
        }

        /// <summary>绘制（已保证非服务端；调用方保证无活动批，自开自合）。</summary>
        public static void Draw() {
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;
            Texture2D lineTex = ACMAsset.LightShot;
            Texture2D shardTex = ACMAsset.EmberShards;
            if (lineTex == null)
                return;

            bool any = false;
            for (int i = 0; i < PoolSize; i++) {
                if (pool[i].Life > 0f) { any = true; break; }
            }
            if (!any)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 lineOrigin = lineTex.Size() / 2f;
            int shardW = shardTex != null ? shardTex.Width / 3 : 1;
            int shardH = shardTex != null ? shardTex.Height / 3 : 1;
            Vector2 shardOrigin = new(shardW / 2f, shardH / 2f);

            for (int i = 0; i < PoolSize; i++) {
                ref WindParticle p = ref pool[i];
                if (p.Life <= 0f)
                    continue;

                float lifeT = p.Life / p.MaxLife;
                // 出生/消亡两端淡入淡出
                float fade = MathF.Min(1f, MathF.Min((1f - lifeT) * 6f, lifeT * 3f));
                float speed = p.Vel.Length();

                if (p.Debris && shardTex != null) {
                    // 被吸起的碎屑：暗青灰小块，速度快时更亮
                    float bright = MathHelper.Clamp(speed / 16f, 0.25f, 0.9f);
                    Color c = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.NorthSeaCyan, bright * 0.6f)
                        * (fade * 0.5f * bright);
                    c.A = 0;
                    Rectangle src = new(p.Shard % 3 * shardW, p.Shard / 3 * shardH, shardW, shardH);
                    sb.Draw(shardTex, p.Pos - Main.screenPosition, src, c, p.Rot, shardOrigin,
                        0.05f + p.Scale * 0.04f, SpriteEffects.None, 0f);
                }
                else {
                    // 风线：沿速度方向拉伸的亮条，速度门控亮度（快才亮 → 自动放大快节奏时刻）
                    if (speed < 1.2f)
                        continue;
                    float stretch = MathHelper.Clamp(speed * 0.09f, 0.25f, 1.6f);
                    float bright = MathHelper.Clamp((speed - 2f) / 14f, 0.08f, 0.85f);
                    Color c = Color.Lerp(AoshunHelper.NorthSeaCyan, AoshunHelper.ElectricWhite, bright * 0.7f)
                        * (fade * bright * 0.55f);
                    c.A = 0;
                    float rot = p.Vel.ToRotation();
                    sb.Draw(lineTex, p.Pos - Main.screenPosition, null, c, rot, lineOrigin,
                        new Vector2(stretch * p.Scale, 0.10f * p.Scale), SpriteEffects.None, 0f);
                }
            }

            sb.End();
        }
    }
}
