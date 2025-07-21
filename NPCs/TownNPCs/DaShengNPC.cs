using AncientChineseMythology.Systems;
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
    public class DaShengNPC : ModNPC
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/TownNPCs/DaSheng/DaShengNPC";
        public override string HeadTexture => "AncientChineseMythology/Textures/NPCs/TownNPCs/DaSheng/DaShengNPC_Head";

        private static Profiles.StackedNPCProfile npcProfile;
        private static Asset<Texture2D> shimmerWeapon;

        public override void SetStaticDefaults() {
            // 帧数/攻击帧配置——与向导一致
            Main.npcFrameCount[Type] = 25;
            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.DangerDetectRange[Type] = 500;
            NPCID.Sets.PrettySafe[Type] = 300;

            NPCID.Sets.AttackType[Type] = 1;  // 近战摇符纸
            NPCID.Sets.AttackTime[Type] = 45; // 较快
            NPCID.Sets.AttackAverageChance[Type] = 30;
            NPCID.Sets.HatOffsetY[Type] = 4;

            // ActsLikeTownNPC 但不占房
            NPCID.Sets.ActsLikeTownNPC[Type] = true;
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
            NPCID.Sets.SpawnsWithCustomName[Type] = true;
            NPCID.Sets.ShimmerTownTransform[Type] = true;

            // Bestiary 绘制偏移
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
            NPC.aiStyle = 7;                // Town AI
            AnimationType = NPCID.Guide;     // 帧切换
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
                new FlavorTextBestiaryInfoElement("妖魔鬼怪快离开。妖魔鬼怪快离开")
            });
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) {
            if (!Main.hardMode)                 // ← 新增
                return false;

            if (NPC.AnyNPCs(Type))
                return false;

            return true;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }

        public override bool CanChat() => true;

        public override string GetChat() {
            var chat = new WeightedRandom<string>();
            chat.Add("俺老孙乃齐天大圣孙悟空！石破天惊，万妖俯首！");
            chat.Add("踏南天，碎凌霄。若一去不回……便一去不回！");
            chat.Add("天压我，我劈开这天；地阻我，我踏碎这地！");
            return chat;                       // WeightedRandom 支持隐式转换
        }

        public override void SetChatButtons(ref string button, ref string button2) {
            button = Language.GetTextValue("LegacyInterface.28"); // “商店”
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shop) {
            if (firstButton)
                shop = "Shop";
        }

        public override void AddShops() {
            new NPCShop(Type)
                .Register();
        }

        public override void TownNPCAttackStrength(ref int damage, ref float knockback) {
            damage = 108;
            knockback = 15f;
        }

        public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) {
            cooldown = 32;
            randExtraCooldown = 12;
        }

        public override void TownNPCAttackProj(ref int projType, ref int attackDelay) {
            projType = ProjectileID.PurificationPowder; // 像撒符灰
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
