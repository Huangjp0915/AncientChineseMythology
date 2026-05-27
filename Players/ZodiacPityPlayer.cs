using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Players
{
    /// <summary>奇异石 / 生肖精魄保底计数（spec §3.4：500 / 1200）。</summary>
    public class ZodiacPityPlayer : ModPlayer
    {
        public const int StrangeStonePityKills = 500;
        public const int SpiritPityKills = 1200;

        public int StonePityCounter;
        public int SpiritPityCounter;

        public void OnMobKill(NPC npc) {
            if (npc.friendly || npc.lifeMax <= 5 || npc.boss) {
                return;
            }

            StonePityCounter++;
            SpiritPityCounter++;

            if (StonePityCounter >= StrangeStonePityKills) {
                StonePityCounter = 0;
                Item.NewItem(npc.GetSource_Loot(), npc.getRect(), ModContent.ItemType<StrangeStone>());
            }

            if (SpiritPityCounter >= SpiritPityKills) {
                SpiritPityCounter = 0;
                DropRandomSpirit(npc);
            }
        }

        private static void DropRandomSpirit(NPC npc) {
            int[] spirits = {
                ModContent.ItemType<ZodiacRat>(),
                ModContent.ItemType<ZodiacCow>(),
                ModContent.ItemType<ZodiacTiger>(),
                ModContent.ItemType<ZodiacRabbit>(),
                ModContent.ItemType<ZodiacDragon>(),
                ModContent.ItemType<ZodiacSnake>(),
                ModContent.ItemType<ZodiacHorse>(),
                ModContent.ItemType<ZodiacGoat>(),
                ModContent.ItemType<ZodiacMonkey>(),
                ModContent.ItemType<ZodiacChicken>(),
                ModContent.ItemType<ZodiacDog>(),
                ModContent.ItemType<ZodiacPig>()
            };

            int drop = spirits[Main.rand.Next(spirits.Length)];
            Item.NewItem(npc.GetSource_Loot(), npc.getRect(), drop);
        }

        public override void SaveData(TagCompound tag) {
            tag["StonePityCounter"] = StonePityCounter;
            tag["SpiritPityCounter"] = SpiritPityCounter;
        }

        public override void LoadData(TagCompound tag) {
            StonePityCounter = tag.GetInt("StonePityCounter");
            SpiritPityCounter = tag.GetInt("SpiritPityCounter");
        }
    }
}
