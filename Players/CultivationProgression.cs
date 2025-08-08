namespace AncientChineseMythology.Players;

public static class CultivationProgression
{
    public static readonly string[] MajorNames = {
        "炼精化气","炼气化神","炼神返虚","炼虚合道",
        "人仙","地仙","天仙","金仙",
        "太乙金仙","大罗金仙","准圣","圣人",
        "至圣","合道","天道","大道"
    };

    public const int MinorPerMajor = 4;
    public static readonly string[] MinorNames = { "初期", "中期", "后期", "大圆满" };

    //经验表：每个小境界所需的「阶段经验」。这里按等差递增可随时调整
    public static int ExpFor(int major, int minor)
        => 500 * (major * MinorPerMajor + minor + 1);

    //击杀表：想晋升下一个大境界需要的击杀量
    public static readonly int[] KillsForMajorUp = {
        100, 1000, 5000, 10000,
        30000, 80000, 100000, 150000,
        200000, 300000, 500000, 1000000,
        1500000, 2500000, 5000000, 10000000
    };

    public static readonly int[] MajorHealthBonusTable = {
        200,  300,  500,  500,
        1000, 2000, 2500, 3000,
        5000, 5000, 10000, 15000,
        20000, 20000, 20000, 25000
    };
    public static readonly int[] MajorManaBonusTable = {
        20, 50, 100,  180,
        250,400,500,500,
        1000,1000,2000,4000,
        5000,5000,5000,5000
    };
    public static readonly int[] MajorDefenseBonusTable = {
        10,20,50,100,200,500,800,1200,1500,2000,2500,3500,5000,7000,9000,10000
    };
    public static readonly float[] MajorDamageBonusTable = {
        0.08f,0.20f,0.30f,0.50f,
        0.80f,1f,1.20f,1.50f,
        1.80f,2f,2.20f,2.80f,
        3.30f,3.90f,4.5f,5f
    };

    // 小境界增益基准随大境界线性递增，比如：
    // 在 major=0 时 minor 每级增益为 (10,10,1,0.02)
    // 在 major=N 时增益为 (10 + 2*N, 10 + 2*N, 1 + 0.5*N, 0.02 + 0.005*N)
    public static (int hp, int mana, int def, float dmg) GetMinorBonusBase(int major) {
        int hp = 10 + 2 * major;
        int mana = 10 + 2 * major;
        int def = 1 + major / 2;
        float dmg = 0.02f + 0.005f * major;
        return (hp, mana, def, dmg);
    }
}
