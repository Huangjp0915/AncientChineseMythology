using Microsoft.Xna.Framework;
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
    /// 龙炎骸火 —— 超级毕业召唤武器，红色熔岩主题，召唤熔岩龙，追踪敌人并周期性喷发熔岩玄蛋
    /// </summary>
    public class DraconicEmberBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type]        = true;
            Main.buffNoTimeDisplay[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<DraconicEmberProj>()] > 0)
                player.buffTime[buffIndex] = 18000;
        }
    }

    public class DraconicEmber : ModItem
    {
        public override void SetDefaults() {
            Item.damage     = 480;
            Item.DamageType = DamageClass.Summon;
            Item.mana       = 15;
            Item.width      = 60;
            Item.height     = 60;
            Item.useTime      = 35;
            Item.useAnimation = 35;
            Item.useStyle   = ItemUseStyleID.Swing;
            Item.knockBack  = 12;
            Item.value      = Item.buyPrice(gold: 200);
            Item.rare       = ItemRarityID.Purple;
            Item.autoReuse  = false;
            Item.noMelee    = true;
            Item.buffType   = ModContent.BuffType<DraconicEmberBuff>();
            Item.shoot      = ModContent.ProjectileType<DraconicEmberProj>();
            Item.shootSpeed = 8f;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Projectile.NewProjectile(source, player.Center - new Vector2(0, 100), Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class DraconicEmberProj : ModProjectile
    {
        public override void SetStaticDefaults() {
            Main.projPet[Type]                           = true;
            ProjectileID.Sets.MinionSacrificable[Type]   = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }
        public override void SetDefaults() {
            Projectile.width  = 60;
            Projectile.height = 60;
            Projectile.friendly    = true;
            Projectile.minion      = true;
            Projectile.DamageType  = DamageClass.Summon;
            Projectile.minionSlots = 2f;
            Projectile.penetrate   = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft    = Main.maxTilesX;
        }
        private ref float Timer => ref Projectile.ai[0];

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<DraconicEmberBuff>())) {
                Projectile.active = false; return;
            }
            Projectile.timeLeft = 2;
            Timer++;
            bool hit = false; float rng = 1000f; Vector2 tgt = Vector2.Zero;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < rng) { rng = d; tgt = npc.Center; hit = true; }
            }
            if (hit) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.DirectionTo(tgt) * 18f, 0.10f);
                // 每90帧喷发一枚熔岩玄蛋
                if (Timer % 90 == 0 && Projectile.owner == Main.myPlayer) {
                    SoundEngine.PlaySound(SoundID.Item20, Projectile.position);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                        Projectile.DirectionTo(tgt) * 16f,
                        ModContent.ProjectileType<DraconicEmberEggProj>(),
                        Projectile.damage, 0f, Projectile.owner);
                }
            } else {
                Vector2 idle = owner.Center + new Vector2(0f, -150f + (float)Math.Sin(Timer * 0.03f) * 30f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (idle - Projectile.Center) * 0.07f, 0.10f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.spriteDirection = Projectile.velocity.X < 0 ? -1 : 1;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Texture2D sg  = ACMAsset.SoftGlow;
            Texture2D emb = ACMAsset.EmberShards;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            float pulse = 0.42f + 0.18f * (float)Math.Sin(Main.timeForVisualEffects * 0.07f);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(255, 80, 10) * pulse, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 2.2f, SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(255, 200, 20) * (pulse * 0.55f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 1.0f, SpriteEffects.None, 0);
            sb.Draw(emb, Projectile.Center - Main.screenPosition, null, new Color(255, 100, 0) * (pulse * 0.70f),
                (float)Main.timeForVisualEffects * 0.04f,
                new Vector2(emb.Width * 0.5f, emb.Height * 0.5f), 0.90f, SpriteEffects.None, 0);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            SpriteEffects se = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, 0f,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), 1.0f, se, 0);
            return false;
        }
    }

    public class DraconicEmberEggProj : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }
        public override void SetDefaults() {
            Projectile.width  = 22;
            Projectile.height = 22;
            Projectile.friendly    = true;
            Projectile.tileCollide = true;
            Projectile.penetrate   = 3;
            Projectile.timeLeft    = 150;
            Projectile.DamageType  = DamageClass.Summon;
            Projectile.light       = 0.8f;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 8;
        }
        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity.Y += 0.08f;
        }
        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<GeocrystalBurst>(), 0, 0f, Projectile.owner);
        }
        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Texture2D sg  = ACMAsset.SoftGlow;
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.65f;
                sb.Draw(sg, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    new Color(255, 80, 10) * a, 0f,
                    new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 0.45f + i * 0.025f, SpriteEffects.None, 0);
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), 1.0f, SpriteEffects.None, 0);
            return false;
        }
    }
}
