using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    public static class NiuMaHelper
    {
        public static string Path = typeof(NiuMaHelper).Namespace.Replace(".", "/") + "/";
        public static string NothingTex_Path = Path + "NothingTex";

        public static float Rand_Float(double a, double b = 0) {
            var max = (float)Math.Max(a, b);
            var min = (float)Math.Min(a, b);
            return Main.rand.NextFloat(min, max);
        }
        public static int Rand_Int(double a, double b = 0, int? withOut = null) {
            var max = (int)Math.Max(a, b);
            var min = (int)Math.Min(a, b);

            var f = Main.rand.Next(min, max + 1);
            if (withOut.HasValue)
                if (f == withOut.Value)
                    return Rand_Int(min, max, withOut);
            return f;
        }
        public static Vector2 NormalizeVector(this Vector2 v, Vector2 safe = default) {
            return v.SafeNormalize(safe);
        }
    }
    public class Dust_1 : ModDust
    {
        public override string Texture => GetType().Namespace.Replace(".", "/") + "/Dust_5";
        public override void OnSpawn(Dust dust) {
            dust.noLight = true;//�����޹�
            dust.noGravity = true;//����������
            dust.alpha = 240;
            dust.scale = NiuMaHelper.Rand_Float(0.9f, 1.3f);
            dust.velocity = new Vector2(NiuMaHelper.Rand_Float(1, 3)).RotateRandom(7);
            dust.color = Color.SkyBlue;
            base.OnSpawn(dust);
        }

        public override bool Update(Dust dust)//ban��ԭ������Ӹ��£����Լ���
        {
            /*k++;
            if (k > 50)
                co -= 0.04f;*/
            dust.position += dust.velocity;
            dust.scale -= 0.02f;
            dust.velocity *= 0.97f;
            dust.alpha -= 5;
            if (dust.scale <= 0 || dust.velocity.Length() < 0.04f || dust.alpha < 0)
                dust.active = false;

            return false;
        }
        //public static Color col = Color.Red;
        public static Texture2D tx;
        public static Texture2D tx_Black;

        public override void Load() {
            tx = ModContent.Request<Texture2D>(GetType().Namespace.Replace(".", "/") + "/Dust_5", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
            tx_Black = ModContent.Request<Texture2D>(GetType().Namespace.Replace(".", "/") + "/Dust_1", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
            base.Load();
        }
        public override bool PreDraw(Dust dust) {
            var c = dust.color;
            if (dust.color.ToVector3().Length() == 0) {
                //Main.NewText(dust.color);

                Main.spriteBatch.Draw(tx_Black, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, Color.Black * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 1f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(tx_Black, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, dust.color * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.15f, SpriteEffects.None, 0);
            }
            else {
                c.A = 0;
                Main.spriteBatch.Draw(tx, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, c * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(tx, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, new Color(1, 1, 1f, 0f) * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 0.7f * dust.scale * 0.15f, SpriteEffects.None, 0);


            }

            return false;
        }
    }
    public class Dust_2 : ModDust
    {
        public override string Texture => GetType().Namespace.Replace(".", "/") + "/Dust_5";
        public override void OnSpawn(Dust dust) {
            dust.noLight = true;//�����޹�
            dust.noGravity = true;//����������
            dust.noLight = true;
            dust.alpha = 255;
            dust.color = Color.Red;
            /*k = 0;
            co = 1;*/
            base.OnSpawn(dust);
        }
        public override bool Update(Dust dust)//ban��ԭ������Ӹ��£����Լ���
        {
            /*k++;
            if (k > 50)
                co -= 0.04f;*/
            dust.alpha -= 2;
            dust.position += dust.velocity;
            dust.velocity *= 0.98f;
            Lighting.AddLight(dust.position, dust.color.ToVector3() * 0.1f);
            if (dust.alpha <= 0)
                dust.active = false;


            return false;
        }
        public override bool PreDraw(Dust dust) {
            var c = dust.color;
            if (dust.color.ToVector3().Length() == 0) {
                //Main.NewText(dust.color);

                Main.spriteBatch.Draw(Dust_1.tx_Black, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, Color.Black * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 1f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(Dust_1.tx_Black, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, dust.color * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.15f, SpriteEffects.None, 0);
            }
            else {
                c.A = 0;
                Main.spriteBatch.Draw(Dust_1.tx, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, c * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(Dust_1.tx, (dust.position - Main.screenPosition)/* + tx.Size() / 2*/, null, new Color(1, 1, 1f, 0f) * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 0.7f * dust.scale * 0.15f, SpriteEffects.None, 0);


            }

            return false;
        }
    }

}