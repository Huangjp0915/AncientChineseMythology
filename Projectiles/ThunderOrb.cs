using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles{
    public class ThunderOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        private const int MaxLife = 60; 
        
        public override void SetDefaults()
        {
            Projectile.width  = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile  = true;
            Projectile.aiStyle  = 0;
            Projectile.penetrate = 1;
            Projectile.timeLeft  = 600;
            Main.projFrames[Projectile.type] = 4;
        }

        public override void AI()
        {
                /* ① 首帧初始化血量 */
            if (Projectile.localAI[0] <= 0f)
                Projectile.localAI[0] = MaxLife;

            /* ② 读取自身矩形 */
            Rectangle orbBox = Projectile.Hitbox;

            /* ③ 扫描玩家弹幕 */
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !p.friendly || p.damage <= 0)
                    continue;                          // 只要真正的友方弹幕
                if (p.Hitbox.Intersects(orbBox)) {
                    ApplyDamage(p.damage);

                    // 若对方不是无限穿透则消耗一次
                    if (p.penetrate > 0) {
                        p.penetrate--;
                        if (p.penetrate == 0)
                            p.Kill();
                    }
                }
            }

            /* ④ （可选）近战亦可击碎 —— 简化版 */
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player plr = Main.player[i];
                if (!plr.active || plr.dead || plr.itemAnimation == 0) continue;

                Rectangle meleeBox = new(                 // 近战挥动矩形
                    (int)plr.itemLocation.X,
                    (int)plr.itemLocation.Y,
                    plr.HeldItem.width,
                    plr.HeldItem.height);
                if (meleeBox.Intersects(orbBox))
                    ApplyDamage(plr.GetWeaponDamage(plr.HeldItem));
            }
            
            Projectile.velocity *= 1.02f;
            // 简单寻的：向玩家轻微修正
            int targetIdx = Player.FindClosest(Projectile.Center, 1, 1);
            Player target = Main.player[targetIdx];
            
            Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 6f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.05f);
            Lighting.AddLight(Projectile.Center, 0.4f, 0.6f, 1f);
            if (++Projectile.frameCounter >= 5)          // 每 5 tick 换帧
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 4;   // 0→1→2→3→0…
            }
        }

        private void ApplyDamage(int dmg) {
            Projectile.localAI[0] -= dmg;
            CombatText.NewText(Projectile.Hitbox, CombatText.DamagedFriendly, dmg, dramatic: true);
            Projectile.netUpdate = true;                // ⬅️ 联机同步 :contentReference[oaicite:5]{index=5}

            if (Projectile.localAI[0] <= 0)
                Projectile.Kill();
        }

        public override void OnKill(int timeLeft)
        {
            // 小范围爆炸 & 闪电尘
            SoundEngine.PlaySound(SoundID.Item94, Projectile.Center);
            for (int i = 0; i < 20; i++)
                Dust.NewDustDirect(Projectile.position, 18, 18, DustID.Electric, Scale: 1.4f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // 命中玩家后立刻销毁
            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // 撞墙时播放粒子／音效后销毁
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
            Projectile.Kill();
            return false;          // 返回 false 告诉 tML 我们自己处理了
        }
    }
}