using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    /// <summary>
    /// 牛头马面共用工具: 主题色板 / 专属着色器缓存 (SoulFlame 本体魂焰, NetherGate 鬼门法阵) /
    /// 本体着色绘制与法阵参数助手 / 搭档查找。着色器按简报规范在此静态缓存, 不注册进 ACMShaders。
    /// </summary>
    public static class NiuMaHelper
    {
        public static string Path = typeof(NiuMaHelper).Namespace.Replace(".", "/") + "/";
        public static string NothingTex_Path = Path + "NothingTex";

        // ===== 主题色板 (预警红只用 TelegraphColors.Lethal, 此处均为主题色) =====
        /// <summary>牛头主题: 熔炎赤红。</summary>
        public static readonly Color EmberRed = new(255, 92, 48);
        /// <summary>牛头焰心亮色。</summary>
        public static readonly Color EmberCore = new(255, 214, 130);
        /// <summary>马面主题: 幽冥紫。</summary>
        public static readonly Color GhostViolet = new(150, 96, 235);
        /// <summary>马面焰心: 鬼绿。</summary>
        public static readonly Color GhostCore = new(150, 240, 185);

        // ===== 专属着色器缓存 (惰性 ImmediateLoad, 服务端返回 null) =====
        private static Asset<Effect> soulFlameRef;
        private static Asset<Effect> netherGateRef;

        /// <summary>本体魂焰描边着色器 (s0=NPC 贴图, s1=噪声)。</summary>
        public static Effect SoulFlame => GetFx(ref soulFlameRef, "NiuMaSoulFlame");
        /// <summary>鬼门法阵着色器 (s0=噪声, 屏幕空间 SDF)。</summary>
        public static Effect NetherGate => GetFx(ref netherGateRef, "NiuMaNetherGate");

        private static Effect GetFx(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/" + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        /// <summary>
        /// 用 SoulFlame 着色器绘制 Boss 本体 (须在已有活动批的阶段调用, 会 End→Begin→End→恢复默认批)。
        /// 着色器缺失时退化为普通绘制, 画面不缺失。
        /// </summary>
        public static void DrawBodySoulFlame(SpriteBatch sb, Texture2D tex, Vector2 screenPos, Rectangle frame,
            Color light, float rotation, float scale, SpriteEffects spe,
            Color tint, Color tintCore, float flash, float charge, float alpha) {
            Vector2 origin = frame.Size() * 0.5f;
            Effect fx = SoulFlame;
            if (fx == null) {
                sb.Draw(tex, screenPos, frame, light * alpha, rotation, origin, scale, spe, 0f);
                return;
            }

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uTint"]?.SetValue(tint.ToVector4());
            fx.Parameters["uTint2"]?.SetValue(tintCore.ToVector4());
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(charge, 0f, 1f));
            fx.Parameters["uPixel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAlpha"]?.SetValue(MathHelper.Clamp(alpha, 0f, 1f));

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, screenPos, frame, light, rotation, origin, scale, spe, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>设好 NetherGate 全部 uniform (调用方随后自行走 DrawScreenSpaceDecal / Standalone)。</summary>
        public static void SetGateParams(Effect fx, Vector2 worldCenter, float worldRadius,
            Color colA, Color colB, float open, float spin, float intensity) {
            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uv, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.02f, 1.4f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorA"]?.SetValue(colA.ToVector4());
            fx.Parameters["uColorB"]?.SetValue(colB.ToVector4());
            fx.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(open, 0f, 1f));
            fx.Parameters["uSpin"]?.SetValue(spin);
        }

        /// <summary>在弹幕 PreDraw 等"已有活动批"阶段直接画一次鬼门法阵 (加性)。</summary>
        public static void DrawGateInBatch(Vector2 worldCenter, float worldRadius,
            Color colA, Color colB, float open, float spin, float intensity) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            Effect fx = NetherGate;
            if (fx == null)
                return;
            SetGateParams(fx, worldCenter, worldRadius, colA, colB, open, spin, intensity);
            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
        }

        /// <summary>按类型找在场 Boss (缓存校验 + 全表扫描兜底); 找不到返回 null。</summary>
        public static NPC FindBoss(int type, ref int cachedWho) {
            if (cachedWho >= 0 && cachedWho < Main.maxNPCs) {
                NPC n = Main.npc[cachedWho];
                if (n.active && n.type == type)
                    return n;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type) {
                    cachedWho = i;
                    return n;
                }
            }
            cachedWho = -1;
            return null;
        }

        /// <summary>清空本 Boss 单元名下的全部威胁弹幕 (换阶段/合体开场公平阀门)。服务器权威。</summary>
        public static void ClearHostileProjectiles() {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
                return;
            foreach (var p in Main.ActiveProjectiles) {
                if (p.ModProjectile == null)
                    continue;
                // 保留入场载体与复生反制圈 (非威胁演出件); 忘川水域虽非 hostile 也一并清除
                bool tide = p.type == Terraria.ModLoader.ModContent.ProjectileType<NiuMaTideField>();
                if (!p.hostile && !tide)
                    continue;
                string ns = p.ModProjectile.GetType().Namespace ?? "";
                if (ns.EndsWith("NiutouMamian"))
                    p.Kill();
            }
        }

        /// <summary>沿 Verlet 节点画垂坠锁链 (客户端视觉)。</summary>
        public static void DrawHangChain(SpriteBatch sb, Vector2[] nodes, Vector2 screenPos, Color tint, float alpha) {
            var tex = Terraria.GameContent.TextureAssets.Chains[0].Value;
            for (int i = 0; i < nodes.Length - 1; i++) {
                Vector2 a = nodes[i];
                Vector2 b = nodes[i + 1];
                float rot = (b - a).ToRotation() - MathHelper.PiOver2;
                float len = Vector2.Distance(a, b);
                var rec = new Rectangle(0, 0, tex.Width, (int)len + 2);
                sb.Draw(tex, a - screenPos, rec, tint * alpha, rot, new Vector2(tex.Width * 0.5f, 0f), 1f, SpriteEffects.None, 0f);
            }
        }

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
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 240;
            dust.scale = NiuMaHelper.Rand_Float(0.9f, 1.3f);
            dust.velocity = new Vector2(NiuMaHelper.Rand_Float(1, 3)).RotateRandom(7);
            dust.color = Color.SkyBlue;
            base.OnSpawn(dust);
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.02f;
            dust.velocity *= 0.97f;
            dust.alpha -= 5;
            if (dust.scale <= 0 || dust.velocity.Length() < 0.04f || dust.alpha < 0)
                dust.active = false;

            return false;
        }
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
                Main.spriteBatch.Draw(tx_Black, dust.position - Main.screenPosition, null, Color.Black * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 1f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(tx_Black, dust.position - Main.screenPosition, null, dust.color * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.15f, SpriteEffects.None, 0);
            }
            else {
                c.A = 0;
                Main.spriteBatch.Draw(tx, dust.position - Main.screenPosition, null, c * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(tx, dust.position - Main.screenPosition, null, new Color(1, 1, 1f, 0f) * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 0.7f * dust.scale * 0.15f, SpriteEffects.None, 0);
            }

            return false;
        }
    }
    public class Dust_2 : ModDust
    {
        public override string Texture => GetType().Namespace.Replace(".", "/") + "/Dust_5";
        public override void OnSpawn(Dust dust) {
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 255;
            dust.color = Color.Red;
            base.OnSpawn(dust);
        }
        public override bool Update(Dust dust) {
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
                Main.spriteBatch.Draw(Dust_1.tx_Black, dust.position - Main.screenPosition, null, Color.Black * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 1f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(Dust_1.tx_Black, dust.position - Main.screenPosition, null, dust.color * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.15f, SpriteEffects.None, 0);
            }
            else {
                c.A = 0;
                Main.spriteBatch.Draw(Dust_1.tx, dust.position - Main.screenPosition, null, c * (dust.alpha / 255f), 0, new Vector2(24, 24), 1.7f * dust.scale * 0.2f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(Dust_1.tx, dust.position - Main.screenPosition, null, new Color(1, 1, 1f, 0f) * 0.4f * (dust.alpha / 255f), 0, new Vector2(24, 24), 0.7f * dust.scale * 0.15f, SpriteEffects.None, 0);
            }

            return false;
        }
    }

}
