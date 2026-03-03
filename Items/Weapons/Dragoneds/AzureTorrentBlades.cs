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
    /// 蔚蓝激流剑群 —— 超级毕业召唤武器，召唤蓝色剑群，3帧动画，悬浮旋转后俯冲追踪敌人
    /// </summary>
    public class AzureTorrentBladesBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type]        = true;
            Main.buffNoTimeDisplay[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<AzureTorrentBladesProj>()] > 0)
                player.buffTime[buffIndex] = 18000;
        }
    }

    public class AzureTorrentBlades : ModItem
    {
        public override void SetDefaults() {
            Item.damage     = 440;
            Item.DamageType = DamageClass.Summon;
            Item.mana       = 8;
            Item.width      = 50;
            Item.height     = 50;
            Item.useTime      = 25;
            Item.useAnimation = 25;
            Item.useStyle   = ItemUseStyleID.Swing;
            Item.knockBack  = 6;
            Item.value      = Item.buyPrice(gold: 200);
            Item.rare       = ItemRarityID.Purple;
            Item.autoReuse  = false;
            Item.noMelee    = true;
            Item.buffType   = ModContent.BuffType<AzureTorrentBladesBuff>();
            Item.shoot      = ModContent.ProjectileType<AzureTorrentBladesProj>();
            Item.shootSpeed = 10f;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class AzureTorrentBladesProj : ModProjectile
    {
        // 帧0=剑鞘(待机) 帧1=剑刃(接近) 帧2=剑刃闪光(冲刺中)
        private static readonly int TOTAL_FRAMES = 3;

        public override void SetStaticDefaults() {
            Main.projFrames[Type]                        = TOTAL_FRAMES;
            Main.projPet[Type]                           = true;
            ProjectileID.Sets.MinionSacrificable[Type]   = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }
        public override void SetDefaults() {
            Projectile.width  = 40;
            Projectile.height = 40;
            Projectile.friendly    = true;
            Projectile.minion      = true;
            Projectile.DamageType  = DamageClass.Summon;
            Projectile.minionSlots = 0.5f;
            Projectile.penetrate   = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft    = Main.maxTilesX;
        }
        private ref float Timer => ref Projectile.ai[0];

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<AzureTorrentBladesBuff>())) {
                Projectile.active = false; return;
            }
            Projectile.timeLeft = 2;
            Timer++;
            // 寻找目标
            bool hit = false; float rng = 800f; Vector2 tgt = Vector2.Zero;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < rng) { rng = d; tgt = npc.Center; hit = true; }
            }
            if (hit) {
                // 帧1-2: 冲刺旋转
                Projectile.frame = rng < 80f ? 2 : 1;
                Vector2 dir = Projectile.DirectionTo(tgt);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 20f, 0.14f);
                Projectile.rotation += 0.25f;
            } else {
                // 帧0: 悬浮待机
                Projectile.frame = 0;
                Vector2 idle = owner.Center + new Vector2(-40f * owner.direction, -90f - (float)Math.Sin(Timer * 0.05f) * 20f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (idle - Projectile.Center) * 0.08f, 0.12f);
                Projectile.rotation = (float)Math.Sin(Timer * 0.05f) * 0.25f;
            }
            Projectile.spriteDirection = Projectile.velocity.X < 0 ? -1 : 1;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D sg  = ACMAsset.SoftGlow;
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            int fh = tex.Height / TOTAL_FRAMES;
            Rectangle src = new Rectangle(0, Projectile.frame * fh, tex.Width, fh);

            // 速度越快，光辉越亮
            float speed = Projectile.velocity.Length();
            float glowA  = MathHelper.Clamp(speed / 20f, 0.15f, 0.90f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(30, 140, 255) * glowA, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 1.4f, SpriteEffects.None, 0);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            SpriteEffects se = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, src, lightColor, Projectile.rotation,
                new Vector2(tex.Width * 0.5f, fh * 0.5f), 1.0f, se, 0);
            return false;
        }
    }
}
