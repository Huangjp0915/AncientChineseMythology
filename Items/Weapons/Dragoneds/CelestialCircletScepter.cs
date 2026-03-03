using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 天枢耀能环流杖 —— 超级毕业法杖，释放5枚以鼠标为中心高速旋转的天枢耀能光环，
    /// 光环逐渐螺旋收束后向最近目标冲刺贯穿
    /// </summary>
    public class CelestialCircletScepter : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 480;
            Item.DamageType = DamageClass.Magic;
            Item.width  = 50;
            Item.height = 50;
            Item.useTime      = 32;
            Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8;
            Item.crit  = 25;
            Item.mana  = 28;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare  = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.noMelee      = true;
            Item.shoot = ModContent.ProjectileType<CelestialCircletOrb>();
            Item.shootSpeed = 16f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item8, player.position);
            // 5枚光环以60°间隔均匀扇出
            int count = 5;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = velocity.RotatedBy(angle - MathHelper.TwoPi * (count / 2) / count * 0.35f);
                Projectile.NewProjectile(source, position, vel, type, damage, knockback,
                    player.whoAmI, ai0: angle, ai1: i);
            }
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 耀能光环弹幕：初期绕鼠标点旋转，随后归向追踪
    // ──────────────────────────────────────────────────────────────
    public class CelestialCircletOrb : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/BlankStar";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        private bool _homing = false;
        private float _orbitTimer = 0f;
        private const float ORBIT_DURATION = 90f; // 1.5秒 绕行后改为追踪

        public override void SetDefaults() {
            Projectile.width  = 32;
            Projectile.height = 32;
            Projectile.friendly    = true;
            Projectile.tileCollide = false;
            Projectile.penetrate   = 5;
            Projectile.timeLeft    = 280;
            Projectile.DamageType  = DamageClass.Magic;
            Projectile.light       = 1.0f;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 10;
        }

        public override void AI() {
            Player p = Main.player[Projectile.owner];
            _orbitTimer++;

            if (!_homing && _orbitTimer < ORBIT_DURATION) {
                // 围绕鼠标/玩家前方位置旋转
                Vector2 center = p.Center + p.DirectionTo(Main.MouseWorld) * 160f;
                float baseAngle = Projectile.ai[0]; // 初始相位
                float orbitRadius = MathHelper.Lerp(180f, 60f, _orbitTimer / ORBIT_DURATION);
                float orbitSpeed  = MathHelper.Lerp(0.08f, 0.18f, _orbitTimer / ORBIT_DURATION);
                float angle = baseAngle + _orbitTimer * orbitSpeed;
                Vector2 target = center + new Vector2(orbitRadius, 0).RotatedBy(angle);
                Projectile.velocity = (target - Projectile.Center) * 0.18f;
            }
            else {
                // 追踪最近敌人
                _homing = true;
                float closestDist = 900f;
                int targetNPC = -1;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) { closestDist = dist; targetNPC = i; }
                }
                if (targetNPC >= 0) {
                    Vector2 dir = Projectile.DirectionTo(Main.npc[targetNPC].Center);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 28f, 0.14f);
                }
            }

            Projectile.rotation += 0.22f;

            // 弧光粒子
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                    DustID.BlueFairy,
                    Main.rand.NextVector2Circular(3, 3), 0,
                    new Color(100, 210, 255), Main.rand.NextFloat(0.8f, 2.0f));
                d.noGravity = true;
            }
            if (Main.rand.NextBool(5)) {
                Dust ds = Dust.NewDustPerfect(Projectile.Center,
                    DustID.Flare_Blue,
                    Main.rand.NextVector2Circular(6, 6), 0,
                    new Color(240, 255, 120), 1.8f);
                ds.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Confused, 120);
            target.AddBuff(BuffID.Electrified, 90);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(target.Center,
                    DustID.BlueFairy,
                    Main.rand.NextVector2CircularEdge(6, 6), 0,
                    new Color(120, 200, 255), 2.0f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D star = ACMAsset.BlankStar;
            Texture2D sg   = ACMAsset.SoftGlow;
            Texture2D arc  = ACMAsset.ElectricArcSheet;

            // 电弧光环背景（ElectricArcSheet 取第0行）
            Rectangle arcFrame = new Rectangle(0, 0, arc.Width, arc.Height / 4);
            Color arcCol = new Color(80, 180, 255) { A = 0 };
            sb.Draw(arc, Projectile.Center - Main.screenPosition, arcFrame,
                arcCol * 0.5f, Projectile.rotation * 0.5f,
                new Vector2(arcFrame.Width * 0.5f, arcFrame.Height * 0.5f),
                0.4f, SpriteEffects.None, 0);

            // 拖尾
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.55f;
                Color tc = new Color(100, 210, 255) { A = 0 };
                sb.Draw(sg,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, tc * a, 0f,
                    new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                    0.35f, SpriteEffects.None, 0);
            }

            // BlankStar 主星（A=0 使颜色直接叠加）
            Color starCol = new Color(160, 230, 255) { A = 0 };
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                starCol * 0.9f, Projectile.rotation,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                0.65f, SpriteEffects.None, 0);

            // SoftGlow 核心
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(200, 240, 255, 0) * 0.8f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.28f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

