using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Items.Potions;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.SummoningStaffs;
using AncientChineseMythology.Items;

namespace AncientChineseMythology.NPCs.TownNPCs
{
    [AutoloadHead]
    public class TaiShangLaoJunNPC : ModNPC
    {
        // 使用武器商的贴图和头像
        public override string Texture => "AncientChineseMythology/Textures/NPCs/TownNPCs/TaiShangLaoJun/TaiShangLaoJunNPC";
        public override string HeadTexture => "AncientChineseMythology/Textures/NPCs/TownNPCs/TaiShangLaoJun/TaiShangLaoJunNPC_Head";

        public override void SetStaticDefaults()
        {
            // ---- 让NPC使用向导AI、向导动画 ----
            NPC.aiStyle = 7;       // TownNPC通用AI
            AIType = NPCID.Guide;  // 行为(移动/攻击判定)仿照向导
            AnimationType = NPCID.Guide; // 动画帧切换也交给向导

            // 同步向导的帧数
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Guide];

            // 复制向导设置，防止花屏或报错
            NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[NPCID.Guide];
            NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[NPCID.Guide];
            NPCID.Sets.DangerDetectRange[Type] = NPCID.Sets.DangerDetectRange[NPCID.Guide];
            NPCID.Sets.AttackType[Type] = NPCID.Sets.AttackType[NPCID.Guide]; 
            NPCID.Sets.AttackTime[Type] = NPCID.Sets.AttackTime[NPCID.Guide];
            NPCID.Sets.AttackAverageChance[Type] = NPCID.Sets.AttackAverageChance[NPCID.Guide];
            NPCID.Sets.ShimmerTownTransform[Type] = true;

            NPCID.Sets.NoTownNPCHappiness[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 18;
            NPC.height = 40;

            // 再次确认
            NPC.aiStyle = 7;
            AIType = NPCID.Guide;
            AnimationType = NPCID.Guide;

            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.noGravity = false;
            NPC.noTileCollide = false;

            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
        }

        public override bool CanTownNPCSpawn(int numTownNPCs)
        {
            if (NPC.AnyNPCs(Type)) return false;
            
            // 遍历所有玩家
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && !player.dead)
                {
                    int count = 0;
                    // 遍历玩家背包中的所有物品
                    for (int j = 0; j < player.inventory.Length; j++)
                    {
                        if (player.inventory[j].type == ModContent.ItemType<ScrapElixir>())
                        {
                            count += player.inventory[j].stack;
                        }
                    }
                    // 如果该玩家的【废丹】数量大于10，则允许此 NPC 生成
                    if (count > 10)
                        return true;
                }
            }
            return false;
        }
        public override bool CanChat() => true;

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

        // 定义专属商店
        public override void AddShops()
        {
            new NPCShop(Type, "TaiShangLaoJunShop")
                .Add<PoJunDan>()
                .Add<XuePoDan>()
                .Add<NingShenDan>()
                .Add<XuanGangDan>()
                .Add<DiHuo>()
                .Add<BaGuaZhenpan>()
                //.Add<ZhenfaBook>()  
                //.Add<ZhenfaPaper>()  
                //.Add<XuanYuanDan>()
                //.Add<PoJingDan>()  
                .Register();
        }
    }
}
