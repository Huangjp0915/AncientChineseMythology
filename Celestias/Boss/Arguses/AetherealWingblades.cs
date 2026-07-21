using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 星瞳哨兵 — Argus"百目"的分体之眼 (类名保留自旧版羽翼光刃, 跨文件引用不变)。
    ///
    /// 模式 (ai[0]):
    ///   0 = 直飞光刃 — 旧版兼容: 沿初速直线飞行的星羽光刃 (接触伤害)。
    ///   1 = 哨兵眼 — 部署后按时间线执行完整凝视语法, 自身无接触伤害:
    ///       休眠 ai[1] tick → 睁眼 18f → 追踪 26f (细紫线随目标) → 锁定 12f
    ///       (锁定角写入 ai[2], 服务器 netUpdate 下发权威值, 弹道就此冻结) → 熄灭 4f
    ///       → 发射 1 支星箭 → 余辉 12f → 淡出 24f 消散。
    /// 视觉: ArgusEyeIris 程序化星瞳, 同帧全部哨兵单批绘制。
    /// </summary>
    public class AetherealWingblades : ModProjectile
    {
        //哨兵时间线常量 (自睁眼起)
        private const int OpenDur = 18;
        private const int TrackEnd = OpenDur + 26;   // 44
        private const int LockEnd = TrackEnd + 12;   // 56
        private const int DarkEnd = LockEnd + 4;     // 60
        private const int FireTick = DarkEnd;        // 60
        private const int LingerEnd = FireTick + 12; // 72
        private const int FadeEnd = LingerEnd + 24;  // 96

        private static Asset<Effect> eyeIrisRef;

        private static Effect EyeIrisEffect {
            get {
                if (Main.dedServ)
                    return null;
                eyeIrisRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/ArgusEyeIris", AssetRequestMode.ImmediateLoad);
                return eyeIrisRef?.Value;
            }
        }

        private static ulong batchDrawnFrame;

        private ref float Mode => ref Projectile.ai[0];
        private ref float OpenDelay => ref Projectile.ai[1];
        private ref float LockedAngle => ref Projectile.ai[2];

        private ref float LifeTimer => ref Projectile.localAI[0];

        private bool IsSentry => Mode == 1f;

        /// <summary>哨兵局部时间 (睁眼起算, 休眠期为负)。</summary>
        private float SentryTime => LifeTimer - OpenDelay;

        public override void SetStaticDefaults() {
            //首眼代绘全体 + 视线跨屏 → 出屏也要绘制
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 320;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            LifeTimer++;

            if (!IsSentry) {
                //模式0: 直飞光刃 (旧版兼容)
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                        0, 0, DustID.BlueTorch, -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                        110, default, 1.1f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.35f, 0.35f, 0.6f);
                return;
            }

            //哨兵眼: 定身 (预减抵消引擎位移)
            Projectile.position -= Projectile.velocity;
            float t = SentryTime;

            //休眠期微光
            if (t < 0f) {
                if (!Main.dedServ && Main.rand.NextBool(10)) {
                    Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(14, 14),
                        0, 0, DustID.PurpleTorch, 0, 0, 170, default, 0.6f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
                return;
            }

            //睁眼瞬间
            if (t == 0f && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.45f }, Projectile.Center);

            //追踪期: 各端朝最近玩家转向 (纯视觉); 锁定角由锁定帧统一裁定
            if (t < TrackEnd) {
                int closest = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                if (closest >= 0) {
                    Player p = Main.player[closest];
                    float aim = (p.Center - Projectile.Center).ToRotation();
                    Projectile.rotation = Projectile.rotation.AngleLerp(aim, 0.18f);
                }
            }
            else {
                Projectile.rotation = LockedAngle;
            }

            //锁定裁定: 各端同式预测消除视觉延迟, 服务器 netUpdate 下发权威角 (之后弹道冻结 — 绝不追身)
            if (t == TrackEnd) {
                int closest = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                if (closest >= 0) {
                    Player p = Main.player[closest];
                    //箭 extraUpdates=1 → 拦截解按实效速度 42px/f 求解
                    Vector2 dir = ACMUtils.LeadTarget(Projectile.Center, p.Center, p.velocity, 42f);
                    LockedAngle = dir.ToRotation();
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        Projectile.netUpdate = true;
                }
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
            }

            //发射 (服务器沿冻结角生成星箭)
            if (t == FireTick) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 vel = LockedAngle.ToRotationVector2() * 21f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        ModContent.ProjectileType<StarSightArrows>(), Projectile.damage, 1f, Main.myPlayer);
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.PurpleTorch, 0, 0, 70, default, 1.5f);
                        d.noGravity = true;
                        d.velocity = LockedAngle.ToRotationVector2().RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 7f);
                    }
                }
            }

            //时间线走完 → 消散
            if (t >= FadeEnd)
                Projectile.Kill();

            float glow = MathHelper.Clamp(t / OpenDur, 0f, 1f) * (t > LingerEnd ? 1f - (t - LingerEnd) / (FadeEnd - LingerEnd) : 1f);
            Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.25f * glow, 0.7f * glow);
        }

        //哨兵本体无接触伤害 (伤害只来自可读的箭)
        public override bool CanHitPlayer(Player target) => !IsSentry;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 90, default, 1.2f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //模式0 光刃: 默认批内加性流光
            if (!IsSentry) {
                DrawBladeMode();
                return false;
            }

            //哨兵: 预告线画在默认批 (每颗自绘, 便宜), 眼体单批集中
            DrawSentryLine();

            if (batchDrawnFrame != Main.GameUpdateCount) {
                batchDrawnFrame = Main.GameUpdateCount;
                DrawAllSentryEyes();
            }
            return false;
        }

        private void DrawBladeMode() {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D streak = ACMAsset.LightShot;
            Texture2D star = ACMAsset.BlankStar;
            if (streak == null || star == null)
                return;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color c = new Color(190, 215, 255) with { A = 0 };
            sb.Draw(streak, drawPos, null, c * 0.8f, Projectile.rotation,
                new Vector2(streak.Width * 0.8f, streak.Height / 2f), new Vector2(0.28f, 0.07f), SpriteEffects.None, 0f);
            sb.Draw(star, drawPos, null, c, LifeTimer * 0.15f, star.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
        }

        /// <summary>凝视预告线: 追踪期细紫游移 → 锁定期红亮冻结 → 熄灭一拍 → 射击余辉。</summary>
        private void DrawSentryLine() {
            float t = SentryTime;
            if (t < OpenDur)
                return;

            Texture2D streak = ACMAsset.LightShot;
            if (streak == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            if (t < TrackEnd) {
                //追踪: 细紫线, 亮度渐升
                float p = (t - OpenDur) / (TrackEnd - OpenDur);
                ArgusFx.DrawSightLine(sb, Projectile.Center, Projectile.rotation.ToRotationVector2(), 980f,
                    new Color(170, 100, 255), 0.2f + p * 0.3f, 2f);
            }
            else if (t < LockEnd) {
                //锁定: 红线增粗发亮; 玩家在走廊内 → 屏幕边缘"被看见"警示
                float p = (t - TrackEnd) / (LockEnd - TrackEnd);
                ArgusFx.DrawSightLine(sb, Projectile.Center, LockedAngle.ToRotationVector2(), 980f,
                    TelegraphColors.Lethal, 0.5f + p * 0.5f, 2.5f + p * 2f);
                ArgusFx.ReportIfLocalPlayerSighted(Projectile.Center, LockedAngle.ToRotationVector2(),
                    980f, 120f, 0.4f + p * 0.4f);
            }
            else if (t >= FireTick && t < LingerEnd) {
                //射击余辉: 白闪快速衰减
                float p = 1f - (t - FireTick) / (LingerEnd - FireTick);
                ArgusFx.DrawSightLine(sb, Projectile.Center, LockedAngle.ToRotationVector2(), 980f,
                    Color.White, 0.75f * p, 4f * p + 0.5f);
            }
            //熄灭一拍 (LockEnd..FireTick) 刻意无线 — 爆发前的黑暗
        }

        /// <summary>同帧全部哨兵眼单批绘制 (ArgusEyeIris + Additive)。</summary>
        private static void DrawAllSentryEyes() {
            Effect fx = EyeIrisEffect;
            Texture2D glow = ACMAsset.SoftGlow;
            if (fx == null || glow == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSlit"]?.SetValue(0f);
            fx.Parameters["uPupilShift"]?.SetValue(0.07f);

            int myType = ModContent.ProjectileType<AetherealWingblades>();
            Vector2 origin = glow.Size() / 2f;

            //uOpen/uNova 需逐眼设置 → Immediate 模式下逐 Draw 前重新 Apply
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != myType || p.ai[0] != 1f)
                    continue;

                float t = p.localAI[0] - p.ai[1];
                if (t < 0f)
                    continue;

                //睁眼度: 睁开 → 常开 → 射击后眯起淡出
                float open = MathHelper.Clamp(t / OpenDur, 0f, 1f);
                if (t > LingerEnd)
                    open *= MathHelper.Clamp(1f - (t - LingerEnd) / (FadeEnd - LingerEnd), 0f, 1f);

                //锁定充能: 锁定期渐红渐亮, 熄灭拍骤降
                float nova = 0f;
                if (t >= TrackEnd && t < LockEnd)
                    nova = (t - TrackEnd) / (LockEnd - TrackEnd);
                else if (t >= FireTick && t < LingerEnd)
                    nova = 1f - (t - FireTick) / (LingerEnd - FireTick);

                fx.Parameters["uOpen"]?.SetValue(open);
                fx.Parameters["uNova"]?.SetValue(nova);
                fx.CurrentTechnique.Passes[0].Apply(); //Immediate 批下逐眼参数须手动重新 Apply

                Color tint = Color.Lerp(new Color(185, 105, 255), TelegraphColors.Lethal, nova * 0.8f);
                tint.A = 255;
                float scale = 150f / glow.Width;
                sb.Draw(glow, p.Center - Main.screenPosition, null, tint, p.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }
}
