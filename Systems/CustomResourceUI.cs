using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using System.Collections.Generic;
using System.Linq;
using ReLogic.Graphics;

namespace AncientChineseMythology.Systems
{
    public class CustomResourceUI : ModSystem
    {
        // ─────────────────── ① 用一个“万能关键词”把所有原版资源条层清走 ──────────────────
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // 把所有 “同时包含 Resource 和 Bars” 的层都干掉
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                string name = layers[i].Name.ToLower();
                if (name.Contains("resource") && name.Contains("bars"))
                    layers.RemoveAt(i);
            }

            // 把我们的层放到最末尾（保证无论 UI 处于什么状态，我们都最后绘制）
            layers.Add(new LegacyGameInterfaceLayer(
                "AncientChineseMythology: Resource Bars",
                DrawResources,
                InterfaceScaleType.UI));
        }

        // ─────────────────── ② 统一常量（微调间距 & 位置）────────────────────
        const int ICON     = 22;   // 贴图像素
        const int GAP_H    = 2;    // 心之间水平空隙
        const int GAP_V    = 0;    // 行 / 列 垂直空隙
        const int BAR_PAD  = 14;   // 心区与星列之间的左右缓冲
        const int SCREEN_PAD = 10; // 整体离屏幕边缘距离
        const int DOWN_SHIFT = 0;  // 整块心区额外下移：避免压住小地图

        // ─────────────────── ③ 主绘制 ─────────────────────────────────────
        private bool DrawResources()
        {
            SpriteBatch sb = Main.spriteBatch;
            Player  pl    = Main.LocalPlayer;

            // —— 文本 “当前/最大生命” ——    
            string txt = $"{pl.statLife}/{pl.statLifeMax2}";
            var font   = FontAssets.MouseText.Value;
            Vector2 txtSize = font.MeasureString(txt);

            // 星列坐标（屏幕右边缘顶对齐）
            int starX = Main.screenWidth - SCREEN_PAD - ICON;
            int starY = SCREEN_PAD;

            // 心区宽度 & 坐标（保证和星列至少隔 BAR_PAD）
            int heartsWidth = ICON * 10 + GAP_H * 9;
            int heartsX     = starX - BAR_PAD - heartsWidth;
            int heartsY     = SCREEN_PAD + (int)txtSize.Y + GAP_V + DOWN_SHIFT;

            // 文本居中在心上方
            Vector2 txtPos = new(
                heartsX + heartsWidth / 2f - txtSize.X / 2f,
                SCREEN_PAD + DOWN_SHIFT);
            sb.DrawString(font, txt, txtPos, Color.White);

            DrawHearts(sb, pl, new Vector2(heartsX, heartsY));
            DrawStars (sb, pl, new Vector2(starX,  starY));

            return true;
        }

        // ─────────────────── ④ 生命心（两排）────────────────────────────────
        private static void DrawHearts(SpriteBatch sb, Player pl, Vector2 start)
        {
            Texture2D baseHeart = TextureAssets.Heart.Value;
            Texture2D heart1k   = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/UI/Heart1k").Value;
            Texture2D heart10k  = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/UI/Heart10k").Value;
            Texture2D heart100k = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/UI/Heart100k").Value;

            Texture2D[] icons = BuildIconSet(
                pl.statLifeMax2 - 400,
                new[] { 100_000, 10_000, 1_000 },
                new[] { heart100k, heart10k, heart1k },
                baseHeart);

            int filled = (int)System.Math.Ceiling(pl.statLife / 20f);
            for (int i = 0; i < 20; i++)
            {
                int row = i / 10, col = i % 10;
                Vector2 pos = start + new Vector2(col * (ICON + GAP_H),
                                                  row * (ICON + GAP_V));
                Color c = i < filled ? Color.White : Color.DarkGray * .8f;
                sb.Draw(icons[i], pos, c);
            }
        }

        // ─────────────────── ⑤ 魔力星（一列）─────────────────────────────────
        private static void DrawStars(SpriteBatch sb, Player pl, Vector2 start)
        {
            Texture2D baseStar = TextureAssets.Mana.Value;
            Texture2D star1k   = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/UI/Star1k").Value;
            Texture2D star10k  = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/UI/Star10k").Value;

            Texture2D[] icons = BuildIconSet(
                pl.statManaMax2 - 200,
                new[] { 10_000, 1_000 },
                new[] { star10k, star1k },
                baseStar);

            int filled = (int)System.Math.Ceiling(pl.statMana / 20f);
            for (int i = 0; i < 20; i++)
            {
                Vector2 pos = start + new Vector2(0, i * (ICON + GAP_V));
                Color c = i < filled ? Color.White : Color.DarkGray * .8f;
                sb.Draw(icons[i], pos, c);
            }
        }

        // ─────────────────── ⑥ 根据额外生命/魔力生成 20 颗图标序列 ────────────────
        private static Texture2D[] BuildIconSet(int extra,
                                                int[] tiers,
                                                Texture2D[] tex,
                                                Texture2D baseTex)
        {
            Texture2D[] arr = Enumerable.Repeat(baseTex, 20).ToArray();
            int back = 0;
            for (int i = 0; i < tiers.Length; i++)
            {
                int cnt = extra / tiers[i];
                extra  -= cnt * tiers[i];
                for (int j = 0; j < cnt && back < 20; j++, back++)
                    arr[19 - back] = tex[i];
            }
            return arr;
        }
    }
}
