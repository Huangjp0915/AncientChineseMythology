using AncientChineseMythology.Celestias.Boss.AoGuangs;

using AncientChineseMythology.Celestias.Boss.Arguses;

using AncientChineseMythology.Celestias.Boss.Vigors;

using AncientChineseMythology.NPCs.Boss.Archosaur;

using AncientChineseMythology.NPCs.Boss.BlackBear;

using AncientChineseMythology.NPCs.Boss.Hanbas;

using AncientChineseMythology.NPCs.Boss.Hoqings;

using AncientChineseMythology.NPCs.Boss.Jiangcens;

using AncientChineseMythology.NPCs.Boss.KyuubiKitsunes;

using AncientChineseMythology.NPCs.Boss.Yingous;

using Microsoft.Xna.Framework;

using Microsoft.Xna.Framework.Graphics;

using System;

using System.Collections.Generic;

using Terraria.Localization;

using Terraria.ModLoader;



namespace AncientChineseMythology.Systems

{

    public class BossChecklistIntegration : ModSystem

    {

        private static readonly Version BossChecklistAPIVersion = new Version(1, 6);



        public override void PostSetupContent() {

            DoBossChecklistIntegration();

        }



        private void DoBossChecklistIntegration() {

            if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist) || bossChecklist.Version < BossChecklistAPIVersion) {

                return;

            }



            LogBoss(bossChecklist, "BlackBear", 0.1f, () => DownedBossSystem.downedBlackBear,

                ModContent.NPCType<BlackBear>(), "黑熊精",

                "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear");



            LogBoss(bossChecklist, "Archosaur", 0.1f, () => DownedBossSystem.downedArchosaur,

                ModContent.NPCType<ArchosaurBoss>(), "祖龙残魂",

                "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/Archosaur");



            LogBoss(bossChecklist, "Hanba", 0.12f, () => DownedBossSystem.downedHanba,

                ModContent.NPCType<Hanba>(), "旱魃",

                "AncientChineseMythology/NPCs/Boss/Hanbas/Hanba");



            LogBoss(bossChecklist, "Hoqing", 0.12f, () => DownedBossSystem.downedHoqing,

                ModContent.NPCType<Hoqing>(), "后卿",

                "AncientChineseMythology/NPCs/Boss/Hoqings/Hoqing");



            LogBoss(bossChecklist, "Yingou", 0.12f, () => DownedBossSystem.downedYingou,

                ModContent.NPCType<Yingou>(), "赢勾",

                "AncientChineseMythology/Textures/NPCs/Boss/Yingous/Yingou");



            LogBoss(bossChecklist, "Jiangcen", 0.12f, () => DownedBossSystem.downedJiangcen,

                ModContent.NPCType<Jiangcen>(), "将臣",

                "AncientChineseMythology/NPCs/Boss/Jiangcens/Jiangcen");



            LogBoss(bossChecklist, "Kyuubi", 0.15f, () => DownedBossSystem.downedKyuubi,

                ModContent.NPCType<KyuubiKitsune>(), "九尾妖狐",

                "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/KyuubiKitsune");



            LogBoss(bossChecklist, "AoGuang", 0.18f, () => DownedBossSystem.downedAoGuang,

                ModContent.NPCType<AoGuang>(), "东海龙王敖广",

                "AncientChineseMythology/Celestias/Boss/AoGuangs/AoGuang");



            LogBoss(bossChecklist, "Vigor", 0.2f, () => DownedBossSystem.downedVigor,

                ModContent.NPCType<Vigor>(), "神威·断罪刃",

                "AncientChineseMythology/Celestias/Boss/Vigors/Vigor");



            LogBoss(bossChecklist, "Argus", 0.22f, () => DownedBossSystem.downedArgus,

                ModContent.NPCType<Argus>(), "天目·追魂弧",

                "AncientChineseMythology/Celestias/Boss/Arguses/Argus");

        }



        private void LogBoss(Mod bossChecklist, string internalName, float weight, Func<bool> downed,

            int bossType, string displayName, string portraitPath) {

            var customPortrait = (SpriteBatch sb, Rectangle rect, Color color) => {

                Texture2D texture = ModContent.Request<Texture2D>(portraitPath).Value;

                Vector2 centered = new Vector2(

                    rect.X + (rect.Width / 2) - (texture.Width / 2),

                    rect.Y + (rect.Height / 2) - (texture.Height / 2));

                sb.Draw(texture, centered, color);

            };



            bossChecklist.Call(

                "LogBoss",

                Mod,

                internalName,

                weight,

                downed,

                bossType,

                new Dictionary<string, object>() {

                    ["displayName"] = Language.GetText(displayName),

                    ["customPortrait"] = customPortrait

                }

            );

        }

    }

}

