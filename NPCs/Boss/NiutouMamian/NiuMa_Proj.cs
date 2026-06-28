using AncientChineseMythology.Underworlds;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    public class SpoawnProj : ModProjectile
    {
        public static void CreatNPC(Vector2 Position) {
            var p = Projectile.NewProjectileDirect(null, Position, new Vector2(0, -10), ModContent.ProjectileType<SpoawnProj>(), 0, 0);
        }
        public override string Texture => GetType().Namespace.Replace(".", "/") + "/Dust_1";
        public override void SetDefaults() {
            Projectile.aiStyle = -1;

            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 320;
            Projectile.hide = true;
            base.SetDefaults();
        }
        public override bool? CanDamage() {
            return false;
        }
        private Vector2 Niu, Ma;
        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);
        }
        private Vector2 CircleMove(Vector2 center, Vector2 myPos, float R, float nextRo = .1f) {
            return myPos.RotatedBy(nextRo, center).SafeNormalize(Vector2.UnitX) * R;
        }
        public override void AI() {
            var p = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
            p.SetScreenPos(Projectile.Center);

            var ty = ModContent.DustType<Dust_1>();
            Projectile.velocity *= .98f;
            if (++Projectile.ai[0] < 180) {
                Niu = Vector2.Lerp(Niu, CircleMove(Vector2.Zero, Niu, 50, .9f), 0.08f);
                Ma = Vector2.Lerp(Ma, -Niu, 0.08f);
                p.SetZoom(3);
                {
                    var d = Dust.NewDustPerfect(Projectile.Center, ty);
                    d.color = Color.DarkGoldenrod;
                    d.velocity = new Vector2(0, NiuMaHelper.Rand_Float(2, 7)).RotatedByRandom(.5);
                }
            }
            else {
                if (Projectile.ai[0] == 181) {
                    p.SetScreenShake(7, 12);
                    for (int i = 0; i < 20; i++) {
                        var d = Dust.NewDustPerfect(Projectile.Center, ty);
                        d.color = Color.DarkGoldenrod;
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 8)).RotatedByRandom(8);
                    }
                }
                Niu = Vector2.Lerp(Niu, new Vector2(500, 0) + new Vector2(NiuMaHelper.Rand_Float(0, 40)).RotatedByRandom(1), 0.012f);
                Ma = Vector2.Lerp(Ma, new Vector2(-500, 0) + new Vector2(NiuMaHelper.Rand_Float(0, 40)).RotatedByRandom(1), 0.012f);


            }
            {
                var d = Dust.NewDustPerfect(Niu + Projectile.Center, ty);
                d.color = Color.DarkRed * .2f;
                d.scale *= 2.6f;
                d.color.A = 255;
                d.velocity *= .8f;

            }
            {
                var d = Dust.NewDustPerfect(Ma + Projectile.Center, ty);
                d.color = Color.Purple * .2f;
                d.color.A = 255;
                d.scale *= 2.6f;

                d.velocity *= .8f;
            }

            base.AI();
        }
        public override void OnKill(int timeLeft) {
            var n = NPC.NewNPCDirect(Projectile.GetSource_FromThis(), Projectile.Center + Niu, ModContent.NPCType<NiuTou>());
            var m = NPC.NewNPCDirect(Projectile.GetSource_FromThis(), Projectile.Center + Ma, ModContent.NPCType<MaMian>());
            (n.ModNPC as NiuTou).NPC_MaMian_Count = m.whoAmI;
            (m.ModNPC as MaMian).NPC_NiuTou_Count = n.whoAmI;

            var ty = ModContent.DustType<Dust_1>();

            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Niu + Projectile.Center, ty);
                d.color = Color.DarkRed * .2f;
                d.color.A = 255;
                d.scale *= 2.6f;

                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
            }
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Ma + Projectile.Center, ty);
                d.color = Color.Purple * .2f;
                d.color.A = 255;
                d.scale *= 2.6f;

                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
            }
            base.OnKill(timeLeft);
        }
    }
    public class Proj_756_Adjust : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void AI() {
            int num7 = 20;
            int num8 = 30;
            int num9 = 35;
            int maxValue = 6;
            bool flag = Projectile.ai[0] < num7;
            bool flag2 = Projectile.ai[0] >= num8;
            bool flag3 = Projectile.ai[0] >= num9;
            Projectile.ai[0] += 1f;
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.frame = Main.rand.Next(maxValue);
            }

            if (flag) {
                Projectile.Opacity += 0.1f;
                Projectile.scale = Projectile.Opacity * Projectile.ai[1];
            }

            if (flag2) {
                Projectile.Opacity -= 0.2f;
            }

            if (flag3)
                Projectile.Kill();
            base.AI();
        }
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.Opacity = 0;
            base.SetDefaults();
        }
        public override bool PreDraw(ref Color lightColor) {
            // 牛头冲锋血雾轨迹 (致命, 红可用): 自定义拉伸光束替换原版占位弹 756。
            if (Main.dedServ)
                return false;
            var t = ACMAsset.LightShot; // 64x64, 朝向右侧 (-->)
            if (t == null)
                return false;
            var sb = Main.spriteBatch;
            var origin = new Vector2(0, t.Height * 0.5f);
            float len = 200f * Projectile.scale / t.Width; // 沿速度方向拉伸到 ~200px*scale
            var glow = new Color(180, 30, 30); glow.A = 0;          // 暗血红外晕
            var core = new Color(255, 120, 110); core.A = 0;        // 亮红芯
            sb.Draw(t, Projectile.Center - Main.screenPosition, null, glow * Projectile.Opacity, Projectile.rotation, origin, new Vector2(len, Projectile.scale) * Projectile.Opacity, SpriteEffects.None, 0f);
            sb.Draw(t, Projectile.Center - Main.screenPosition, null, core * Projectile.Opacity * 0.8f, Projectile.rotation, origin, new Vector2(len, Projectile.scale * 0.45f) * Projectile.Opacity, SpriteEffects.None, 0f);
            return false;
        }
        public override bool ShouldUpdatePosition() {
            return false;
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.velocity.SafeNormalize(-Vector2.UnitY) * 200f * Projectile.scale, 22f * Projectile.scale, ref p);
        }
    }
    public class ChainProj_Buff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        public override void Update(Player player, ref int buffIndex) {
            player.Center -= player.velocity;
            player.velocity *= 0;
            player.velocity.Y -= .3f;
            base.Update(player, ref buffIndex);
        }
    }
    public class ChainProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        private NPC Owner => Main.npc[(int)Projectile.ai[2]];
        public override void AI() {
            //Main.NewText(CanDamage());
            if (player != null) {
                var p = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
                p.SetZoom(4.6f);
                player.Center -= player.velocity;
                player.velocity *= 0f;
                player.velocity.Y -= .3f;
                if (++Projectile.ai[0] > 40)
                    player.Center = Vector2.Lerp(player.Center, Owner.Center, .13f);
                Projectile.Center = player.Center;
                if (Owner.life < Owner.lifeMax * .5f)
                    player.AddBuff(ModContent.BuffType<ChainProj_Buff_1>(), 1 * 60);
                if (player.dead || Vector2.Distance(Owner.Center, player.Center) < 130) {
                    Projectile.Kill();
                    return;
                }
                Projectile.timeLeft = 10;

            }
            else {
                if (Owner != null && Owner.active) {
                    Projectile.timeLeft = 10;

                    if (Vector2.Distance(Owner.Center, Projectile.Center) > 1800 && Projectile.ai[1] == 0) {
                        Projectile.ai[1] = 1;
                        Projectile.velocity *= -1;
                    }
                    else if (Projectile.ai[1] != 0 && Vector2.Distance(Owner.Center, Projectile.Center) < 200) {
                        Projectile.Kill();
                    }
                }
            }
            base.AI();
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2100;
            base.SetStaticDefaults();
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            /*for(float j = 0; j < 1; j++)
             {
                 Dust.NewDustPerfect(Vector2.Lerp(Owner.Center, Projectile.Center, ))
             }*/
            var b = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Owner.Center, Projectile.Center, 10, ref p);

            //Main.NewText(targetHitbox + "  " + Main.LocalPlayer.Hitbox + " " + Owner.Center + " " + Projectile.Center + " " + b);


            return b/* && Vector2.Distance(Projectile.Center, targetHitbox.Center()) < 80*/;
        }
        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 1;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            base.SetDefaults();
        }
        public override bool? CanDamage() {
            return player == null && Projectile.ai[1] == 0;
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            player = target;
            var p = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
            p.SetScreenShake(8, 10);
            ACMScreenShakeSystem.Add(6f);
            UnderworldField.AddSoulErosion(target, 1); // 魂蚀: 锁链拖拽侵蚀
            Owner.ai[3] = 0;
            Owner.ai[0] = 111;
            base.OnHitPlayer(target, info);
        }
        private Player player = null;
        public override bool PreDraw(ref Color lightColor) {
            if (Owner != null && Owner.active) {
                // 锁链发光梯度 (硬化 API): 飞出时致命红, 拖拽绑定时幽紫 (非致命)。
                bool lethal = player == null && Projectile.ai[1] == 0;
                Color beamCore = lethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                Color beamEdge = lethal ? new Color(110, 20, 28) : new Color(60, 30, 110);
                ACMShaders.DrawBeam(Owner.Center, Projectile.Center, lethal ? 9f : 6f, beamCore, beamEdge, lethal ? 1f : 0.7f);

                var t = TextureAssets.Chains[0].Value;
                var sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                var rec = new Rectangle(0, 0, t.Width, (int)(Vector2.Distance(Owner.Center, Projectile.Center) * .9));
                var ro = -MathHelper.PiOver2 + (Owner.Center - Projectile.Center).ToRotation();
                sb.Draw(t, Owner.Center - Main.screenPosition, rec, Color.DarkGray, ro, rec.Size() * new Vector2(.5f, 1), 1, SpriteEffects.None, 0);
                var t2 = TextureAssets.Projectile[234].Value;
                sb.Draw(t2, Owner.Center + new Vector2(0, -Vector2.Distance(Owner.Center, Projectile.Center) * .9f).RotatedBy(ro) - Main.screenPosition, null, Color.DarkGray, ro, t2.Size() * .5f, 1, SpriteEffects.None, 0);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            return false;
        }
    }
    public class EyeProj_Buff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        private class EyeProj_Buff_1_Player : ModPlayer
        {
            public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
                if (Player.HasBuff<EyeProj_Buff_1>()) {
                    modifiers.FinalDamage *= .5f;
                }
                base.ModifyHitNPC(target, ref modifiers);
            }
        }
        public override void Update(Player player, ref int buffIndex) {
            player.statDefense *= .6f;
            player.statLifeMax2 /= 2;
            base.Update(player, ref buffIndex);
        }
    }
    public class EyeProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 360;
            Projectile.penetrate = 1;
            base.SetDefaults();
        }
        private Player player => Main.player[Projectile.owner];
        public override void AI() {
            var dis = (Projectile.Center - player.Center).Length();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (player.Center - Projectile.Center).NormalizeVector() * (6 + player.velocity.Length() * .5f) * Math.Clamp(dis * .08f, 0, 1), .05f);

            base.AI();
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.Kill();

            player.AddBuff(ModContent.BuffType<EyeProj_Buff_1>(), 30 * 60);
            base.OnHitPlayer(target, info);
        }
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            // 牛头凝视眼束: 幽紫鬼眼发光 (追踪/凝视, 非红致命; 自定义替换占位 MagicPixel)。
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var shot = ACMAsset.LightShot;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
            var halo = TelegraphColors.NetherViolet; halo.A = 0;
            var pupil = new Color(220, 120, 255); pupil.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, halo * 0.85f, 0, glow.Size() * .5f, 0.85f * pulse, default, 0);
            sb.Draw(shot, Projectile.Center - Main.screenPosition, null, pupil * 0.9f, Projectile.velocity.ToRotation(), shot.Size() * .5f, 0.5f * pulse, default, 0);
            return false;
        }

    }
    public class DarkGreenProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 180;
            Projectile.penetrate = 1;
            base.SetDefaults();
        }
        private Player player => Main.player[Projectile.owner];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }
        private NPC Owner => Main.npc[(int)Projectile.ai[2]];

        public override void AI() {
            if (Projectile.velocity.Length() < 30) {
                Projectile.velocity *= 1.05f;
            }
            var ro = (player.Center - Projectile.Center).ToRotation();
            var CurRo = Projectile.velocity.ToRotation();
            var ToRo = Math.Clamp(MathHelper.WrapAngle(CurRo.AngleLerp(ro, 0.02f) - CurRo), -.05f, .05f);

            Projectile.velocity = Projectile.velocity.RotatedBy(ToRo);

            var ty = ModContent.DustType<Dust_2>();

            for (int i = 0; i < 4; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, ty);
                d.color = Color.YellowGreen;
                d.alpha /= 6;
                d.scale *= 3;
                d.velocity = Projectile.velocity.RotatedByRandom(.3) * .6f;
            }

            base.AI();
        }
        private static int[] Ai0_DeBuff_FromTarget = [30, 20, 24, 70, 22, 80, 35, 23, 31, 32, 197, 33, 36, 195, 196, 37, 38, 39, 69, 44, 46, 47, 149, 156, 164, 163, 144, 148, 145];

        public override void OnKill(int timeLeft) {
            var ty = ModContent.DustType<Dust_1>();

            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, ty);
                d.color = Color.YellowGreen;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
            }

            base.OnKill(timeLeft);
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.Kill();

            UnderworldField.AddSoulErosion(target, 1); // 魂蚀: 马面灵魂弹

            if (Owner.life > Owner.lifeMax * .5f) {
                target.AddBuff(39, 3 * 60);
            }
            else {
                for (int i = 0; i < Ai0_DeBuff_FromTarget.Length; i++) {
                    target.AddBuff(Ai0_DeBuff_FromTarget[i], 60);
                }

            }
            base.OnHitPlayer(target, info);
        }
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            // 马面灵魂弹: 鬼绿芯 + 幽紫晕 (自定义替换占位 SharpTears)。
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var o = glow.Size() * .5f;
            float ro = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            var edge = TelegraphColors.NetherViolet; edge.A = 0;
            var core = TelegraphColors.GhostGreen; core.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, edge * 0.75f, ro, o, 0.85f, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.95f, ro, o, 0.5f, default, 0);
            return false;
        }

    }
    public class DeclineSpeedBuff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        public override void Update(Player player, ref int buffIndex) {
            player.Center -= player.velocity * .3f;
            base.Update(player, ref buffIndex);
        }
    }
    public class DeclineSpeedBuff_2 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        public override void Update(Player player, ref int buffIndex) {
            player.Center -= player.velocity * .6f;
            base.Update(player, ref buffIndex);
        }
    }
    public class DarkGreenBoomProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 600;
            Projectile.penetrate = 1;
            base.SetDefaults();
        }
        private Player player => Main.player[Projectile.owner];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }
        private float value => Projectile.ai[2] - Math.Max(40 - Projectile.ai[1], 2);
        public override void AI() {
            if (Projectile.timeLeft > 180) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (player.Center - Projectile.Center).NormalizeVector() * (4 + player.velocity.Length() * .8f), 0.02f);
            }
            else {
                Projectile.velocity *= .98f;
            }
            var ty = ModContent.DustType<Dust_2>();
            for (int i = 0; i < 3; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, ty);
                d.color = Color.YellowGreen;
                d.alpha /= 4;
                d.scale *= 3;
                d.velocity = Projectile.velocity.RotatedByRandom(1) * .5f;
            }
            if (Projectile.timeLeft <= 180 && Projectile.timeLeft > 0) {
                Projectile.ai[2]++;
                if (value > 0)
                    for (int i = 0; i < 20; i++) {
                        var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(0, 250).RotatedByRandom(8), DustID.GreenTorch).noGravity = true;

                    }
                if (value == 4) {
                    Projectile.ai[2] = 0;
                    Projectile.ai[1] += 7;
                }
            }
            base.AI();
        }
        public override void OnKill(int timeLeft) {
            Main.LocalPlayer.GetModPlayer<NiuMaPlayer>().SetScreenShake(7, 8);
            foreach (var p in Main.player) {
                if (p != null)
                    if (p.active && !p.dead)
                        if (p.Distance(Projectile.Center) < 250)
                            p.Hurt(new PlayerDeathReason(), Projectile.damage, 0);

            }
            for (int i = 0; i < 30; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.FireworkFountain_Green);
                d.color = Color.YellowGreen;
                d.noGravity = true;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 7)).RotatedByRandom(8) * 3;
            }

            base.OnKill(timeLeft);
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.Kill();
            base.OnHitPlayer(target, info);
        }
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            // 马面爆裂灵魂核 (致命大招): 鬼绿核 + 幽紫晕 (自定义替换占位弹 540)。
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var o = glow.Size() * .5f;
            float pulse = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f);
            var edge = new Color(90, 45, 150); edge.A = 0;
            var core = TelegraphColors.GhostGreen; core.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, edge * 0.8f, 0, o, 1.7f * pulse, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.95f, 0, o, 0.95f * pulse, default, 0);
            if (Projectile.timeLeft <= 180 && Projectile.timeLeft > 0 && value > 1) {
                var warn = TelegraphColors.GhostGreen; warn.A = 0;
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null, warn * 0.45f, 0, o, 2.6f, default, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 协同阶段「勾魂锁命连携」马面侧弹: 缓慢漂移的灵魂球。
    /// 与牛头的可读冲锋车道交替释放 —— 牛头压线时马面静默, 牛头收招/铺垫时马面填充慢球,
    /// 玩家须读牛头的车道 + 穿马面的慢球缝隙 (学配合, 非拼 DPS)。慢 = 可读预警 (§6.1 持续/站位)。
    /// </summary>
    public class SoulOrbProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void AI() {
            // 极缓加速 + 微弱转向, 始终可读 (不追身)。
            if (Projectile.velocity.Length() < 5.5f)
                Projectile.velocity *= 1.012f;
            Projectile.rotation += 0.04f;

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<Dust_2>());
                d.color = TelegraphColors.GhostGreen;
                d.alpha /= 5;
                d.scale *= 2.4f;
                d.velocity = Projectile.velocity * 0.2f;
            }
            Lighting.AddLight(Projectile.Center, 0.15f, 0.32f, 0.2f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2); // 魂蚀
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<Dust_1>());
                d.color = TelegraphColors.GhostGreen;
                d.color.A = 255;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(1.5f, 4f)).RotatedByRandom(8);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var o = glow.Size() * .5f;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI);
            var edge = TelegraphColors.NetherViolet; edge.A = 0;
            var core = TelegraphColors.GhostGreen; core.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, edge * 0.7f, 0, o, 1.3f * pulse, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.95f, 0, o, 0.7f * pulse, default, 0);
            return false;
        }
    }

    /// <summary>
    /// 同伴复生反制圈 —— 复活演出期间在「阵亡者尸位」生成的鬼绿光阵。
    /// 玩家站入圈内 → 正在引魂的同伴 (channeler) 暂时<b>可被伤害</b> (取消 dontTakeDamage);
    /// 离开 → 同伴重新无敌。给"必然翻盘的复生"一个清晰的处罚窗口 (站对位置才能打断)。
    /// 颜色=翠玉/鬼绿 (正反馈安全区, 非红); 逻辑服务器权威, 绘制本地。
    /// ai[0] = 引魂同伴 whoAmI。
    /// </summary>
    public class NiuMaRevivalCircle : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public const float WorldRadius = 190f;

        private NPC Channeler {
            get {
                int who = (int)Projectile.ai[0];
                return (who >= 0 && who < Main.maxNPCs) ? Main.npc[who] : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 250;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
        }

        public override void AI() {
            NPC ch = Channeler;
            if (ch == null || !ch.active) {
                Projectile.Kill();
                return;
            }

            // 判定: 是否有玩家站在尸位光圈内 (逻辑服务器权威)。
            bool playerInside = false;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead && p.Distance(Projectile.Center) < WorldRadius) {
                    playerInside = true;
                    break;
                }
            }
            if (Main.netMode != NetmodeID.MultiplayerClient)
                ch.dontTakeDamage = !playerInside; // 站圈内则同伴可被打断

            if (!Main.dedServ) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * WorldRadius * Main.rand.NextFloat(0.7f, 1f);
                var d = Dust.NewDustPerfect(pos, playerInside ? DustID.GreenFairy : DustID.GreenTorch);
                d.noGravity = true;
                d.velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 1.6f;
                d.scale = playerInside ? 1.5f : 1.1f;
                Lighting.AddLight(Projectile.Center, 0.1f, 0.4f, 0.22f);
            }
        }

        public override void OnKill(int timeLeft) {
            NPC ch = Channeler;
            if (ch != null && ch.active && Main.netMode != NetmodeID.MultiplayerClient)
                ch.dontTakeDamage = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return false;

            bool playerInside = false;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead && p.Distance(Projectile.Center) < WorldRadius) {
                    playerInside = true;
                    break;
                }
            }
            float life = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float intensity = (playerInside ? 0.95f : 0.6f) * life;

            ACMShaders.WorldDecalParams(Projectile.Center, WorldRadius, out Vector2 uv, out float radiusFrac, out float aspect);
            Color primary = TelegraphColors.Safe;
            Color secondary = TelegraphColors.GhostGreen;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
            return false;
        }
    }

    /// <summary>勾魂标记 (马面半血新机制): 纯可视/计时载体, 实际逻辑在 <see cref="NiuMaPlayer"/>。</summary>
    public class SoulHookBuff : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }
}