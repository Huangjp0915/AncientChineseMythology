using AncientChineseMythology.Items.Summons;
using AncientChineseMythology.Items.Weapons.Sticks;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.TownNPCs
{
    [AutoloadHead]
    public class TangSengNPC : ModNPC
    {
        //如果这张贴图跟向导帧数一致，就可直接在这里声明
        //默认贴图(走路/Idle/等)：
        public override string Texture => "AncientChineseMythology/Textures/NPCs/TownNPCs/Tangseng/TangSengNPC";
        //头像
        public override string HeadTexture => "AncientChineseMythology/Textures/NPCs/TownNPCs/Tangseng/TangSengNPC_Head";

        public override void SetStaticDefaults() {
            //---- 让NPC使用向导AI、向导动画 ----
            NPC.aiStyle = 7;       //TownNPC通用AI
            AIType = NPCID.Guide;  //行为(移动/攻击判定)仿照向导
            AnimationType = NPCID.Guide; //动画帧切换也交给向导

            //同步向导的帧数
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Guide];

            //复制向导设置，防止花屏或报错
            NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[NPCID.Guide];
            NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[NPCID.Guide];
            NPCID.Sets.DangerDetectRange[Type] = NPCID.Sets.DangerDetectRange[NPCID.Guide];
            NPCID.Sets.AttackType[Type] = NPCID.Sets.AttackType[NPCID.Guide];
            NPCID.Sets.AttackTime[Type] = NPCID.Sets.AttackTime[NPCID.Guide];
            NPCID.Sets.AttackAverageChance[Type] = NPCID.Sets.AttackAverageChance[NPCID.Guide];
            NPCID.Sets.ShimmerTownTransform[Type] = true;

            NPCID.Sets.NoTownNPCHappiness[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 18;
            NPC.height = 40;

            //再次确认
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

            //如果不想让他找房子住，就 = true
            //TownNPCStayingHomeless = true;
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) {
            if (NPC.AnyNPCs(Type)) return false;

            return true;
        }
        public override bool CanChat() => true;

        public override string GetChat() {
            string[] dialogues = {
                "我感应到人间妖气日盛，恐有截教之乱。",
                "封神大战的余波尚未平息，万望小心。",
                "多加留意那些“妖气碎片”，它们暗示着更大的阴谋……",
                "若想守护三界，先从一根小小的木棍开始吧……"
            };
            return dialogues[Main.rand.Next(dialogues.Length)];
        }

        //设置对话按钮
        public override void SetChatButtons(ref string button, ref string button2) {
            button = "帮助";
            button2 = "请求木棍";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName) {
            Player player = Main.LocalPlayer;
            //面向玩家
            NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;
            NPC.spriteDirection = NPC.direction;

            if (firstButton) {
                //“帮助”逻辑（检测棍子进阶）
                var stickProgression = new (int itemType, string itemName, string craftHint)[]
                {
                    (ModContent.ItemType<WoodenStick>(), "木棍","你可以去地底找寻一些铁矿，或许对你有些帮助"),
                    (ModContent.ItemType<IronStick>(),   "铁棍", "想要更进一步的话，金灿灿的或许不错？"),
                    (ModContent.ItemType<GoldenStick>(), "金棍", "施主，嫌弃伤害不够？你可以在上面镶嵌一些东西试试"),
                    (ModContent.ItemType<GemStick>(),    "宝石棍","下一步或许你就该下地狱了，你不下地狱谁下地狱，阿弥陀佛"),
                    (ModContent.ItemType<RuyiStick>(),   "如意棍","夜晚域外生物会给你带来你想要的东西，远古的黑暗和光明还有天空也会有你想要的东西"),
                    (ModContent.ItemType<TrueRuyiStick>(),"真·如意棍","或许你需要把那个邪恶的教徒召唤的东西干掉，阿弥陀佛"),
                    (ModContent.ItemType<RuyiJinguBang>(),"如意金箍棒","或者你可以去海边钓鱼试试，说不定有大货")
                };

                int highestIndex = -1;
                for (int i = 0; i < stickProgression.Length; i++) {
                    int type = stickProgression[i].itemType;
                    if (player.inventory.Any(item => item != null && item.type == type)) {
                        highestIndex = i;
                    }
                }

                if (highestIndex == -1) {
                    Main.npcChatText = "你还没有任何棍子，要不要找我拿一根呢？";
                }
                else {
                    var (curID, curName, curHint) = stickProgression[highestIndex];
                    if (highestIndex == stickProgression.Length - 1) {
                        Main.npcChatText = $"你已经有这根棍子了还不满足吗？再往上可就得找那些神仙了！{curHint}";
                    }
                    else {
                        var (nextID, nextName, nextHint) = stickProgression[highestIndex + 1];
                        Main.npcChatText = $"你现在有“{curName}”，下一步可以合成“{nextName}”。\n{nextHint}";
                    }
                }
            }
            else {
                bool hasStick = player.inventory.Any(item => item != null && item.type == ModContent.ItemType<WoodenStick>() && item.stack > 0);
                if (hasStick) {
                    Main.npcChatText = "阿弥陀佛，施主不要贪心";
                }
                else {
                    player.QuickSpawnItem(player.GetSource_FromThis(), ModContent.ItemType<WoodenStick>());
                    Main.npcChatText = "这根木棍送给你，但切记不可贪心";
                }

            }
            Main.player[Main.myPlayer].SetTalkNPC(NPC.whoAmI);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(Terraria.GameContent.ItemDropRules.ItemDropRule.Common(ModContent.ItemType<JiaSha>(), 1));
        }
    }
}
