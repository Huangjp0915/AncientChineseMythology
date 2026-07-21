using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 判官笔 - 地府判官用于判定生死的神笔，魔法武器。
    /// 重做"朱批定罪"：笔画弹命中施加「朱批」层数（敌人头顶朱红批痕可见），
    /// 满 4 层触发定罪 —— 天降朱红判决竖笔劈落，造成 2 倍伤害并清层。
    /// 集火定罪 vs 散射铺场的主动决策点。
    /// </summary>
    public class BrushofJudgment : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 36; //基伤下调换定罪爆发 (DPS 论证见 Docs/WeaponRedo/Umbrals.md §6)
            Item.crit = 4;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8;
            Item.width = 36;
            Item.height = 36;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<JudgmentRuneBolt>();
            Item.shootSpeed = 12f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //发射3发笔画弹 (散射角收敛至 9°, 中距更易全中同一目标)
            int numberProjectiles = 3;
            for (int i = 0; i < numberProjectiles; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(9));
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //从笔尖位置发射
            position = player.Center + velocity.SafeNormalize(Vector2.Zero) * 20f;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 「朱批」标记 - 判官笔命中留下的批痕层数（每敌一份实例）。
    /// 层数逻辑在 owner 端与服务器由 OnHitNPC 一致推进；批痕视觉绘制在本地。
    /// </summary>
    public class JudgmentInkMarkNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public const int MaxMarks = 4;
        private const int DecayTime = 360; //6 秒不续标开始逐层褪色

        public int marks;
        public int decayTimer;
        /// <summary>最近一次上标帧（批痕落笔弹跳动画用）。</summary>
        public int lastMarkStamp;

        public override void PostAI(NPC npc) {
            if (marks <= 0)
                return;
            if (++decayTimer >= DecayTime) {
                marks--;
                decayTimer = 0;
            }
        }

        /// <summary>
        /// 追加一层朱批；满层清零并由 owner 客户端降下判决竖笔。
        /// </summary>
        public static void AddMark(NPC target, Projectile source) {
            if (target == null || !target.active || target.friendly)
                return;
            var g = target.GetGlobalNPC<JudgmentInkMarkNPC>();
            g.decayTimer = 0;
            g.marks++;
            g.lastMarkStamp = (int)Main.GameUpdateCount;

            if (g.marks >= MaxMarks) {
                g.marks = 0;
                if (source.owner == Main.myPlayer) {
                    //定罪：判决竖笔劈落 (2x 伤害, 锚在敌人当前位置)
                    Projectile.NewProjectile(source.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<JudgmentVerdictStroke>(), source.damage * 2, source.knockBack * 2f,
                        source.owner, target.whoAmI);
                }
            }
            else {
                //落笔顿点音 (音高随层数上行 — 定罪临近可听)
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.45f, Pitch = 0.1f + g.marks * 0.12f }, target.Center);
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || marks <= 0)
                return;

            Texture2D star = ACMAsset.BlankStar;
            if (star == null)
                return;

            //落笔弹跳 (新标 10 帧内下压回弹)
            float stampAge = (int)Main.GameUpdateCount - lastMarkStamp;
            float bounce = stampAge < 10f ? (1f - stampAge / 10f) * 4f : 0f;
            float fade = marks > 0 && decayTimer > DecayTime - 60 ? 1f - (decayTimer - (DecayTime - 60)) / 60f : 1f;

            Vector2 basePos = npc.Top - screenPos + new Vector2(0f, -18f + bounce);
            float spacing = 14f;
            float startX = -(marks - 1) * spacing * 0.5f;
            for (int i = 0; i < marks; i++) {
                Vector2 pos = basePos + new Vector2(startX + i * spacing, 0f);
                float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + i * 1.3f) * 0.06f;
                //朱砂批痕 (加法: A=0)
                spriteBatch.Draw(star, pos, null, new Color(235, 60, 45, 0) * (0.9f * fade),
                    MathHelper.PiOver4 + wobble, star.Size() * 0.5f, 0.13f, SpriteEffects.None, 0f);
                spriteBatch.Draw(star, pos, null, new Color(255, 160, 120, 0) * (0.5f * fade),
                    MathHelper.PiOver4 + wobble, star.Size() * 0.5f, 0.07f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 朱批笔画弹 - 自绘程序化朱红符文追踪弹；命中施加朱批层数。
    /// </summary>
    public class JudgmentRuneBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.light = 0.4f;
        }

        public override void AI() {
            //符印自转
            Projectile.rotation += 0.18f;

            //温和追踪最近敌人
            NPC target = FindTarget(620f);
            if (target != null) {
                float speed = Projectile.velocity.Length();
                if (speed < 1f)
                    speed = 9f;
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 dir = Vector2.Lerp(Projectile.velocity.SafeNormalize(Vector2.UnitX), toTarget, 0.07f);
                Projectile.velocity = dir.SafeNormalize(Vector2.UnitX) * speed;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.1f, 0.08f);
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                    Main.rand.NextVector2Circular(1.2f, 1.2f), 120, default, 0.9f);
                d.noGravity = true;
            }
        }

        private NPC FindTarget(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly)
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //朱批上标 (满层在 AddMark 内触发定罪)
            JudgmentInkMarkNPC.AddMark(target, Projectile);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: 0.7f, owner: Projectile.owner);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.RedTorch,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            //朱红双层 ribbon 拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
                outerColor: new Color(150, 20, 20, 140), innerColor: new Color(255, 90, 70, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            Vector2 pos = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);

            //符纹环 (Sparkle = 爆炸线条, 当作朱批符印, 双向自转)
            Texture2D rune = ACMAsset.Sparkle;
            if (rune != null) {
                Color runeColor = new Color(230, 40, 36, 0);
                Main.spriteBatch.Draw(rune, pos, null, runeColor * 0.9f, Projectile.rotation,
                    rune.Size() * 0.5f, 0.26f * pulse, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(rune, pos, null, new Color(255, 120, 90, 0) * 0.7f, -Projectile.rotation * 0.6f,
                    rune.Size() * 0.5f, 0.18f * pulse, SpriteEffects.None, 0f);
            }

            //朱红芯
            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                Main.spriteBatch.Draw(star, pos, null, new Color(255, 80, 70, 0), Projectile.rotation * 0.5f,
                    star.Size() * 0.5f, 0.22f * pulse, SpriteEffects.None, 0f);
            }

            //核心柔光 (廉价, 不占名额 — 多弹同屏克制)
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f, new Color(230, 50, 45) * 0.7f);
            return false;
        }
    }

    /// <summary>
    /// 判决竖笔 - 朱批满层的定罪时刻：敌人头顶提笔(前摇) → 朱红竖笔一瞬劈落(伤害窗口) → 顿笔爆。
    /// ai[0]=目标 whoAmI（提笔期温和跟随目标 x）。伤害判定与竖笔视觉严格对齐。
    /// </summary>
    public class JudgmentVerdictStroke : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 34;
        private const int RaiseEnd = 12;   //0~12f 提笔蓄势 (预警可读)
        private const int SlamEnd = 17;    //12~17f 劈落 (伤害窗口)
        private const float StrokeHeight = 240f;

        private int LifeFrame => LifeTime - Projectile.timeLeft;
        private int TargetIdx => (int)Projectile.ai[0];

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.JudgmentVerdictStroke.DisplayName",
                () => "Verdict Stroke");
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一笔一判
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //提笔期温和跟随目标 (定罪不脱靶, 劈落后锚定)
            if (LifeFrame < RaiseEnd && TargetIdx >= 0 && TargetIdx < Main.maxNPCs) {
                NPC target = Main.npc[TargetIdx];
                if (target.active && !target.friendly) {
                    Projectile.Center = Vector2.Lerp(Projectile.Center, target.Center, 0.35f);
                }
            }

            //劈落帧: 声与撼
            if (LifeFrame == RaiseEnd) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.25f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.45f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 4.5f);
            }

            //顿笔爆帧: 朱砂飞溅
            if (LifeFrame == SlamEnd) {
                for (int i = 0; i < 10; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                        Main.rand.NextVector2Circular(5f, 3.5f) - new Vector2(0f, 1.5f), 100, default,
                        Main.rand.NextFloat(1.2f, 1.9f));
                    d.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.15f, 0.1f);
        }

        public override bool? CanDamage() => LifeFrame >= RaiseEnd && LifeFrame < SlamEnd + 2;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //竖笔线段判定 (与视觉对齐)
            Vector2 top = Projectile.Center + new Vector2(0f, -StrokeHeight);
            Vector2 bottom = Projectile.Center + new Vector2(0f, 26f);
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), top, bottom, 30f, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: 1.3f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            int f = LifeFrame;
            Vector2 anchor = Projectile.Center;

            if (f < RaiseEnd) {
                //—— 提笔: 头顶浮现朱红笔锋点 + 渐亮预告竖线 (预警三要素: 形+色+渐强) ——
                float t = f / (float)RaiseEnd;
                Vector2 tipPos = anchor + new Vector2(0f, -StrokeHeight - 30f + t * 20f);
                WeaponVFX.DrawGlowBurst(tipPos, 0.5f + t * 0.6f, new Color(235, 60, 45) * (0.4f + t * 0.6f));
                //淡竖线预告
                ACMShaders.DrawBeam(tipPos, anchor + new Vector2(0f, 16f), halfWidth: 2.5f + t * 2f,
                    core: new Color(255, 120, 100, 160), edge: new Color(120, 20, 20, 0),
                    intensity: 0.25f + t * 0.3f, flowSpeed: 1.6f, flowScale: 2.5f, coreSharp: 2f);
            }
            else {
                //—— 劈落 + 余韵: 朱红判决竖笔 (一瞬全亮, 指数消散) ——
                float sinceSlam = f - RaiseEnd;
                float slamT = MathHelper.Clamp(sinceSlam / (SlamEnd - RaiseEnd), 0f, 1f);
                float fade = f <= SlamEnd ? 1f : MathF.Pow(0.86f, f - SlamEnd);

                //竖笔本体 (劈落期笔锋自上而下生长 — poly 锐出)
                float grow = 1f - MathF.Pow(1f - MathHelper.Clamp(slamT * 1.6f, 0f, 1f), 8f);
                Vector2 top = anchor + new Vector2(0f, -StrokeHeight);
                Vector2 bottom = Vector2.Lerp(top, anchor + new Vector2(0f, 26f), grow);
                ACMShaders.DrawBeam(top, bottom, halfWidth: 13f * fade + 3f,
                    core: new Color(255, 230, 210, 230), edge: new Color(200, 35, 30, 40),
                    intensity: fade, flowSpeed: 3f, flowScale: 1.6f, coreSharp: 3f, coreGlow: 0.8f);

                //顿笔点: 底部朱砂晕 + 冲击环
                if (grow >= 0.95f) {
                    float ringLife = MathHelper.Clamp((f - SlamEnd) / 12f, 0f, 1f);
                    WeaponVFX.DrawShockwaveRing(anchor + new Vector2(0f, 16f), 10f + ringLife * 52f, 9f,
                        (1f - ringLife) * 0.9f, new Color(255, 140, 110), new Color(150, 25, 25));
                    WeaponVFX.DrawGlowBurst(anchor, 1.1f * fade, new Color(235, 70, 50) * fade);
                }

                //劈落一瞬的径向泛光 (名额满自动退化)
                if (sinceSlam < 4f)
                    WeaponVFX.DrawRadialBloom(anchor, 0.06f, 0.7f * fade, new Color(240, 70, 55), 4f);
            }

            return false;
        }
    }
}
