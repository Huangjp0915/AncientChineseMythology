using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Items;

namespace AncientChineseMythology.NPCs.TownNPCs
{
    [AutoloadHead]
    public class TaiShangLaoJunNPC : ModNPC
    {
        // 使用武器商的贴图和头像
        public override string Texture => "Terraria/Images/NPC_19";
        public override string HeadTexture => "AncientChineseMythology/Textures/TaiShangLaoJun/TaiShangLaoJunNPC_Head";

        public override void SetStaticDefaults()
        {
            // 采用武器商的动画帧数
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.ArmsDealer];
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.townNPC = true;
            NPC.friendly = true;
            // 使用武器商的 AI
            NPC.aiStyle = 7;
            NPC.width = 18;
            NPC.height = 40;
            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
            AnimationType = NPCID.ArmsDealer;
            TownNPCStayingHomeless = true;
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) => true;
        public override bool CanChat() => true;

        public override List<string> SetNPCNameList()
        {
            return new List<string> { "太上老君" };
        }

        public override string GetChat()
        {
            string[] dialogues =
            {
                "我是太上老君，炼丹炉就在我的身边。",
                "炼丹之道，贵在炉火纯青！",
                "想炼丹？来我的店铺看看吧！"
            };
            return dialogues[Main.rand.Next(dialogues.Length)];
        }

        // 对话按钮仅显示“商店”
        public override void SetChatButtons(ref string button, ref string button2)
        {
            button = "商店";
            button2 = "";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (firstButton)
            {
                shopName = "TaiShangLaoJunShop";
            }
        }

        // 定义专属商店，出售炼丹炉（物品类名为 LianDanLu）
        public override void AddShops()
        {
            new NPCShop(Type, "TaiShangLaoJunShop")
                .Add<ElixirFurnaceItem>()
                .Add<PoJunDan>()
                .Add<XuePoDan>()
                .Add<NingShenDan>()
                .Add<XuanGangDan>()
                .Register();
        }
    }
}
