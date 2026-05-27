using AncientChineseMythology.Items.Potions;
using AncientChineseMythology.Underworlds.Items;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace AncientChineseMythology.NPCs.TownNPCs
{
    [AutoloadHead]
    public class MengPoNPC : ModNPC
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/TownNPCs/MengPo/MengPoNPC";
        public override string HeadTexture => "AncientChineseMythology/Textures/NPCs/TownNPCs/MengPo/MengPoNPC_Head";

        private static Profiles.StackedNPCProfile npcProfile;
        private static Asset<Texture2D> shimmerWeapon;

        public override void SetStaticDefaults() {
            //帧数/攻击帧配置——与向导一致
            Main.npcFrameCount[Type] = 25;
            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.DangerDetectRange[Type] = 500;
            NPCID.Sets.PrettySafe[Type] = 300;

            NPCID.Sets.AttackType[Type] = 1;  //近战摇符纸
            NPCID.Sets.AttackTime[Type] = 45; //较快
            NPCID.Sets.AttackAverageChance[Type] = 30;
            NPCID.Sets.HatOffsetY[Type] = 4;

            //ActsLikeTownNPC 但不占房
            NPCID.Sets.ActsLikeTownNPC[Type] = true;
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
            NPCID.Sets.SpawnsWithCustomName[Type] = true;
            NPCID.Sets.ShimmerTownTransform[Type] = true;

            //Bestiary 绘制偏移
            NPCID.Sets.NPCBestiaryDrawModifiers modifiers = new() {
                Velocity = 1f,
                Direction = 1
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, modifiers);

        }

        public override void SetDefaults() {
            NPC.friendly = true;
            NPC.townNPC = true;
            NPC.width = 18;
            NPC.height = 40;
            NPC.aiStyle = 7;                //Town AI
            AnimationType = NPCID.Guide;     //帧切换
            NPC.damage = 10;
            NPC.defense = 12;
            NPC.lifeMax = 250;
            NPC.knockBackResist = 0.5f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
        }

        public override void SetBestiary(BestiaryDatabase db, BestiaryEntry be) {
            be.Info.AddRange(new IBestiaryInfoElement[] {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("归去魂兮，小女子孟姜氏")
            });
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) {
            if (!Main.hardMode)           //← 新增
                return false;

            if (NPC.AnyNPCs(Type))
                return false;

            return true; //原有条件
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }

        public override bool CanChat() => true;

        public override string GetChat() {
            var chat = new WeightedRandom<string>();
            chat.Add("前尘如沸汤烫手，何必紧攥不放？饮尽此盏，方知忘字最是慈悲。");
            chat.Add("你道不忘是情深，却不知轮回三千载，刻骨相思终成他人碗中一粒盐。");
            chat.Add("喉间一滴忘川水，心头万座须弥山。饮罢启程吧，山自倾颓水自流。");
            chat.Add("若真修到心如镜，何惧前世影幢幢？此汤与尔，不过镜台拂尘。");
            return chat;                       //WeightedRandom 支持隐式转换
        }

        public override void SetChatButtons(ref string button, ref string button2) {
            button = Language.GetTextValue("LegacyInterface.28"); //“商店”
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shop) {
            if (firstButton)
                shop = "Shop";
        }

        public override void AddShops() {
            var shop = new NPCShop(Type)
                .Add<XuanYuanDan>()
                .Add<PoJingDan>();

            shop.Add(new Item(ModContent.ItemType<UnderworldInvasionSummon>()) {
                shopCustomPrice = Item.buyPrice(gold: 5)
            }, new Condition("Mods.AncientChineseMythology.Shop.MoonLord", () => NPC.downedMoonlord));

            shop.Register();
        }

        public override void TownNPCAttackStrength(ref int damage, ref float knockback) {
            damage = 18;
            knockback = 3f;
        }

        public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) {
            cooldown = 20;
            randExtraCooldown = 10;
        }

        public override void TownNPCAttackProj(ref int projType, ref int attackDelay) {
            projType = ProjectileID.PurificationPowder; //像撒符灰
            attackDelay = 1;
        }

        public override void TownNPCAttackProjSpeed(ref float mult, ref float grav, ref float randOff) {
            mult = 9f;
            randOff = 0.4f;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < (NPC.life > 0 ? 1 : 5); i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Copper);

            if (Main.netMode == NetmodeID.Server || NPC.life > 0)
                return;
        }

        public override ITownNPCProfile TownNPCProfile() => npcProfile;
    }
}