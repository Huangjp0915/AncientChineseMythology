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
    /// 天律法典 - 天柱敌怪掉落的魔法书类武器。
    /// 机制身份: 律令锁敌 — 律令符命中给敌人烙"天条锁"(受天罚雷伤害+25%);
    /// 每四次施法在鼠标处展开天律审判页, 对页内所有被锁敌人各落一道天罚落雷。
    /// 决策点: 先锁多个目标, 再把审判页盖在人堆上收割。
    /// </summary>
    public class TomeofDivineLaw : ModItem
    {
        private int castCount; // 施法计数 (Shoot 仅 owner 端调用, 实例字段安全)

        public override void SetDefaults() {
            Item.damage = 160;
            Item.DamageType = DamageClass.Magic;
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item103;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DivineRune>();
            Item.shootSpeed = 15f;
            Item.mana = 12;
            Item.crit = 10;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            castCount++;

            // 普通释放: 律令符 (音高随计数递升 = 可听节拍)
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.15f + castCount * 0.1f, Volume = 0.4f }, position);

            // 每四次施法: 天律审判页
            if (castCount >= 4) {
                castCount = 0;
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f, Volume = 0.8f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 0.6f }, player.Center);

                Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero,
                    ModContent.ProjectileType<RuneCircle>(), damage, knockback, player.whoAmI);

                for (int i = 0; i < 20; i++) {
                    float angle = MathHelper.TwoPi * i / 20;
                    Vector2 vel = angle.ToRotationVector2() * 5f;
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                    int dust = Dust.NewDust(Main.MouseWorld, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.4f) * Main.rand.NextFloat(2, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(position, 0, 0, dustType, dustVel.X, dustVel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "记载天界法则的神圣典籍，条条皆是雷霆"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "掷出天律令符，命中烙下天条锁：受天罚落雷伤害提高25%"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect2", "每四次施法展开审判页，对页内所有被锁敌人各落一道天罚落雷"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(10).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 天条锁 - 律令符烙下的锁敌状态 (owner 端闭环)。
    /// 被锁敌人身上升起竖排金色律文光丝, 受天罚落雷伤害 +25% (见 HeavenJudgmentBolt.ModifyHitNPC)。
    /// </summary>
    public class DivineLawGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>剩余锁定帧数。</summary>
        public int LockTimer;
        public bool Locked => LockTimer > 0;

        /// <summary>烙天条锁 (仅 owner 端调用生效)。</summary>
        public static void ApplyLock(NPC target, Projectile source, int duration = 240) {
            if (Main.myPlayer != source.owner)
                return;
            target.GetGlobalNPC<DivineLawGlobalNPC>().LockTimer = duration;
        }

        public override void PostAI(NPC npc) {
            if (LockTimer > 0)
                LockTimer--;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!Locked || Main.dedServ)
                return;

            // 竖排律文光丝 ×3 (细 BeamGrad 线, 随时间轻微摆动) — "天条锁身"的可读标记
            float sway = MathF.Sin((float)Main.GlobalTimeWrappedHourly * 3.2f + npc.whoAmI) * 4f;
            float fade = MathHelper.Clamp(LockTimer / 40f, 0f, 1f); // 最后 40 帧淡出
            for (int i = -1; i <= 1; i++) {
                Vector2 basePos = npc.Top + new Vector2(i * 14f + sway * i, 4f);
                ACMShaders.DrawBeam(basePos - new Vector2(0f, 56f + 12f * MathF.Abs(i)), basePos, 1.8f,
                    PillarPalette.Gold, PillarPalette.SkyCyan, 0.45f * fade, flowSpeed: 1.8f, coreSharp: 2.6f);
            }

            // 律文金尘沿丝上浮
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(npc.Top + new Vector2(Main.rand.NextFloat(-14f, 14f), 0f),
                    DustID.GoldCoin, new Vector2(0f, -1.2f), 140, default, 0.9f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 天律令符 - 直线快弹 + 轻追踪, 命中烙天条锁
    /// </summary>
    public class DivineRune : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BookStaffShot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation += 0.12f;

            // 轻追踪 (8 帧重锁, 目标缓存 localAI[0])
            if (++Projectile.localAI[1] % 8 == 0 || Projectile.localAI[0] == 0f)
                Projectile.localAI[0] = 1f + (FindClosestNPC(500f)?.whoAmI ?? -2);

            int targetId = (int)Projectile.localAI[0] - 1;
            if (targetId >= 0 && targetId < Main.npc.Length && Main.npc[targetId].active && Main.npc[targetId].CanBeChasedBy()) {
                Vector2 toTarget = (Main.npc[targetId].Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 15f, 0.05f);
            }

            int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
            int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.4f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = -Projectile.velocity * 0.08f;

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * 0.5f);
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
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 0.8f, Projectile.owner);

            // 烙天条锁 (锁身音: 短促金石声)
            DivineLawGlobalNPC.ApplyLock(target, Projectile);
            SoundEngine.PlaySound(SoundID.Item101 with { Pitch = 0.5f, Volume = 0.35f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: new Color(150, 220, 235, 120), innerColor: new Color(255, 250, 210, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle(3, 8);
            Vector2 origin = rectangle.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, rectangle, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Color glowColor = Color.Gold * 0.4f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 天律审判页 - 鼠标处展开的天书法阵。展开瞬间对页内所有被天条锁锁定的敌人各落一道天罚落雷。
    /// </summary>
    public class RuneCircle : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LunarFlare;

        private float circleRadius = 0f;
        private float circleAlpha = 0f;
        private const float MaxRadius = 130f;
        private const float JudgeRadius = 260f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 240;
            Projectile.height = 240;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 25;
        }

        public override void AI() {
            Projectile.rotation += 0.08f;

            // 展开第 3 帧: 审判 — 对页内所有被锁敌人各落一道天罚 (owner 端生成, Strike 内部判 owner)
            if (Projectile.timeLeft == 57 && Projectile.owner == Main.myPlayer) {
                int judged = 0;
                foreach (NPC npc in Main.npc) {
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || judged >= 6) continue;
                    if (Vector2.Distance(npc.Center, Projectile.Center) > JudgeRadius) continue;
                    if (!npc.TryGetGlobalNPC(out DivineLawGlobalNPC law) || !law.Locked) continue;
                    judged++;
                    HeavenJudgmentBolt.Strike(Projectile.GetSource_FromThis(), npc.Center,
                        (int)(Projectile.damage * 1.2f), 3f, Projectile.owner, 0.95f);
                }
                if (judged > 0)
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 0.8f }, Projectile.Center);
            }

            // 扩张 / 收拢
            if (Projectile.timeLeft > 30) {
                circleRadius = MathHelper.Lerp(circleRadius, MaxRadius, 0.12f);
                circleAlpha = MathHelper.Lerp(circleAlpha, 1f, 0.12f);
            }
            else {
                circleAlpha = Projectile.timeLeft / 30f;
            }

            // 阵缘游走粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * circleRadius;
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 3f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.5f) * circleAlpha * 0.8f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            return Vector2.Distance(Projectile.Center, targetCenter) < circleRadius + 30f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 天书法阵地纹 (共享 ArenaRunic, 金律文配色, 不占全屏名额)
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null && circleAlpha > 0.03f) {
                ACMShaders.WorldDecalParams(Projectile.Center, circleRadius * 1.55f,
                    out Vector2 uvCenter, out float radiusFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uvCenter);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(0.75f * circleAlpha);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(PillarPalette.Gold.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(PillarPalette.SkyCyan.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(10f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            // 内层旋转律文 (两层反向)
            int runeCount = 6;
            for (int layer = 0; layer < 2; layer++) {
                float layerRadius = circleRadius * (0.45f + layer * 0.3f);
                float layerRot = Projectile.rotation * (1f + layer * 0.3f) * (layer % 2 == 0 ? 1 : -1);

                Color layerColor = layer == 0 ? PillarPalette.Gold : PillarPalette.SkyCyan;
                layerColor *= circleAlpha * (0.55f - layer * 0.15f);
                layerColor.A = 0;

                for (int i = 0; i < runeCount; i++) {
                    float angle = layerRot + MathHelper.TwoPi * i / runeCount;
                    Vector2 runePos = screenPos + angle.ToRotationVector2() * layerRadius;
                    Main.spriteBatch.Draw(tex, runePos, null, layerColor, angle + Projectile.rotation, origin, 0.4f - layer * 0.1f, SpriteEffects.None, 0f);
                }
            }

            // 中心光芒
            Color centerColor = PillarPalette.HolyWhite * circleAlpha * 0.5f;
            centerColor.A = 0;
            Main.spriteBatch.Draw(tex, screenPos, null, centerColor, Projectile.rotation * 2f, origin, 0.8f * circleAlpha, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 6f;
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
