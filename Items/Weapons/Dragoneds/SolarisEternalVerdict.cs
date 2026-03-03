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
    /// 太阳不灭裁决 —— 超级毕业召唤武器，十字闪耀眼球，悬浮于玩家上方，每60帧向最近目标发射4道太阳射线
    /// </summary>
    public class SolarisEternalVerdictBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<SolarisEternalVerdictProj>()] > 0)
                player.buffTime[buffIndex] = 18000;
        }
    }

    public class SolarisEternalVerdict : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 500;
            Item.DamageType = DamageClass.Summon;
            Item.mana = 12;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 35;
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 10;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.buffType = ModContent.BuffType<SolarisEternalVerdictBuff>();
            Item.shoot = ModContent.ProjectileType<SolarisEternalVerdictProj>();
            Item.shootSpeed = 8f;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Projectile.NewProjectile(source, player.Center - new Vector2(0, 180), Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class SolarisEternalVerdictProj : ModProjectile
    {
        public override void SetStaticDefaults() {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }
        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1.5f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Main.maxTilesX;
        }
        private ref float Timer => ref Projectile.ai[0];

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<SolarisEternalVerdictBuff>())) {
                Projectile.active = false; return;
            }
            Projectile.timeLeft = 2;
            Timer++;
            // 悬浮于玩家头部约180px处
            Vector2 hover = owner.Center + new Vector2(
                (float)Math.Sin(Timer * 0.035f) * 40f,
                -180f - (float)Math.Sin(Timer * 0.05f) * 15f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (hover - Projectile.Center) * 0.08f, 0.12f);
            Projectile.rotation += 0.03f;

            // 寻找目标，每60帧朝目标发射4道射线
            if (Timer % 60 == 0 && Projectile.owner == Main.myPlayer) {
                bool hit = false; float rng = 1000f; Vector2 tgt = Vector2.Zero;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy()) continue;
                    float d = Vector2.Distance(Projectile.Center, npc.Center);
                    if (d < rng) { rng = d; tgt = npc.Center; hit = true; }
                }
                if (hit) {
                    SoundEngine.PlaySound(SoundID.Item12, Projectile.position);
                    float baseAng = Projectile.DirectionTo(tgt).ToRotation();
                    float spread = MathHelper.PiOver4 * 0.38f; // 每侧22.5°
                    for (int k = -1; k <= 1; k += 2) {
                        for (int j = 0; j < 2; j++) {
                            float ang = baseAng + k * spread * j;
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                                ang.ToRotationVector2() * 28f,
                                ModContent.ProjectileType<SolarisRay>(), Projectile.damage, 0f, Projectile.owner);
                        }
                    }
                    // 正前方主射线
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                        Projectile.DirectionTo(tgt) * 32f,
                        ModContent.ProjectileType<SolarisRay>(), (int)(Projectile.damage * 1.3f), 0f, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D star = ACMAsset.BlankStar;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            float pulse = 0.55f + 0.25f * (float)Math.Sin(Main.timeForVisualEffects * 0.07f);
            // 外层太阳光晕
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(255, 220, 50) * (pulse * 0.70f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 2.0f, SpriteEffects.None, 0);
            // 内层白核
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(255, 255, 200) * (pulse * 0.90f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 0.85f, SpriteEffects.None, 0);
            // 旋转十字星
            sb.Draw(star, Projectile.Center - Main.screenPosition, null, new Color(255, 240, 80) * (pulse * 0.80f),
                Projectile.rotation * 1.5f,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f), 0.75f, SpriteEffects.None, 0);
            sb.Draw(star, Projectile.Center - Main.screenPosition, null, new Color(255, 180, 20) * (pulse * 0.55f),
                Projectile.rotation * 1.5f + MathHelper.PiOver4,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f), 0.55f, SpriteEffects.None, 0);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), 1.0f, SpriteEffects.None, 0);
            return false;
        }
    }

    public class SolarisRay : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 120;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.light = 1.2f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
        }
        public override void AI() => Projectile.rotation = Projectile.velocity.ToRotation();
        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Texture2D tex = ACMAsset.LightShot;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D star = ACMAsset.BlankStar;
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.78f;
                sb.Draw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    new Color(255, 210, 30) * a, Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.55f + i * 0.015f, 0.18f), SpriteEffects.None, 0);
                sb.Draw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    new Color(255, 255, 160) * (a * 0.38f), Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.24f, 0.08f), SpriteEffects.None, 0);
            }
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, new Color(255, 240, 60),
                Projectile.rotation, new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(0.95f, 0.22f), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(255, 220, 50) * 0.85f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 0.55f, SpriteEffects.None, 0);
            // 弹头脆弱脑大抗星芙脑呵呵心脱自简单单
            float headPulse = 0.55f + 0.45f * (float)Math.Sin(Main.timeForVisualEffects * 0.18f);
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                new Color(255, 240, 80) * (0.85f * headPulse),
                (float)Main.timeForVisualEffects * 0.06f,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                0.28f + 0.08f * headPulse, SpriteEffects.None, 0);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
