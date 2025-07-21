using AncientChineseMythology.Items;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Players
{
    public class ZhenfaPlayer : ModPlayer
    {
        public List<string> DiscoveredRecipes = new();

        #region ── 保存 / 读取 ────────────────────────────────────────────
        public override void SaveData(TagCompound tag) {
            tag["ZhenfaRecipes"] = DiscoveredRecipes;
        }

        public override void LoadData(TagCompound tag) {
            DiscoveredRecipes = tag.Get<List<string>>("ZhenfaRecipes") ?? new();
        }
        #endregion

        #region ── API 给纸张调用 ────────────────────────────────────────
        public string DiscoverRandomRecipe() {
            List<string> candidates = ZhenfaRecipeCatalog.AllRecipes.FindAll(r => !DiscoveredRecipes.Contains(r));
            if (candidates.Count == 0) return null;

            string pick = Main.rand.NextFromList(candidates.ToArray());
            DiscoveredRecipes.Add(pick);

            if (Main.myPlayer == Player.whoAmI) {
                Main.NewText($"你领悟了新的阵法：{pick}！", Color.LightGreen);
                SoundEngine.PlaySound(SoundID.Unlock, Player.Center);
            }

            // 若 UI 正在打开，刷新列表
            if (ZhenfaUISystem.ShowBookUI)
                ZhenfaUISystem.BookUI?.RebuildList();

            return pick;
        }
        #endregion
    }
}
