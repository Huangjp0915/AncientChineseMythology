using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    /// <summary>
    /// 地狱刀气 —— 直线预告体: 40f 蓝→红充能线, 充满后在两端各生成一柄 <see cref="SaberKiller"/> 真刃回扫。
    /// ai[0]/ai[1] = 轨道中心 (仅前置阶段用); ai[2] &lt; 0 = 轨道前置计时 (出生同步, 多人一致):
    /// &lt; -10 绕心公转 (旋刃牢笼), -10 起指向中心, 归零后进入正常充能。
    /// </summary>
    internal class SaberHell : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            //首帧本地初始化 (各端都会执行, 不依赖 OnSpawn 同步): 轨道前置按延迟顺延寿命
            if (Projectile.localAI[2] == 0) {
                Projectile.localAI[2] = 1;
                if (Projectile.ai[2] < 0)
                    Projectile.timeLeft = 130 - (int)Projectile.ai[2];
            }

            Projectile.velocity = Projectile.velocity.UnitVector();

            //前置阶段: 绕 (ai0, ai1) 公转 → 指向中心收束 (计时走同步的 ai[2])
            if (Projectile.ai[2] < 0) {
                Projectile.ai[2]++;
                Vector2 center = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                if (Projectile.ai[2] < -10) {
                    Vector2 toCenter = Projectile.Center - center;
                    toCenter = toCenter.RotatedBy(0.045);
                    Projectile.Center = center + toCenter;
                    //切向朝向: 旋刃牢笼读感
                    Projectile.velocity = toCenter.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                }
                else {
                    Projectile.velocity = (center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                }
                return;
            }

            if (Projectile.localAI[0] < 40) {
                if (Projectile.localAI[0] == 0) Projectile.localAI[1] = 30;
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] == 40 && Projectile.owner == Main.myPlayer) {
                    //充能完毕: 两端真刃相向回扫 (仅所有权端生成, 服务器自动同步)
                    int num = 1000;
                    int num2 = 36;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(),
                        Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2,
                        ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack,
                        Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Projectile.velocity *= -1;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(),
                        Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2,
                        ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack,
                        Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                }
            }
            else {
                if (Projectile.localAI[1] > 0) Projectile.localAI[1]--;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            //刀光 = BeamGrad 流动梯度直带; 充能越满越红 (红只留给即将出鞘的真刃)。
            float chargeT;   // 0~1 充能进度(→红)
            float thickness; // 屏幕像素全宽
            float intensity; // 0~1 整体亮度/淡入淡出
            if (Projectile.ai[2] < 0) {
                //前置公转/收束阶段: 细预告线, 蓝色 (尚未致命); 越近发动越亮
                float toStrike = MathHelper.Clamp(-Projectile.ai[2] / 60f, 0f, 1f);
                chargeT = (1f - toStrike) * 0.35f;
                thickness = MathHelper.Lerp(22f, 9f, toStrike);
                intensity = MathHelper.Lerp(0.6f, 0.28f, toStrike);
            }
            else {
                chargeT = MathHelper.Clamp(Projectile.localAI[0] / 40f, 0f, 1f);
                thickness = MathHelper.Lerp(8f, 64f, chargeT);
                intensity = MathHelper.Clamp(Projectile.localAI[1] / 30f, 0f, 1f);
            }
            if (intensity <= 0.01f)
                return false;

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            const float halfLen = 2600f;
            Vector2 start = Projectile.Center - dir * halfLen;
            Vector2 end = Projectile.Center + dir * halfLen;

            Color core = Color.Lerp(new Color(190, 224, 255), TelegraphColors.Lethal, chargeT);
            Color edge = Color.Lerp(new Color(40, 90, 160), new Color(150, 20, 30), chargeT);
            edge.A = 0;

            ACMShaders.DrawBeam(start, end, thickness * 0.5f, core, edge, intensity,
                flowSpeed: 2.2f, flowScale: 2.6f, coreSharp: 2.4f);
            return false;
        }
    }
}
