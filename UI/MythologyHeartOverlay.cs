using AncientChineseMythology.Players;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;                //Asset<T>
using Terraria;
using Terraria.GameContent;           //TextureAssets
using Terraria.ModLoader;

namespace AncientChineseMythology.Content.UI;

[Autoload(Side = ModSide.Client)]
public class MythologyHeartOverlay : ModResourceOverlay
{
    private static int GetTier(Player p) =>
        p.GetModPlayer<MythologyPlayer>().GetResourceTier();

    public override bool PreDrawResource(ResourceOverlayDrawContext ctx) {
        //——— 判定正在画星还是心 ———
        Asset<Texture2D> tex = ctx.texture;

        if (ctx.resourceNumber >= 20)         //0-基序号：0-19 ⇒ 前 20 颗
            return false;                     //跳过绘制，UI 不再扩张

        //★ 星 0-3 帧
        int starFrame = -1;
        for (int i = 0; i < TextureAssets.Star.Length; i++) {
            if (tex == TextureAssets.Star[i]) { starFrame = i; break; }
        }
        bool isStar = starFrame != -1;

        //♥ 心（只有 Heart / Heart2 两张）
        bool isHeart = !isStar && (tex == TextureAssets.Heart || tex == TextureAssets.Heart2);

        if (!isHeart && !isStar)          //既不是心也不是星 → 不处理
            return true;

        //———拼自定义贴图路径 ———
        int tier = GetTier(Main.LocalPlayer);             //0=金心，1=橙心…
        string folder = isHeart ? "Hearts" : "Stars";
        string file = isHeart
                        ? (tex == TextureAssets.Heart2 ? "Heart2" : "Heart")   //保证两帧都能换
                        : $"Star_{starFrame}";                                //Star_0…Star_3

        string path = $"AncientChineseMythology/Textures/UI/{folder}/Tier{tier}/{file}";

        ctx.texture = ModContent.Request<Texture2D>(path);     //替换
        return true;                                           //继续原版绘制
    }
}
