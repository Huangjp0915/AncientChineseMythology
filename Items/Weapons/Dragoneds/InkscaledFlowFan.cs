using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 墨鳞流扇 —— 超级毕业召唤武器，墨鳞游鱼，5帧循环动画，波浪游动追踪敌人
    /// </summary>
    public class InkscaledFlowFanBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type]        = true;
            Main.buffNoTimeDisplay[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<InkscaledFlowFanProj>()] > 0)
                player.buffTime[buffIndex] = 18000;
        }
    }

    public class InkscaledFlowFan : ModItem
    {
        public override void SetDefaults() {
            Item.damage     = 430;
            Item.DamageType = DamageClass.Summon;
            Item.mana       = 7;
            Item.width      = 50;
            Item.height     = 50;
            Item.useTime      = 22;
            Item.useAnimation = 22;
            Item.useStyle   = ItemUseStyleID.Swing;
            Item.knockBack  = 5;
            Item.value      = Item.buyPrice(gold: 200);
            Item.rare       = ItemRarityID.Purple;
            Item.autoReuse  = false;
            Item.noMelee    = true;
            Item.buffType   = ModContent.BuffType<InkscaledFlowFanBuff>();
            Item.shoot      = ModContent.ProjectileType<InkscaledFlowFanProj>();
            Item.shootSpeed = 8f;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class InkscaledFlowFanProj : ModProjectile
    {
        private static readonly int TOTAL_FRAMES = 5;

        public override void SetStaticDefaults() {
            Main.projFrames[Type]                        = TOTAL_FRAMES;
            Main.projPet[Type]                           = true;
            ProjectileID.Sets.MinionSacrificable[Type]   = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }
        public override void SetDefaults() {
            Projectile.width  = 36;
            Projectile.height = 36;
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
            if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<InkscaledFlowFanBuff>())) {
                Projectile.active = false; return;
            }
            Projectile.timeLeft = 2;
            Timer++;
            // 5 帧循环
            if (Timer % 6 == 0)
                Projectile.frame = (Projectile.frame + 1) % TOTAL_FRAMES;

            bool hit = false; float rng = 750f; Vector2 tgt = Vector2.Zero;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < rng) { rng = d; tgt = npc.Center; hit = true; }
            }

            Vector2 baseVel;
            if (hit) {
                baseVel = Projectile.DirectionTo(tgt) * 17f;
            } else {
                // 绕玩家波浪游动
                float wave = (float)Math.Sin(Timer * 0.06f) * 60f;
                Vector2 idle = owner.Center + new Vector2(-50f * owner.direction, -60f + wave);
                baseVel = (idle - Projectile.Center) * 0.08f;
            }
            // 叠加侧向波浪扰动（游鱼姿态）
            Vector2 perp = new Vector2(-baseVel.Y, baseVel.X).SafeNormalize(Vector2.Zero);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                baseVel + perp * (float)Math.Sin(Timer * 0.12f) * 2.0f, 0.13f);

            if (Projectile.velocity.Length() > 0.2f)
                Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.spriteDirection = Projectile.velocity.X < 0 ? -1 : 1;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Texture2D sg  = ACMAsset.SoftGlow;
            int fh = tex.Height / TOTAL_FRAMES;
            Rectangle src = new Rectangle(0, Projectile.frame * fh, tex.Width, fh);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            float pulse = 0.28f + 0.12f * (float)Math.Sin(Main.timeForVisualEffects * 0.08f);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, new Color(180, 210, 240) * pulse, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f), 1.1f, SpriteEffects.None, 0);
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
