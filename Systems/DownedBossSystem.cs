using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Systems
{
    public class DownedBossSystem : ModSystem
    {
        public static bool downedBlackBear = false; //跟踪 BlackBear 是否已被击败
        public static bool downedHanba = false;
        public static bool downedHoqing = false;
        public static bool downedJiangcen = false;
        public static bool downedYingou = false;
        public static bool downedArchosaur = false;
        public static bool downedAoGuang = false; //跟踪东海龙王敖广是否已被击败
        public static bool downedAokin = false; //跟踪南海龙王敖钦是否已被击败
        public static bool downedAoyuan = false; //跟踪西海龙王敖闰是否已被击败
        public static bool downedAoshun = false; //跟踪北海龙王敖顺是否已被击败
        public static bool downedQinlong = false; //跟踪青龙是否已被击败
        public static bool downedBaihu = false; //跟踪白虎是否已被击败
        public static bool downedSuzaku = false; //跟踪朱雀是否已被击败
        public static bool downedXuanwu = false; //跟踪玄武是否已被击败
        public static bool downedVigor = false; //跟踪神威·断罪刃是否已被击败
        public static bool downedArgus = false; //跟踪天目·追魂弧是否已被击败
        public static bool downedVaisravana = false; //跟踪毗沙门天是否已被击败
        public static bool downedCelestialOverseer = false; //跟踪天庭观察者是否已被击败
        public static bool downedDazheng = false; //跟踪大椿是否已被击败
        public static bool downedDryads = false; //跟踪树精是否已被击败
        public static bool downedHeavenInvasion = false; //跟踪天庭入侵事件是否已被击退
        public static bool downedUnderworldInvasion = false; //跟踪地府入侵事件是否已被击退
        public static bool downedBlackImpermanence = false;
        public static bool downedWhiteImpermanence = false;
        public static bool downedSpectre = false;
        public static bool downedNetherDragon = false;
        public static bool downedYinEmperor = false;
        public static bool downedKyuubi = false;
        public static bool downedNiuMa = false;
        public static bool downedCelestialDragon = false;
        public static bool downedAzureDragon = false;

        public override void SaveWorldData(TagCompound tag) {
            tag["downedBlackBear"] = downedBlackBear; //保存状态
            tag["downedHanba"] = downedHanba;
            tag["downedHoqing"] = downedHoqing;
            tag["downedJiangcen"] = downedJiangcen;
            tag["downedYingou"] = downedYingou;
            tag["downedArchosaur"] = downedArchosaur; //保存状态
            tag["downedAoGuang"] = downedAoGuang; //保存东海龙王状态
            tag["downedAokin"] = downedAokin; //保存南海龙王状态
            tag["downedAoyuan"] = downedAoyuan; //保存西海龙王状态
            tag["downedAoshun"] = downedAoshun; //保存北海龙王状态
            tag["downedQinlong"] = downedQinlong; //保存青龙状态
            tag["downedBaihu"] = downedBaihu; //保存白虎状态
            tag["downedSuzaku"] = downedSuzaku; //保存朱雀状态
            tag["downedXuanwu"] = downedXuanwu; //保存玄武状态
            tag["downedVigor"] = downedVigor; //保存神威·断罪刃状态
            tag["downedArgus"] = downedArgus; //保存天目·追魂弧状态
            tag["downedVaisravana"] = downedVaisravana; //保存毗沙门天状态
            tag["downedCelestialOverseer"] = downedCelestialOverseer; //保存天庭观察者状态
            tag["downedDazheng"] = downedDazheng; //保存大椿状态
            tag["downedDryads"] = downedDryads; //保存树精状态
            tag["downedHeavenInvasion"] = downedHeavenInvasion; //保存天庭入侵状态
            tag["downedUnderworldInvasion"] = downedUnderworldInvasion; //保存地府入侵状态
            tag["downedBlackImpermanence"] = downedBlackImpermanence;
            tag["downedWhiteImpermanence"] = downedWhiteImpermanence;
            tag["downedSpectre"] = downedSpectre;
            tag["downedNetherDragon"] = downedNetherDragon;
            tag["downedYinEmperor"] = downedYinEmperor;
            tag["downedKyuubi"] = downedKyuubi;
            tag["downedNiuMa"] = downedNiuMa;
            tag["downedCelestialDragon"] = downedCelestialDragon;
            tag["downedAzureDragon"] = downedAzureDragon;
        }

        public override void LoadWorldData(TagCompound tag) {
            downedBlackBear = tag.GetBool("downedBlackBear"); //加载状态
            downedHanba = tag.GetBool("downedHanba");
            downedHoqing = tag.GetBool("downedHoqing");
            downedJiangcen = tag.GetBool("downedJiangcen");
            downedYingou = tag.GetBool("downedYingou");
            downedArchosaur = tag.GetBool("downedArchosaur"); //加载状态
            downedAoGuang = tag.GetBool("downedAoGuang"); //加载东海龙王状态
            downedAokin = tag.GetBool("downedAokin"); //加载南海龙王状态
            downedAoyuan = tag.GetBool("downedAoyuan"); //加载西海龙王状态
            downedAoshun = tag.GetBool("downedAoshun"); //加载北海龙王状态
            downedQinlong = tag.GetBool("downedQinlong"); //加载青龙状态
            downedBaihu = tag.GetBool("downedBaihu"); //加载白虎状态
            downedSuzaku = tag.GetBool("downedSuzaku"); //加载朱雀状态
            downedXuanwu = tag.GetBool("downedXuanwu"); //加载玄武状态
            downedVigor = tag.GetBool("downedVigor"); //加载神威·断罪刃状态
            downedArgus = tag.GetBool("downedArgus"); //加载天目·追魂弧状态
            downedVaisravana = tag.GetBool("downedVaisravana"); //加载毗沙门天状态
            downedCelestialOverseer = tag.GetBool("downedCelestialOverseer"); //加载天庭观察者状态
            downedDazheng = tag.GetBool("downedDazheng"); //加载大椿状态
            downedDryads = tag.GetBool("downedDryads"); //加载树精状态
            downedHeavenInvasion = tag.GetBool("downedHeavenInvasion"); //加载天庭入侵状态
            downedUnderworldInvasion = tag.GetBool("downedUnderworldInvasion"); //加载地府入侵状态
            downedBlackImpermanence = tag.GetBool("downedBlackImpermanence");
            downedWhiteImpermanence = tag.GetBool("downedWhiteImpermanence");
            downedSpectre = tag.GetBool("downedSpectre");
            downedNetherDragon = tag.GetBool("downedNetherDragon");
            downedYinEmperor = tag.GetBool("downedYinEmperor");
            downedKyuubi = tag.GetBool("downedKyuubi");
            downedNiuMa = tag.GetBool("downedNiuMa");
            downedCelestialDragon = tag.GetBool("downedCelestialDragon");
            downedAzureDragon = tag.GetBool("downedAzureDragon");
        }

        public override void OnWorldUnload() {
            downedBlackBear = false;
            downedHanba = false;
            downedHoqing = false;
            downedJiangcen = false;
            downedYingou = false;
            downedArchosaur = false;
            downedAoGuang = false;
            downedAokin = false;
            downedAoyuan = false;
            downedAoshun = false;
            downedQinlong = false;
            downedBaihu = false;
            downedSuzaku = false;
            downedXuanwu = false;
            downedVigor = false;
            downedArgus = false;
            downedVaisravana = false;
            downedCelestialOverseer = false;
            downedDazheng = false;
            downedDryads = false;
            downedHeavenInvasion = false;
            downedUnderworldInvasion = false;
            downedBlackImpermanence = false;
            downedWhiteImpermanence = false;
            downedSpectre = false;
            downedNetherDragon = false;
            downedYinEmperor = false;
            downedKyuubi = false;
            downedNiuMa = false;
            downedCelestialDragon = false;
            downedAzureDragon = false;
        }
    }
}
