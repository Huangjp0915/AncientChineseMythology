using AncientChineseMythology.Systems;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace AncientChineseMythology.NPCs.TownNPCs
{
    [AutoloadHead]
    public class YangJianNPC : ModNPC
    {
        //------------------------ 静态字段 ------------------------
        private static Profiles.StackedNPCProfile npcProfile;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/TownNPCs/YangJian/YangJianNPC";
        public override string HeadTexture => "AncientChineseMythology/Textures/NPCs/TownNPCs/YangJian/YangJianNPC_Head";

        public override void Load() {
        }

        public override void SetStaticDefaults() {
            //★ 帧数与官方示例一致
            Main.npcFrameCount[Type] = 25;
            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.DangerDetectRange[Type] = 700;
            NPCID.Sets.PrettySafe[Type] = 300;
            NPCID.Sets.AttackType[Type] = 0;  //近战摇符纸
            NPCID.Sets.AttackTime[Type] = 30; //较快
            NPCID.Sets.AttackAverageChance[Type] = 30;
            NPCID.Sets.HatOffsetY[Type] = 4;
            NPCID.Sets.ShimmerTownTransform[Type] = true;

            //关键：既是 Town AI，又非真正城镇 NPC
            NPCID.Sets.ActsLikeTownNPC[Type] = true;
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
            NPCID.Sets.SpawnsWithCustomName[Type] = true;
            NPCID.Sets.FaceEmote[Type] = 0; //若有自定义表情可替换

            //Bestiary 绘制方向
            NPCID.Sets.NPCBestiaryDrawModifiers draw = new() {
                Velocity = 1f,
                Direction = 1
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, draw);
        }

        public override void SetDefaults() {
            NPC.friendly = true;
            NPC.townNPC = true;
            NPC.width = 18;
            NPC.height = 40;
            NPC.aiStyle = 7;
            NPC.damage = 15;
            NPC.defense = 18;
            NPC.lifeMax = 300;
            NPC.knockBackResist = 0.5f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;

            AnimationType = NPCID.Guide; //复用向导帧切换方式
        }

        public override void SetBestiary(BestiaryDatabase db, BestiaryEntry be) {
            be.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("天庭二郎神下凡行走人间。")
            ]);
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) {
            if (NPC.AnyNPCs(Type)) return false;

            return DownedBossSystem.downedBlackBear;
        }

        public override bool CanChat() => true;

        public override string GetChat() {
            WeightedRandom<string> chat = new();
            chat.Add("斩妖除魔，责无旁贷。");
            chat.Add("哮天犬又跑丢了……");
            return chat;
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
