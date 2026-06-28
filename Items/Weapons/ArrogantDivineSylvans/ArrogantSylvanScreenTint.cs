using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·触发短暂染屏 (纯视觉地基件, 仅本武器线复用)。
///
/// 大招/触发瞬间释放一道金翠"定调"全屏调色 (<see cref="WeaponVFX.ApplyPaletteTint"/>, 强度≤0.12,
/// 占本帧唯一全屏后处理名额, 同屏 ≤1 由 <see cref="ACMShaders.RequestFullscreenSlot"/> 自动仲裁)。
/// 火铳「万棘狂涌」/ 长弓「世界树之矢」/ 典籍「叶暴漩涡」三技各自触发本弹一次, 名额冲突时helper自动跳过 (无害)。
/// damage=0, 不更新位置, owner 客户端生成 (染屏纯本地表现, 无需联机同步)。
/// </summary>
public class ArrogantSylvanScreenTint : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_1";

    private const int LifeTime = 26;

    public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
        if (Main.dedServ || Main.myPlayer != owner)
            return;
        Projectile.NewProjectile(source, worldPos, Vector2.Zero,
            ModContent.ProjectileType<ArrogantSylvanScreenTint>(), 0, 0f, owner);
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

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;

        // 0→1→0 钟形包络, 短暂定调
        float life = 1f - Projectile.timeLeft / (float)LifeTime;
        float env = MathHelper.Clamp((float)System.Math.Sin(life * System.Math.PI), 0f, 1f);
        float intensity = 0.12f * env;

        // 金翠双色: 阴影压暗翠, 高光提亮金
        WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
            shadowTint: new Color(28, 60, 30, 110),
            highlightTint: new Color(230, 235, 150, 95),
            intensity: intensity, saturation: 1.06f, hueShift: 0f);
        return false;
    }
}
