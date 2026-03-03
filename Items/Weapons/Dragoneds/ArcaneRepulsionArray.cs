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
    /// 奥能排斥阵列 —— 超级毕业召唤武器，金蓝龙鳞无人机，巡逻时绕玩家飞行，遇敌发射穿透金蓝激光
    /// </summary>
    public class ArcaneRepulsionArrayBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<ArcaneRepulsionArrayProj>()] > 0)
                player.buffTime[buffIndex] = 18000;
        }
    }

    public class ArcaneRepulsionArray : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 460;
            Item.DamageType = DamageClass.Summon;
            Item.mana = 10;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.buffType = ModContent.BuffType<ArcaneRepulsionArrayBuff>();
            Item.shoot = ModContent.ProjectileType<ArcaneRepulsionArrayProj>();
            Item.shootSpeed = 8f;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class ArcaneRepulsionArrayProj : ModProjectile
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
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Main.maxTilesX;
        }
        private ref float Timer => ref Projectile.ai[0];
        private ref float OrbitAng => ref Projectile.ai[1];

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<ArcaneRepulsionArrayBuff>())) {
                Projectile.active = false; return;
            }
            Projectile.timeLeft = 2;
            Timer++;
            // 寻找目标
            bool hit = false; float rng = 900f; Vector2 tgt = Vector2.Zero;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < rng) { rng = d; tgt = npc.Center; hit = true; }
            }
            if (hit) {
                OrbitAng += 0.035f;
                Vector2 orbitPos = tgt + new Vector2((float)Math.Cos(OrbitAng) * 220f, (float)Math.Sin(OrbitAng) * 180f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (orbitPos - Projectile.Center) * 0.10f, 0.12f);
                if (Timer % 80 == 0 && Projectile.owner == Main.myPlayer) {
                    SoundEngine.PlaySound(SoundID.Item12, Projectile.position);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                        Projectile.DirectionTo(tgt) * 28f,
                        ModContent.ProjectileType<ArcaneRepulsionLaser>(), Projectile.damage, 0f, Projectile.owner);
                }
            }
            else {
                OrbitAng += 0.028f;
                Vector2 idle = owner.Center + new Vector2((float)Math.Cos(OrbitAng) * 180f, -80f + (float)Math.Sin(OrbitAng) * 60f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (idle - Projectile.Center) * 0.08f, 0.12f);
            }
            Projectile.rotation += 0.05f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Texture2D sg = ACMAsset.SoftGlow;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            float pulse = 0.45f + 0.15f * (float)Math.Sin(Main.timeForVisualEffects * 0.06f);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(255, 200, 30) * pulse, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 1.8f, SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(30, 140, 255) * (pulse * 0.55f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 1.0f, SpriteEffects.None, 0);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), 1.0f, SpriteEffects.None, 0);
            return false;
        }
    }

    public class ArcaneRepulsionLaser : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }
        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 8;
            Projectile.timeLeft = 100;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.light = 1.0f;
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
            Texture2D arc = ACMAsset.ElectricArcSheet;
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.72f;
                // 绿金势能光束拖影
                sb.Draw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    new Color(160, 245, 60) * a, Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.55f + i * 0.014f, 0.17f), SpriteEffects.None, 0);
                sb.Draw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    new Color(255, 210, 40) * (a * 0.45f), Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.25f, 0.09f), SpriteEffects.None, 0);
            }
            // 弹头主体
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, new Color(190, 255, 80),
                Projectile.rotation, new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(0.95f, 0.22f), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(140, 255, 60) * 0.80f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 0.50f, SpriteEffects.None, 0);
            // ElectricArcSheet超荷电弧弹头转动
            int arcFrame = (int)(Main.timeForVisualEffects / 3) % 4;
            Rectangle arcSrc = new Rectangle(0, arcFrame * (arc.Height / 4), arc.Width, arc.Height / 4);
            sb.Draw(arc, Projectile.Center - Main.screenPosition, arcSrc,
                new Color(160, 255, 80) * 0.65f,
                Projectile.rotation,
                new Vector2(arc.Width * 0.5f, (arc.Height / 4) * 0.5f),
                0.28f, SpriteEffects.None, 0);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
