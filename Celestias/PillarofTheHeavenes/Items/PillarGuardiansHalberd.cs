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
    /// 镇天神戟 - 天柱敌怪掉落的长枪类近战武器。
    /// 机制身份: 方阵三连突 — 两记短突接第三记"贯阵"重突 (前摇拉杆→爆发全伸→玩家借势前冲),
    /// 贯阵命中轰落天罚落雷。决策点: 连突节奏与第三突的对线定位。
    /// </summary>
    public class PillarGuardiansHalberd : ModItem
    {
        private int comboStep;       // 三连突计数 (Shoot 仅 owner 端调用, 实例字段安全)
        private uint lastThrustTime; // 连段窗口计时

        public override void SetDefaults() {
            Item.damage = 210;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null; // 每突手动播放 (音高随连段递升)
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<HalberdThrust>();
            Item.shootSpeed = 16f;
            Item.crit = 12;
        }

        public override bool CanUseItem(Player player) {
            // 上一突进行中不可再突 (突刺节奏由弹幕生命周期控制)
            return player.ownedProjectileCounts[ModContent.ProjectileType<HalberdThrust>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 1.6s 未续段则重置回第一突
            if (Main.GameUpdateCount - lastThrustTime > 96)
                comboStep = 0;
            lastThrustTime = Main.GameUpdateCount;

            int step = comboStep;
            comboStep = (comboStep + 1) % 3;

            float dmgMul = step == 2 ? 1.7f : 0.85f;
            Projectile.NewProjectile(source, player.Center, velocity, type,
                (int)(damage * dmgMul), knockback, player.whoAmI, step);

            // 突刺声: 短突音高递升, 贯阵低沉
            if (step == 2)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.35f, Volume = 1f }, player.Center);
            else
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.1f + step * 0.15f, Volume = 0.8f }, player.Center);

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "天柱守卫方阵所持的神圣长戟"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "三连突刺：两记短突接一记贯阵重突"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect2", "贯阵释放冲击波并借势前冲，命中轰落天罚落雷"));
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<HeavenFragment>(15).AddIngredient<EmpyriteBar>(15).AddTile(TileID.LunarCraftingStation).Register();
        }
    }

    /// <summary>
    /// 长戟突刺弹幕 - 手持弹幕。ai[0]=连段序号 (0/1 短突 12f, 2 贯阵 20f)。
    /// 波形: 收杆反拉 → poly(20) 爆发全伸 → 收回 (力量感来自爆发帧的陡峭缓出)。
    /// </summary>
    public class HalberdThrust : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Celestias/PillarofTheHeavenes/Items/PillarGuardiansHalberd";

        private int Step => (int)Projectile.ai[0];
        private bool Heavy => Step >= 2;
        private int Duration => Heavy ? 20 : 12;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float BoltFired => ref Projectile.localAI[1];

        private bool hasReleasedWave;
        private float extend; // 当前伸出量 (像素, 负=收杆)

        private Player Owner => Main.player[Projectile.owner];

        // poly(N) ease-out: 前几帧走完几乎全部行程 — "一击"而非"一波"
        private static float PolyOut(float t, float power) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), power);

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每突每敌一跳
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            float t = Timer;
            Timer += 1f;
            if (t >= Duration) {
                Projectile.Kill();
                return;
            }

            float maxExtend = Heavy ? 200f : 150f;
            float pullBack = Heavy ? -40f : -28f;
            // 三段: 收杆(慢, 可读) → 爆发(2~3 帧走完全程) → 收回
            float pullFrames = Heavy ? 8f : 4f;
            float burstFrames = Heavy ? 3f : 2f;

            if (t < pullFrames) {
                extend = pullBack * ACMUtils.QuadOut(t / pullFrames);
            }
            else if (t < pullFrames + burstFrames) {
                float bt = (t - pullFrames) / burstFrames;
                extend = MathHelper.Lerp(pullBack, maxExtend, PolyOut(bt, 20f));
            }
            else {
                float rt = (t - pullFrames - burstFrames) / (Duration - pullFrames - burstFrames);
                extend = MathHelper.Lerp(maxExtend, 26f, ACMUtils.QuadIn(rt));
            }

            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation() + MathHelper.PiOver4;
            Projectile.Center = Owner.MountedCenter + direction * (-60f + extend);

            Owner.direction = direction.X >= 0 ? 1 : -1;
            float armRotation = direction.ToRotation() - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            // 收杆期: 尘向杆身收敛 (蓄势可读)
            if (t < pullFrames && Main.rand.NextBool(2)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(40f, 40f);
                Dust d = Dust.NewDustPerfect(from, DustID.GoldCoin, (Projectile.Center - from) * 0.15f, 120, default, 1.1f);
                d.noGravity = true;
            }

            // 爆发帧: 冲击波 + 贯阵前冲
            if (t >= pullFrames && !hasReleasedWave) {
                hasReleasedWave = true;

                if (Heavy) {
                    // 贯阵: 冲击波 + 玩家借势前冲 (后坐的反向 — 突刺即位移)
                    if (Projectile.owner == Main.myPlayer) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, direction * 15f,
                            ModContent.ProjectileType<HalberdShockwave>(), (int)(Projectile.damage * 0.47f), Projectile.knockBack, Projectile.owner);
                    }

                    if (Projectile.owner == Main.myPlayer) {
                        float fwd = Vector2.Dot(Owner.velocity, direction);
                        if (fwd < 9f)
                            Owner.velocity += direction * 5f;
                    }

                    WeaponVFX.AddScreenShake(Projectile.Center, 3f);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);
                }
                else {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.25f + Step * 0.1f, Volume = 0.55f }, Projectile.Center);
                }

                int burstDust = Heavy ? 16 : 8;
                for (int i = 0; i < burstDust; i++) {
                    Vector2 dustVel = direction.RotatedByRandom(0.35f) * Main.rand.NextFloat(5, Heavy ? 12 : 8);
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                    Dust d = Dust.NewDustPerfect(Projectile.Center + direction * 30f, dustType, dustVel, 100, default, Heavy ? 2.2f : 1.6f);
                    d.noGravity = true;
                }
            }

            // 杆尖金尘 (伸出期)
            if (extend > 40f && Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + direction * 24f;
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                Dust d = Dust.NewDustPerfect(dustPos, dustType, direction * 2f, 100, default, 1.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.4f) * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, Heavy ? 1.3f : 0.9f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, Heavy ? 2.5f : 1.5f);

            // 贯阵命中: 天罚落雷 (每次贯阵只落一道)
            if (Heavy && BoltFired == 0f) {
                BoltFired = 1f;
                HeavenJudgmentBolt.Strike(Projectile.GetSource_OnHit(target),
                    target.Center, (int)(Projectile.damage * 0.35f), 3f, Projectile.owner, 0.85f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            // 爆发窗口: 杆身白金光轴 (速度门控 — 只在最快的几帧出现)
            float pullFrames = Heavy ? 8f : 4f;
            float burstFrames = Heavy ? 3f : 2f;
            if (Timer >= pullFrames && Timer < pullFrames + burstFrames + 3f) {
                float w = Heavy ? 9f : 6f;
                ACMShaders.DrawBeam(Owner.MountedCenter, Projectile.Center + direction * 52f, w,
                    PillarPalette.HolyWhite, PillarPalette.SkyCyan, 0.8f, flowSpeed: 3f, coreSharp: 2.8f);
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2;
            SpriteEffects effects = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRot = Projectile.rotation + (Owner.direction > 0 ? 0 : MathHelper.PiOver2);

            // 发光
            Color glowColor = Color.Gold * (Heavy ? 0.55f : 0.4f);
            glowColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor, drawRot, origin, Projectile.scale * 1.2f, effects, 0f);

            // 主体
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, drawRot, origin, Projectile.scale, effects, 0f);

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 伸出不足时不判定 (视觉与伤害对齐: 收杆期无伤害)
            if (extend < 20f)
                return false;
            Vector2 start = Owner.MountedCenter;
            Vector2 end = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 50f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 30f, ref collisionPoint);
        }
    }

    /// <summary>
    /// 贯阵冲击波 - 第三突释放的天柱冲击
    /// </summary>
    public class HalberdShockwave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2PhoenixBowShot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.96f;

            for (int i = 0; i < 2; i++) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.HeavenlyPillar, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 天柱冲击波环 (扩张衰减)
            float ringLife = 1f - Projectile.timeLeft / 40f;
            WeaponVFX.DrawShockwaveRing(Projectile.Center, 12f + ringLife * 70f, 8f, 1f - ringLife,
                new Color(255, 250, 210), new Color(150, 220, 235));

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle(2, 9);
            Vector2 origin = rectangle.Size() / 2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.6f;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, rectangle, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Color glowColor = Color.Gold * 0.5f;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
