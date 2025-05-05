using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Systems
{
    public class BrokenHeavenBarrierPlayer : ModPlayer
    {
        public int barrierTimer;   // 进入后一秒内免疫

        public override void PreUpdate()
        {
            if (barrierTimer > 0) { barrierTimer--; return; }

            Rectangle pixelRect = BrokenHeavenIslandSystem.IslandRect;
            pixelRect.Inflate(8, 8);                               // 更小壳
            pixelRect = new Rectangle(pixelRect.X * 16, pixelRect.Y * 16,
                                    pixelRect.Width * 16, pixelRect.Height * 16);
                                  
            if (Player.inventory.Any(it => it.type == ModContent.ItemType<Items.SkyKey>()))
                return;   // 手持钥匙时允许靠近

            if (pixelRect.Contains(Player.Center.ToPoint()))
            {
                Vector2 fallback = new Vector2(Main.spawnTileX * 16f, (Main.spawnTileY - 3) * 16f);
                Player.Teleport(fallback, TeleportationStyleID.RodOfDiscord);
                if (Main.myPlayer == Player.whoAmI)
                    Main.NewText("一股神秘力量拒绝你接近天庭残骸……");
            }
        }
    }
}
