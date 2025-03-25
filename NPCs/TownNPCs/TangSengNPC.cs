using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Graphics;
using AncientChineseMythology.Items; 

namespace AncientChineseMythology.NPCs
{
    [AutoloadHead]
    public class TangSengNPC : ModNPC
    {
        // 使用武器商的纹理（确保动画和纹理匹配）
        public override string Texture => "Terraria/Images/NPC_19";
        // 自定义头像纹理
        public override string HeadTexture => "AncientChineseMythology/Textures/Tangseng/TangSengNPC_Head";

        public override void SetStaticDefaults()
        {
            // 使用武器商的动画帧设置
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.ArmsDealer];
            NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[NPCID.ArmsDealer];
            NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[NPCID.ArmsDealer];
            NPCID.Sets.DangerDetectRange[Type] = NPCID.Sets.DangerDetectRange[NPCID.ArmsDealer];
            NPCID.Sets.AttackType[Type] = NPCID.Sets.AttackType[NPCID.ArmsDealer];
            NPCID.Sets.AttackTime[Type] = NPCID.Sets.AttackTime[NPCID.ArmsDealer];
            NPCID.Sets.AttackAverageChance[Type] = NPCID.Sets.AttackAverageChance[NPCID.ArmsDealer];
            NPCID.Sets.HatOffsetY[Type] = NPCID.Sets.HatOffsetY[NPCID.ArmsDealer];

            // 禁用幸福度系统，去除“快乐”按钮
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 18;
            NPC.height = 40;
            // Town NPC AI
            NPC.aiStyle = 7;
            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
            // 使用武器商的动画帧
            AnimationType = NPCID.ArmsDealer;
            // 根据需要设置NPC初始生成位置
            TownNPCStayingHomeless = true;
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) => true;
        public override bool CanChat() => true;

        public override List<string> SetNPCNameList()
        {
            return new List<string> { "唐僧" };
        }

        public override string GetChat()
        {
            string[] dialogues =
            {
                "我感应到人间妖气日盛，恐有截教之乱。",
                "封神大战的余波尚未平息，万望小心。",
                "多加留意那些“妖气碎片”，它们暗示着更大的阴谋……",
                "若想守护三界，先从一根小小的木棍开始吧……"
            };
            return dialogues[Main.rand.Next(dialogues.Length)];
        }

        // 设定两个对话按钮，第一个为“帮助”，第二个为“请求木棍”
        public override void SetChatButtons(ref string button, ref string button2)
        {
            button = "帮助";
            button2 = "请求木棍";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            Player player = Main.LocalPlayer;
            if (firstButton)
            {
                // 以下为检测棍子进阶逻辑，原有代码保持不变
                var stickProgression = new (int itemType, string itemName, string craftHint)[]
                {
                    (
                        ModContent.ItemType<WoodenStick>(),
                        "木棍",
                        "你可以去地底找寻一些铁矿，或许对你有些帮助" 
                    ),
                    (
                        ModContent.ItemType<IronStick>(),
                        "铁棍",
                        "想要更进一步的话，金灿灿的或许不错？"
                    ),
                    (
                        ModContent.ItemType<GoldenStick>(),
                        "金棍",
                        "施主，嫌弃伤害不够？你可以在上面镶嵌一些东西试试"
                    ),
                    (
                        ModContent.ItemType<GemStick>(),
                        "宝石棍",
                        "下一步或许你就该下地狱了，你不下地狱谁下地狱，阿弥陀佛"
                    ),
                    (
                        ModContent.ItemType<RuyiStick>(),
                        "如意棍",
                        "夜晚域外生物会给你带来你想要的东西，远古的黑暗和光明还有天空也会有你想要的东西"
                    ),
                    (
                        ModContent.ItemType<TrueRuyiStick>(),
                        "真·如意棍",
                        "或许你需要把那个邪恶的教徒召唤的东西干掉，阿弥陀佛"
                    ),
                    (
                        ModContent.ItemType<RuyiJinguBang>(),
                        "如意金箍棒",
                        "你已经有这根棍子了还不满足吗？那你可以去海边钓鱼试试，说不定有大货" // 最高级，没有下一级
                    )
                };

                int highestIndex = -1;
                for (int i = 0; i < stickProgression.Length; i++)
                {
                    int type = stickProgression[i].itemType;
                    if (player.inventory.Any(item => item != null && item.type == type))
                    {
                        highestIndex = i;
                    }
                }

                if (highestIndex == -1)
                {
                    Main.npcChatText = "你还没有任何棍子，要不要找我拿一根呢？";
                }
                else
                {
                    var (curID, curName, curHint) = stickProgression[highestIndex];
                    if (highestIndex == stickProgression.Length - 1)
                    {
                        Main.npcChatText = $"你已经拥有最高级的“{curName}”，再往上可就得找那些神仙了！\n{stickProgression[highestIndex].craftHint}";
                    }
                    else
                    {
                        var (nextID, nextName, nextHint) = stickProgression[highestIndex + 1];
                        Main.npcChatText = $"你现在有“{curName}”，下一步可以合成“{nextName}”。\n{stickProgression[highestIndex + 1].craftHint}";
                    }
                }
            }
            else
            {
                // 请求木棍按钮的逻辑
                int woodenStickType = ModContent.ItemType<WoodenStick>();
                // 判断玩家背包是否已有木棍
                bool hasStick = player.inventory.Any(item => item != null && item.type == woodenStickType);
                if (!hasStick)
                {
                    player.QuickSpawnItem(NPC.GetSource_GiftOrReward(), woodenStickType);
                }
                Main.npcChatText = "这个是一个调皮的猴子让我给有缘人的";
            }
            // 保持对话框打开
            Main.player[Main.myPlayer].SetTalkNPC(NPC.whoAmI);
        }
    }
}
