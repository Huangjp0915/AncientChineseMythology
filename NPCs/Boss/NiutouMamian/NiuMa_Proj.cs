using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Newtonsoft.Json.Linq;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    public class SpoawnProj : ModProjectile
    {
        public static void CreatNPC(Vector2 Position)
        {
            var p = Projectile.NewProjectileDirect(null, Position, new Vector2(0, -10), ModContent.ProjectileType<SpoawnProj>(), 0, 0);
        }
        public override string Texture => GetType().Namespace.Replace(".", "/") + "/Dust_1";
        public override void SetDefaults()
        {
            Projectile.aiStyle = -1;

            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 320;
            Projectile.hide = true;
            base.SetDefaults();
        }
        public override bool? CanDamage()
        {
            return false;
        }
        Vector2 Niu, Ma;
        public override void OnSpawn(IEntitySource source)
        {
            base.OnSpawn(source);
        }
        private Vector2 CircleMove(Vector2 center, Vector2 myPos, float R, float nextRo = .1f)
        {
            return myPos.RotatedBy(nextRo, center).SafeNormalize(Vector2.UnitX) * R;
        }
        public override void AI()
        {
            var p = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
            p.SetScreenPos(Projectile.Center);

            var ty = ModContent.DustType<Dust_1>();
            Projectile.velocity *= .98f;
            if (++Projectile.ai[0] < 180)
            {
                Niu = Vector2.Lerp(Niu, CircleMove(Vector2.Zero, Niu, 50, .9f), 0.08f);
                Ma = Vector2.Lerp(Ma, -Niu, 0.08f);
                p.SetZoom(3);
                {
                    var d = Dust.NewDustPerfect(Projectile.Center, ty);
                    d.color = Color.DarkGoldenrod;
                    d.velocity = new Vector2(0, NiuMaHelper.Rand_Float(2, 7)).RotatedByRandom(.5);
                }
            }
            else
            {
                if (Projectile.ai[0] == 181)
                {
                    p.SetScreenShake(7, 12);
                    for (int i = 0; i < 20; i++)
                    {
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
        public override void OnKill(int timeLeft)
        {
            var n = NPC.NewNPCDirect(Projectile.GetSource_FromThis(), Projectile.Center + Niu, ModContent.NPCType<NiuTou>());
            var m = NPC.NewNPCDirect(Projectile.GetSource_FromThis(), Projectile.Center + Ma, ModContent.NPCType<MaMian>());
            (n.ModNPC as NiuTou).NPC_MaMian_Count = m.whoAmI;
            (m.ModNPC as MaMian).NPC_NiuTou_Count = n.whoAmI;

            var ty = ModContent.DustType<Dust_1>();

            for (int i = 0; i < 10; i++)
            {
                var d = Dust.NewDustPerfect(Niu + Projectile.Center, ty);
                d.color = Color.DarkRed * .2f;
                d.color.A = 255;
                d.scale *= 2.6f;

                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
            }
            for (int i = 0; i < 10; i++)
            {
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
        public override void AI()
        {
            int num = 5;
            float num2 = 1f;
            int num3 = 30;
            int num4 = 30;
            int num5 = 2;
            int num6 = 2;
            int num7 = 20;
            int num8 = 30;
            int num9 = 35;
            int maxValue = 6;
            bool flag = Projectile.ai[0] < (float)num7;
            bool flag2 = Projectile.ai[0] >= (float)num8;
            bool flag3 = Projectile.ai[0] >= (float)num9;
            Projectile.ai[0] += 1f;
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.frame = Main.rand.Next(maxValue);
            }

            if (flag)
            {
                Projectile.Opacity += 0.1f;
                Projectile.scale = Projectile.Opacity * Projectile.ai[1];
            }

            if (flag2)
            {
                Projectile.Opacity -= 0.2f;
            }

            if (flag3)
                Projectile.Kill();
            base.AI();
        }
        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.Opacity = 0;
            Main.instance.LoadProjectile(756);
            base.SetDefaults();
        }
        public override bool PreDraw(ref Color lightColor)
        {
            var t = TextureAssets.Projectile[756].Value;
            var f = new Rectangle(0, (t.Height / 6) * Projectile.frame, t.Width, t.Height / 6);
            Main.spriteBatch.Draw(t, Projectile.Center - Main.screenPosition, f, Color.Red, Projectile.rotation, f.Size() * new Vector2(0, 0.5f), new Vector2(Projectile.scale, Projectile.Opacity), SpriteEffects.None, 0f);
            return false;
        }
        public override bool ShouldUpdatePosition()
        {
            return false;
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float p = 0;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.velocity.SafeNormalize(-Vector2.UnitY) * 200f * Projectile.scale, 22f * Projectile.scale, ref p);
        }
    }
    public class ChainProj_Buff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        public override void Update(Player player, ref int buffIndex)
        {
            player.Center -= player.velocity;
            player.velocity *= 0;
            player.velocity.Y -= .3f;
            base.Update(player, ref buffIndex);
        }
    }
    public class ChainProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        NPC Owner => Main.npc[(int)Projectile.ai[2]];
        public override void AI()
        {
            //Main.NewText(CanDamage());
            if (player != null)
            {
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
                if (player.dead || Vector2.Distance(Owner.Center, player.Center) < 130)
                {
                    Projectile.Kill();
                    return;
                }
                Projectile.timeLeft = 10;

            }
            else
            {
                if (Owner != null && Owner.active)
                {
                    Projectile.timeLeft = 10;

                    if (Vector2.Distance(Owner.Center, Projectile.Center) > 1800 && Projectile.ai[1] == 0)
                    {
                        Projectile.ai[1] = 1;
                        Projectile.velocity *= -1;
                    }
                    else if (Projectile.ai[1] != 0 && Vector2.Distance(Owner.Center, Projectile.Center) < 200)
                    {
                        Projectile.Kill();
                    }
                }
            }
            base.AI();
        }
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2100;
            base.SetStaticDefaults();
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float p = 0f;
            /*for(float j = 0; j < 1; j++)
             {
                 Dust.NewDustPerfect(Vector2.Lerp(Owner.Center, Projectile.Center, ))
             }*/
            var b = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Owner.Center, Projectile.Center, 10, ref p);

            //Main.NewText(targetHitbox + "  " + Main.LocalPlayer.Hitbox + " " + Owner.Center + " " + Projectile.Center + " " + b);


            return b/* && Vector2.Distance(Projectile.Center, targetHitbox.Center()) < 80*/;
        }
        public override void OnKill(int timeLeft)
        {
            base.OnKill(timeLeft);
        }
        public override void SetDefaults()
        {
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
        public override bool? CanDamage()
        {
            return player == null && Projectile.ai[1] == 0;
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            player = target;
            var p = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
            p.SetScreenShake(8, 10);
            Owner.ai[3] = 0;
            Owner.ai[0] = 111;
            base.OnHitPlayer(target, info);
        }
        private Player player = null;
        public override bool PreDraw(ref Color lightColor)
        {
            if (Owner != null && Owner.active)
            {
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
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        class EyeProj_Buff_1_Player : ModPlayer
        {
            public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            {
                if (Player.HasBuff<EyeProj_Buff_1>())
                {
                    modifiers.FinalDamage *= .5f;
                }
                base.ModifyHitNPC(target, ref modifiers);
            }
        }
        public override void Update(Player player, ref int buffIndex)
        {
            player.statDefense *= .6f;
            player.statLifeMax2 /= 2;
            base.Update(player, ref buffIndex);
        }
    }
    public class EyeProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults()
        {
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
        Player player => Main.player[Projectile.owner];
        public override void AI()
        {
            var dis = (Projectile.Center - player.Center).Length();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (player.Center - Projectile.Center).NormalizeVector() * (6 + player.velocity.Length() * .5f) * Math.Clamp(dis * .08f, 0, 1), .05f);

            base.AI();
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            Projectile.Kill();

            player.AddBuff(ModContent.BuffType<EyeProj_Buff_1>(), 30 * 60);
            base.OnHitPlayer(target, info);
        }
        public override bool PreDraw(ref Color lightColor)
        {
            {
                var t = TextureAssets.MagicPixel.Value;
                var sb = Main.spriteBatch;
                var rec = new Rectangle(0, 0, 36, 36);
                sb.Draw(t, Projectile.Center - Main.screenPosition, rec, lightColor, 0, rec.Size() * .5f, 1, default, 0);
            }
            return false;
        }

    }
    public class DarkGreenProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults()
        {
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
        Player player => Main.player[Projectile.owner];
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }
        NPC Owner => Main.npc[(int)Projectile.ai[2]];

        public override void AI()
        {
            if (Projectile.velocity.Length() < 30)
            {
                Projectile.velocity *= 1.05f;
            }
            var ro = (player.Center - Projectile.Center).ToRotation();
            var CurRo = Projectile.velocity.ToRotation();
            var ToRo = Math.Clamp(MathHelper.WrapAngle(CurRo.AngleLerp(ro, 0.02f) - CurRo), -.05f, .05f);

            Projectile.velocity = Projectile.velocity.RotatedBy(ToRo);

            var ty = ModContent.DustType<Dust_2>();

            for (int i = 0; i < 4; i++)
            {
                var d = Dust.NewDustPerfect(Projectile.Center, ty);
                d.color = Color.YellowGreen;
                d.alpha /= 6;
                d.scale *= 3;
                d.velocity = Projectile.velocity.RotatedByRandom(.3) * .6f;
            }

            base.AI();
        }
        static int[] Ai0_DeBuff_FromTarget = [30, 20, 24, 70, 22, 80, 35, 23, 31, 32, 197, 33, 36, 195, 196, 37, 38, 39, 69, 44, 46, 47, 149, 156, 164, 163, 144, 148, 145];

        public override void OnKill(int timeLeft)
        {
            var ty = ModContent.DustType<Dust_1>();

            for (int i = 0; i < 10; i++)
            {
                var d = Dust.NewDustPerfect(Projectile.Center, ty);
                d.color = Color.YellowGreen;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
            }

            base.OnKill(timeLeft);
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            Projectile.Kill();

            if (Owner.life > Owner.lifeMax * .5f)
            {
                target.AddBuff(39, 3 * 60);
            }
            else
            {
                for (int i = 0; i < Ai0_DeBuff_FromTarget.Length; i++)
                {
                    target.AddBuff(Ai0_DeBuff_FromTarget[i], 60);
                }

            }
            base.OnHitPlayer(target, info);
        }
        public override bool PreDraw(ref Color lightColor)
        {
            {
                var t = TextureAssets.Extra[98].Value;
                var sb = Main.spriteBatch;
                sb.Draw(t, Projectile.Center - Main.screenPosition, null, Color.GreenYellow, Projectile.velocity.ToRotation() + MathHelper.PiOver2, t.Size() * .5f, .4f, default, 0);
            }
            return false;
        }

    }
    public class DeclineSpeedBuff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        public override void Update(Player player, ref int buffIndex)
        {
            player.Center -= player.velocity * .3f;
            base.Update(player, ref buffIndex);
        }
    }
    public class DeclineSpeedBuff_2 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            base.SetStaticDefaults();
        }
        public override void Update(Player player, ref int buffIndex)
        {
            player.Center -= player.velocity * .6f;
            base.Update(player, ref buffIndex);
        }
    }
    public class DarkGreenBoomProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults()
        {
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
        Player player => Main.player[Projectile.owner];
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }
        float value => Projectile.ai[2] - Math.Max(40 - Projectile.ai[1], 2);
        public override void AI()
        {
            if (Projectile.timeLeft > 180)
            {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (player.Center - Projectile.Center).NormalizeVector() * (4 + player.velocity.Length() * .8f), 0.02f);
 }
            else
            {
                Projectile.velocity *= .98f;
            }
                var ty = ModContent.DustType<Dust_2>();
            for (int i = 0; i < 3; i++)
            {
                var d = Dust.NewDustPerfect(Projectile.Center, ty);
                d.color = Color.YellowGreen;
                d.alpha /= 4;
                d.scale *= 3;
                d.velocity = Projectile.velocity.RotatedByRandom(1) * .5f;
            }
            if (Projectile.timeLeft <= 180 && Projectile.timeLeft > 0)
            {
                Projectile.ai[2]++;
                if (value > 0)
                for (int i = 0; i < 20; i++)
                {
                    var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(0, 250).RotatedByRandom(8), 61).noGravity = true;

                }
                if (value == 4)
                {
                    Projectile.ai[2] = 0;
                    Projectile.ai[1] += 7;
                }
            }
            base.AI();
        }
        public override void OnKill(int timeLeft)
        {
            Main.LocalPlayer.GetModPlayer<NiuMaPlayer>().SetScreenShake(7, 8);
            foreach (var p in Main.player)
            {
                if (p != null)
                    if (p.active && !p.dead)
                        if (p.Distance(Projectile.Center) < 250) 
                            p.Hurt(new PlayerDeathReason(), Projectile.damage, 0);

            }
            for (int i = 0; i < 30; i++)
            {
                var d = Dust.NewDustPerfect(Projectile.Center, 220);
                d.color = Color.YellowGreen;
                d.noGravity = true;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 7)).RotatedByRandom(8) * 3;
            }

            base.OnKill(timeLeft);
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            Projectile.Kill();
            base.OnHitPlayer(target, info);
        }
        public override bool PreDraw(ref Color lightColor)
        {
            
                var t = TextureAssets.Projectile[540].Value;
                var sb = Main.spriteBatch;
                sb.Draw(t, Projectile.Center - Main.screenPosition, null, Color.GreenYellow, Projectile.velocity.ToRotation() + MathHelper.PiOver2, t.Size() * .5f, .7f, default, 0);
            if(Projectile.timeLeft <= 180 && Projectile.timeLeft > 0 && value > 1)
            {
                var col = Color.GreenYellow;
                col.A = 0;
                sb.Draw(t, Projectile.Center - Main.screenPosition, null, col, Projectile.velocity.ToRotation() + MathHelper.PiOver2, t.Size() * .5f, 1.9f, default, 0);

            }
            return false;
        }
    }
}