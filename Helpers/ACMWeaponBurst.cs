using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Helpers
{
    /// <summary>
    /// 通用一次性命中/蓄力演出弹幕 (地基件, 全武器复用)。
    ///
    /// 为什么需要它: 命中反馈跑在**更新阶段** (ModItem/ModProjectile.OnHitNPC 等), 那里没有绘制上下文,
    /// 不能直接调 <see cref="WeaponVFX"/> 的绘制方法。生成一个本弹幕, 让它在自己的 PreDraw 里跑
    /// 径向辉光 (§B.8) + 冲击环 + 柔光闪 (§B.4), 即可把 shader 级演出安全地接到命中点。
    /// 纯视觉 (damage=0), 走 owner 客户端生成并同步; 主题配色由 <see cref="Projectile.ai"/>[0] 决定 (MP 安全)。
    /// </summary>
    public class ACMWeaponBurst : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        public const int Generic = 0;
        public const int CupriteBurn = 1;
        public const int XuanTieBleed = 2;
        public const int Nature = 3;
        // —— 前期/杂项武器重做扩展主题 (集中登记, 各线 worker 只消费勿改本文件; 值占 20-34 区段, 避开天庭 4-8 / 地府 10-15) ——
        public const int Bronze = 20;          // 青铜断金 (暖金绿)
        public const int Crimson = 21;         // 赤铜剑/猩红火刃 (橙红)
        public const int Gold = 22;            // 金辉 (金棍/龙符金)
        public const int Gem = 23;             // 宝石多彩 (紫青)
        public const int Fatal = 24;           // 致命纯红 #FF2838 (如意棒/致命预警)
        public const int DivineWood = 25;      // 神木翠绿 (深翠)
        public const int ArrogantSylvan = 26;  // 傲世神木金翠双色
        public const int Profane = 27;         // 亵渎暗红血肉
        public const int Soul = 28;            // 万魂幡幽紫
        public const int Fox = 29;             // 九尾狐火 (金橙)
        public const int FoxCharm = 30;        // 妲己狐魅 (紫红)
        public const int Scorch = 31;          // 旱魃焦土 (橙红)
        public const int Bone = 32;            // 骨白
        public const int Shadow = 33;          // 地府幽蓝 (冥鸦/鬼火)
        public const int Water = 34;           // 河豚水纹 (冰蓝)

        // ===== 地府线 (Underworlds) 主题 (青黄魂火 / 暗冥幽蓝紫 / 鬼绿 / 酆都虚空黑紫 / 幽冥龙青蓝冥焰 / 致命纯红) =====
        // 注: 值从 10 起以避开天庭线 (4-8) 占用, 避免 GetColors 重复 case 标签。
        /// <summary>青黄魂火 (怨灵/亡魂线通用, 与 SpectreHelper 配色一致)。</summary>
        public const int SoulFire = 10;
        /// <summary>暗冥幽蓝紫 (亡魂EX/黄泉线)。</summary>
        public const int AbyssPurple = 11;
        /// <summary>鬼绿 (千骸/骨白暗绿线)。</summary>
        public const int GhostGreen = 12;
        /// <summary>酆都虚空黑紫 (酆都终极梯队)。</summary>
        public const int FengduVoid = 13;
        /// <summary>幽冥龙青蓝冥焰 (幽冥龙/怨念叠层线)。</summary>
        public const int NetherGrudge = 14;
        /// <summary>致命预警纯红 #FF2838 (处决/即死/引爆)。</summary>
        public const int LethalRed = 15;
        // ===== 天庭线 (Celestias) 武器重做主题 =====
        /// <summary>敖广·东海水龙 — 冰蓝/深青潮汐。</summary>
        public const int EastSeaWater = 4;
        /// <summary>观察者·机关飞升 — 机关金 + 天青。</summary>
        public const int ClockworkGold = 5;
        /// <summary>天龙·金龙巡卫 — 纯金龙威。</summary>
        public const int GoldDragon = 6;
        /// <summary>祖龙·残魂迷幻 — 白青→赤金 (与 AncestralDragonSky 同源)。</summary>
        public const int AncestralSoul = 7;
        /// <summary>天柱守卫 — 金白祥瑞。</summary>
        public const int HeavenlyPillar = 8;

        private const int LifeTime = 18;

        private int Theme => (int)Projectile.ai[0];
        private float MaxScale => Projectile.ai[1] <= 0f ? 1f : Projectile.ai[1];

        /// <summary>
        /// 在世界点生成一次命中演出 (仅 owner 客户端调用并同步)。
        /// </summary>
        /// <param name="source">实体来源 (一般 proj.GetSource_OnHit / player.GetSource_ItemUse)。</param>
        /// <param name="worldPos">演出中心。</param>
        /// <param name="theme">主题: <see cref="CupriteBurn"/> / <see cref="XuanTieBleed"/> / <see cref="Nature"/> / <see cref="Generic"/>。</param>
        /// <param name="scale">规模倍率 (1≈普通命中, 2≈重击/爆炸)。</param>
        /// <param name="owner">归属玩家 whoAmI。</param>
        public static void Spawn(IEntitySource source, Vector2 worldPos, int theme, float scale, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<ACMWeaponBurst>(), 0, 0f, owner, theme, scale);
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
        }

        private void GetColors(out Color bloom, out Color ringInner, out Color ringOuter) {
            switch (Theme) {
                case CupriteBurn:
                    bloom = new Color(255, 150, 50);
                    ringInner = new Color(255, 190, 90);
                    ringOuter = new Color(220, 60, 20);
                    break;
                case XuanTieBleed:
                    bloom = new Color(190, 40, 40);
                    ringInner = new Color(220, 70, 70);
                    ringOuter = new Color(90, 10, 10);
                    break;
                case Nature:
                    bloom = new Color(120, 240, 110);
                    ringInner = new Color(170, 255, 130);
                    ringOuter = new Color(40, 170, 60);
                    break;
                case EastSeaWater:
                    bloom = new Color(90, 200, 245);
                    ringInner = new Color(170, 240, 255);
                    ringOuter = new Color(30, 90, 170);
                    break;
                case ClockworkGold:
                    bloom = new Color(255, 215, 120);
                    ringInner = new Color(255, 240, 180);
                    ringOuter = new Color(80, 200, 180); // 天青
                    break;
                case GoldDragon:
                    bloom = new Color(255, 205, 90);
                    ringInner = new Color(255, 240, 170);
                    ringOuter = new Color(200, 130, 30);
                    break;
                case AncestralSoul:
                    bloom = new Color(200, 235, 255); // 白青残魂
                    ringInner = new Color(235, 250, 255);
                    ringOuter = new Color(255, 170, 70); // 赤金边
                    break;
                case HeavenlyPillar:
                    bloom = new Color(255, 245, 200);
                    ringInner = new Color(255, 255, 235);
                    ringOuter = new Color(150, 220, 235); // 青
                    break;
                case SoulFire:
                    bloom = new Color(120, 240, 210);
                    ringInner = new Color(255, 220, 120);
                    ringOuter = new Color(30, 120, 110);
                    break;
                case AbyssPurple:
                    bloom = new Color(150, 110, 240);
                    ringInner = new Color(190, 150, 255);
                    ringOuter = new Color(60, 30, 110);
                    break;
                case GhostGreen:
                    bloom = new Color(120, 230, 140);
                    ringInner = new Color(200, 255, 180);
                    ringOuter = new Color(30, 90, 60);
                    break;
                case FengduVoid:
                    bloom = new Color(120, 60, 200);
                    ringInner = new Color(180, 120, 255);
                    ringOuter = new Color(25, 8, 40);
                    break;
                case NetherGrudge:
                    bloom = new Color(90, 200, 240);
                    ringInner = new Color(150, 230, 255);
                    ringOuter = new Color(20, 70, 130);
                    break;
                case LethalRed:
                    bloom = new Color(250, 60, 70);
                    ringInner = new Color(255, 120, 120);
                    ringOuter = new Color(120, 10, 16);
                    break;
                // —— 前期/杂项线 (20-34) ——
                case Bronze:
                    bloom = new Color(210, 180, 90);
                    ringInner = new Color(235, 215, 140);
                    ringOuter = new Color(140, 95, 30);
                    break;
                case Crimson:
                    bloom = new Color(255, 120, 60);
                    ringInner = new Color(255, 175, 95);
                    ringOuter = new Color(190, 30, 20);
                    break;
                case Gold:
                    bloom = new Color(255, 225, 130);
                    ringInner = new Color(255, 245, 190);
                    ringOuter = new Color(200, 150, 40);
                    break;
                case Gem:
                    bloom = new Color(180, 140, 255);
                    ringInner = new Color(150, 230, 255);
                    ringOuter = new Color(95, 60, 185);
                    break;
                case Fatal:
                    bloom = new Color(250, 40, 56);
                    ringInner = new Color(255, 95, 105);
                    ringOuter = new Color(150, 10, 20);
                    break;
                case DivineWood:
                    bloom = new Color(110, 240, 140);
                    ringInner = new Color(195, 255, 155);
                    ringOuter = new Color(30, 150, 75);
                    break;
                case ArrogantSylvan:
                    bloom = new Color(255, 225, 120);
                    ringInner = new Color(185, 255, 150);
                    ringOuter = new Color(45, 175, 85);
                    break;
                case Profane:
                    bloom = new Color(205, 30, 40);
                    ringInner = new Color(245, 75, 75);
                    ringOuter = new Color(80, 5, 12);
                    break;
                case Soul:
                    bloom = new Color(180, 120, 255);
                    ringInner = new Color(210, 165, 255);
                    ringOuter = new Color(95, 40, 165);
                    break;
                case Fox:
                    bloom = new Color(255, 180, 80);
                    ringInner = new Color(255, 215, 120);
                    ringOuter = new Color(200, 70, 25);
                    break;
                case FoxCharm:
                    bloom = new Color(255, 90, 150);
                    ringInner = new Color(255, 160, 210);
                    ringOuter = new Color(165, 20, 95);
                    break;
                case Scorch:
                    bloom = new Color(255, 150, 50);
                    ringInner = new Color(255, 195, 95);
                    ringOuter = new Color(165, 60, 12);
                    break;
                case Bone:
                    bloom = new Color(230, 230, 210);
                    ringInner = new Color(255, 255, 240);
                    ringOuter = new Color(140, 140, 120);
                    break;
                case Shadow:
                    bloom = new Color(110, 180, 235);
                    ringInner = new Color(155, 215, 255);
                    ringOuter = new Color(45, 70, 145);
                    break;
                case Water:
                    bloom = new Color(120, 200, 255);
                    ringInner = new Color(185, 230, 255);
                    ringOuter = new Color(40, 95, 185);
                    break;
                default:
                    bloom = new Color(255, 250, 220);
                    ringInner = new Color(255, 255, 255);
                    ringOuter = new Color(160, 160, 200);
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)LifeTime; // 0→1
            GetColors(out Color bloom, out Color ringInner, out Color ringOuter);

            // 快起慢落的脉冲
            float pulse = MathHelper.Clamp(life < 0.25f ? life / 0.25f : 1f - (life - 0.25f) / 0.75f, 0f, 1f);

            // 柔光闪 (廉价, 总有)
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.2f + life * 1.5f) * MaxScale, bloom * (pulse * 0.8f));

            // 冲击环 (扩张 + 衰减)
            float ringRadius = (8f + life * 64f) * MaxScale;
            WeaponVFX.DrawShockwaveRing(Projectile.Center, ringRadius, 10f * MaxScale, pulse * 0.9f, ringInner, ringOuter);

            // 径向辉光 (走全屏名额, 名额满则内部退化为柔光) — 峰值期才申请, 省名额
            if (pulse > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.07f * MaxScale, pulse * 0.7f, bloom, 8f);

            return false;
        }
    }
}
