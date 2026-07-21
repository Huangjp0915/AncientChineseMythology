using AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    /// <summary>
    /// 监察者权杖 - 天柱敌怪掉落的法杖类魔法武器。
    /// 机制身份: 监天印 — 光球命中烙下监天印, 同一目标叠满三印即引落天罚落雷。
    /// 决策点: 集火叠印处决 vs 分散压制。
    /// </summary>
    public class ScepterofTheOverseer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 190;
            Item.DamageType = DamageClass.Magic;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<OverseerOrb>();
            Item.shootSpeed = 11f;
            Item.mana = 14;
            Item.crit = 8;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 单发监天光球 (追踪增强, 必中感; 伤害全额不再摊到双球)
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 7);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(position, 0, 0, dustType, dustVel.X, dustVel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "监视天界的神圣权杖，所见即所判"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "发射追踪天光球，命中烙下监天印"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect2", "同一目标叠满三层监天印，将引落一道天罚落雷"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(10).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 监天印 - 权杖光球烙下的判罚印记 (owner 端闭环: owner 命中累计, owner 端引爆生成同步弹幕)。
    /// 头顶金环点数 = 层数; 叠满三层清空并引落天罚落雷。
    /// </summary>
    public class OverseerMarkGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>当前印记层数 (0~2, 叠满即刻引爆清零)。</summary>
        public int Stacks;
        /// <summary>印记剩余帧数 (归零清层)。</summary>
        public int Timer;

        /// <summary>叠一层监天印; 叠满三层清空并引落天罚落雷 (仅 owner 端调用生效)。</summary>
        public static void AddMark(NPC target, Projectile source) {
            if (Main.myPlayer != source.owner)
                return;
            var mark = target.GetGlobalNPC<OverseerMarkGlobalNPC>();
            mark.Timer = 300;
            mark.Stacks++;
            if (mark.Stacks >= 3) {
                mark.Stacks = 0;
                // 审判确认音 + 1.5× 天罚
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 0.9f }, target.Center);
                HeavenJudgmentBolt.Strike(source.GetSource_FromThis(), target.Center,
                    (int)(source.damage * 1.5f), 4f, source.owner, 1.1f);
            }
        }

        public override void PostAI(NPC npc) {
            if (Timer > 0) {
                Timer--;
                if (Timer == 0)
                    Stacks = 0;
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Stacks <= 0 || Main.dedServ)
                return;

            Texture2D star = ACMAsset.BlankStar;
            if (star == null)
                return;

            // 头顶旋转金环点阵: 点数 = 层数 (可读的判罚进度)
            Vector2 anchor = npc.Top + new Vector2(0f, -22f);
            float baseRot = (float)Main.GlobalTimeWrappedHourly * 2.2f;
            for (int i = 0; i < Stacks; i++) {
                float ang = baseRot + MathHelper.TwoPi * i / MathF.Max(Stacks, 1);
                Vector2 pos = anchor + ang.ToRotationVector2() * 14f;
                Color c = PillarPalette.Gold * (0.55f + 0.25f * Stacks);
                c.A = 0;
                spriteBatch.Draw(star, pos - screenPos, null, c, ang, star.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
            }

            // 两层以上: 头顶垂下细天光丝 (预告天罚将至)
            if (Stacks >= 2) {
                ACMShaders.DrawBeam(anchor - new Vector2(0f, 260f), anchor + new Vector2(0f, 10f), 2.2f,
                    PillarPalette.HolyWhite, PillarPalette.SkyCyan, 0.35f + 0.15f * MathF.Sin((float)Main.GlobalTimeWrappedHourly * 6f),
                    flowSpeed: 2.8f, coreSharp: 3f);
            }
        }
    }

    /// <summary>
    /// 监天光球 - 强追踪, 命中爆炸并烙监天印
    /// </summary>
    public class OverseerOrb : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LunarFlare;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 100;
        }

        public override void AI() {
            Projectile.rotation += 0.15f;

            // 追踪 (8 帧重锁, 目标缓存 localAI[0]; 转向比旧版更硬 → "被盯上就跑不掉"的监视感)
            if (++Projectile.localAI[1] % 8 == 0 || Projectile.localAI[0] == 0f)
                Projectile.localAI[0] = 1f + (FindClosestNPC(620f)?.whoAmI ?? -2);

            int targetId = (int)Projectile.localAI[0] - 1;
            if (targetId >= 0 && targetId < Main.npc.Length && Main.npc[targetId].active && Main.npc[targetId].CanBeChasedBy()) {
                Vector2 toTarget = (Main.npc[targetId].Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 15f, 0.09f);
            }

            for (int i = 0; i < 2; i++) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center + Main.rand.NextVector2Circular(8, 8), 0, 0, dustType, 0, 0, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * 0.6f);
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;
            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 0.7f }, Projectile.Center);

            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2.2f);
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.IceTorch, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1.2f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 2f);

            // 烙监天印 (三层引落天罚)
            OverseerMarkGlobalNPC.AddMark(target, Projectile);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle(2, 5);
            Vector2 origin = rectangle.Size() / 2f;

            // 金色拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, rectangle, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color outerGlow = Color.Gold * 0.4f;
            outerGlow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, outerGlow, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0f);

            // 中层青色 (反向旋转 = 监视之眼)
            Color midGlow = new Color(100, 200, 180) * 0.5f;
            midGlow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, midGlow, -Projectile.rotation, origin, Projectile.scale * 1.1f, SpriteEffects.None, 0f);

            // 核心
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, origin, Projectile.scale * 0.8f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
