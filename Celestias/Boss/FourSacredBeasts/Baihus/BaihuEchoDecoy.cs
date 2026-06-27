using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎·金属回响(虚影) Metallic Echo decoy —— 「金属回响」环里 2/3 的<b>无害虚影</b>碎片。
    /// 与真实碎片同步收束以制造声势，但<b>不造成伤害</b>且半途黯淡溃散，让玩家学会「每第三片才是真的」——
    /// 以可读性取代弹幕密度（§goal2 Metallic Echo）。服务端零绘制。
    /// </summary>
    public class BaihuEchoDecoy : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
        }

        public override bool CanHitPlayer(Player target) => false;

        public override void AI() {
            Projectile.rotation += 0.2f;
            // 末段减速黯淡，明确「这是假的」
            if (Projectile.timeLeft < 24)
                Projectile.velocity *= 0.9f;
            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, -Projectile.velocity * 0.1f, 180, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Texture2D shardTex = ACMAsset.EmberShards;
            if (shardTex == null)
                return false;
            int gridSize = shardTex.Width / 3;
            Rectangle frame = new(0, 0, gridSize, gridSize);
            Vector2 origin = new(gridSize / 2f, gridSize / 2f);

            float fade = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            // 暗灰半透 → 与真实碎片(亮银)区分
            Color c = new Color(110, 118, 132) * (0.45f * fade);
            sb.Draw(shardTex, drawPos, frame, c, Projectile.rotation, origin, 0.11f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
