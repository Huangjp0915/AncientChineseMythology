using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons.Items
{
    /// <summary>
    /// 幽冥武器共用的视觉与机制工具。
    /// 三件幽冥龙武器都会向敌人叠加「幽冥怨念」，叠满后引爆灵魂湮灭。
    /// </summary>
    public static class NetherFX
    {
        // 幽冥配色：青焰 → 紫魂 → 深渊
        public static readonly Color Cyan = new Color(95, 205, 255);
        public static readonly Color Violet = new Color(150, 100, 255);
        public static readonly Color Deep = new Color(45, 65, 150);

        public static Color Mix(float t) => Color.Lerp(Cyan, Violet, t);

        /// <summary>软发光圆球纹理（复用原版暗影球，无需额外 PNG）。</summary>
        public static Texture2D OrbTexture {
            get {
                Main.instance.LoadProjectile(ProjectileID.ShadowOrb);
                return TextureAssets.Projectile[ProjectileID.ShadowOrb].Value;
            }
        }

        /// <summary>加色风格的双层发光绘制。</summary>
        public static void DrawGlow(SpriteBatch sb, Vector2 worldCenter, Color color, float scale, float rotation = 0f) {
            Texture2D tex = OrbTexture;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = worldCenter - Main.screenPosition;
            Color c = color;
            c.A = 0;
            sb.Draw(tex, pos, null, c * 0.45f, rotation, origin, scale * 1.7f, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, c * 0.85f, rotation, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, Color.White with { A = 0 } * 0.5f, rotation, origin, scale * 0.45f, SpriteEffects.None, 0f);
        }

        public static void SoulDust(Vector2 pos, float speed, int count, float scale = 1.2f) {
            for (int i = 0; i < count; i++) {
                int type = Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch;
                Dust d = Dust.NewDustPerfect(pos, type, Main.rand.NextVector2Circular(speed, speed), 80, default, scale);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 幽冥怨念 — 由幽冥龙系武器叠加的灵魂腐蚀层数。
    /// 叠加时造成持续灵魂伤害；达到上限自动引爆灵魂湮灭爆。
    /// </summary>
    public class NetherGrudgeGlobalNPC : GlobalNPC
    {
        public const int MaxStacks = 8;

        public int Stacks;
        private int decayTimer;

        public override bool InstancePerEntity => true;

        /// <summary>由弹幕叠加怨念。</summary>
        public static void AddGrudge(NPC npc, int amount, Projectile source) =>
            AddGrudgeInternal(npc, amount, source.owner, source.damage);

        /// <summary>由近战直击叠加怨念。</summary>
        public static void AddGrudge(NPC npc, int amount, Player source, int weaponDamage) =>
            AddGrudgeInternal(npc, amount, source.whoAmI, weaponDamage);

        private static void AddGrudgeInternal(NPC npc, int amount, int owner, int srcDamage) {
            if (npc == null || !npc.active || npc.friendly || npc.dontTakeDamage)
                return;

            var grudge = npc.GetGlobalNPC<NetherGrudgeGlobalNPC>();
            grudge.Stacks = Math.Min(MaxStacks, grudge.Stacks + amount);
            grudge.decayTimer = 180;

            if (Main.netMode != NetmodeID.Server)
                NetherFX.SoulDust(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f), 2f, 2 + amount);

            if (grudge.Stacks >= MaxStacks) {
                grudge.Stacks = 0;
                grudge.decayTimer = 0;
                Detonate(npc, owner, srcDamage);
            }
        }

        private static void Detonate(NPC npc, int owner, int srcDamage) {
            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.35f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.2f, Volume = 0.7f }, npc.Center);

            if (owner == Main.myPlayer) {
                int dmg = Math.Max(40, (int)(srcDamage * 3.2f));
                Projectile.NewProjectile(new EntitySource_Parent(npc), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<NetherSoulExplosion>(), dmg, 4f, owner);
            }
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (Stacks <= 0)
                return;

            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            int dot = Stacks * 14; // 每层 7 HP/s 灵魂腐蚀
            npc.lifeRegen -= dot;
            int perTick = dot / 8;
            if (perTick > 0 && damage < perTick)
                damage = perTick;
        }

        public override void PostAI(NPC npc) {
            if (Stacks <= 0)
                return;

            if (--decayTimer <= 0) {
                Stacks--;
                decayTimer = 50;
            }

            if (Main.netMode == NetmodeID.Server)
                return;

            // 怨念越深，灵魂余烬越浓
            if (Main.rand.NextBool(Math.Max(1, 7 - Stacks))) {
                Dust d = Dust.NewDustPerfect(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                    Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2.4f)),
                    100, default, 0.7f + Stacks * 0.06f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 灵魂湮灭爆 — 怨念叠满后的范围爆发，撕裂周身敌人。
    /// </summary>
    public class NetherSoulExplosion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.None;

        private float Radius => MathHelper.Lerp(40f, 150f, 1f - Projectile.timeLeft / 28f);

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 28;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source) {
            for (int i = 0; i < 26; i++) {
                float ang = MathHelper.TwoPi * i / 26f;
                Dust d = Dust.NewDustPerfect(Projectile.Center, i % 2 == 0 ? DustID.BlueTorch : DustID.PurpleTorch,
                    ang.ToRotationVector2() * Main.rand.NextFloat(5f, 11f), 60, default, 1.8f);
                d.noGravity = true;
            }
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, NetherFX.Mix(0.5f).ToVector3() * 1.2f);
            if (Main.rand.NextBool()) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                NetherFX.SoulDust(Projectile.Center + ang.ToRotationVector2() * Radius, 1.5f, 1, 1.3f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = Radius;
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2())
                < r + Math.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 240);
            target.AddBuff(BuffID.OnFire, 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float r = Radius;
            float fade = Projectile.timeLeft / 28f;
            // 扩张冲击环
            int seg = 28;
            for (int i = 0; i < seg; i++) {
                float ang = MathHelper.TwoPi * i / seg + Projectile.timeLeft * 0.05f;
                Vector2 p = Projectile.Center + ang.ToRotationVector2() * r;
                NetherFX.DrawGlow(sb, p, NetherFX.Mix((float)i / seg) * fade, 0.5f * fade);
            }
            // 核心闪光
            NetherFX.DrawGlow(sb, Projectile.Center, NetherFX.Cyan * fade, (1f - fade) * 2.4f);
            return false;
        }
    }
}
