using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.Revenants;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 冥府噬魂幽火炮 - NetherfireBlunderbuss的觉醒升级版
    /// 左键固定 12 发噬魂霰弹; 命中/击杀吸魂充能"魂膛"(0~6 格);
    /// 右键消耗全部魂膛发射魂爆聚束炮 (880×(2+魂膛) 单发大弹) — 攒魂换大炮的资源循环。
    /// 觉醒形态: 散布收窄一半 + 魂膛获取翻倍。
    /// </summary>
    public class SoulEatingCannon : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 880;
            Item.crit = 18;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 68;
            Item.height = 30;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item36;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SoulEatingBullet>();
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override Vector2? HoldoutOffset() { return new Vector2(-12, 2); }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            var mp = player.GetModPlayer<RevenantEXKarmaPlayer>();
            if (player.altFunctionUse == 2) {
                // 右键: 至少 2 格魂膛才可聚束 (仅在新点击时提示, 防按住连响)
                if (mp.SoulChamber < 2) {
                    if (player.whoAmI == Main.myPlayer && Main.mouseRightRelease)
                        SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = -0.6f }, player.Center);
                    return false;
                }
                Item.useTime = 32;
                Item.useAnimation = 32;
                Item.UseSound = null; // 聚束音效在 Shoot 分层播放
            }
            else {
                Item.useTime = 18;
                Item.useAnimation = 18;
                Item.UseSound = SoundID.Item36;
            }
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var mp = player.GetModPlayer<RevenantEXKarmaPlayer>();
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 40f;

            if (player.altFunctionUse == 2) {
                // —— 右键: 魂爆聚束炮 (消耗全部魂膛) ——
                int chamber = mp.SoulChamber;
                mp.SoulChamber = 0;
                Projectile.NewProjectile(source, position, muzzleDir * 18f,
                    ModContent.ProjectileType<SoulMawBlast>(), damage * (2 + chamber), knockback * 1.5f, player.whoAmI, chamber);

                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 1f, Pitch = -0.4f }, position);
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.8f, Pitch = -0.5f }, position);
                // 重后坐 (发射器反作用力 ∝ 弹重)
                player.velocity -= muzzleDir * (5f + chamber * 0.8f);
                SoulCannonMuzzleFlash.Spawn(source, muzzlePos, player.whoAmI);
                WeaponVFX.AddScreenShake(player, 6f);
                return false;
            }

            // —— 左键: 固定 12 发噬魂霰弹 (觉醒散布收窄一半) ——
            float spread = mp.Awakened ? 7.5f : 15f;
            for (int i = 0; i < 12; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(spread));
                perturbedSpeed *= Main.rand.NextFloat(0.8f, 1.25f);
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
            }

            // 炮口焰 (克制版: 22 尘)
            for (int i = 0; i < 14; i++) {
                Vector2 smokeVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(40)) * Main.rand.NextFloat(3f, 10f);
                Dust smoke = Dust.NewDustPerfect(muzzlePos, DustID.Smoke, smokeVel, 150, new Color(100, 40, 160), Main.rand.NextFloat(2f, 3.2f));
                smoke.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 sparkVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(30)) * Main.rand.NextFloat(5f, 14f);
                Dust spark = Dust.NewDustPerfect(muzzlePos, DustID.PurpleTorch, sparkVel, 80, default, Main.rand.NextFloat(2f, 3.2f));
                spark.noGravity = true;
            }
            // 后坐力
            player.velocity -= muzzleDir * 3f;
            SoulCannonMuzzleFlash.Spawn(source, muzzlePos, player.whoAmI);
            WeaponVFX.AddScreenShake(player, 3f);
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position += velocity.SafeNormalize(Vector2.Zero) * 25f;
        }

        public override void HoldItem(Player player) {
            // 魂膛可视: 身周环绕的魂焰数 = 魂膛格数 (轻量 dust, 每 6 帧 1 个)
            if (Main.dedServ || player.whoAmI != Main.myPlayer)
                return;
            var mp = player.GetModPlayer<RevenantEXKarmaPlayer>();
            if (mp.SoulChamber > 0 && Main.GameUpdateCount % 6 == 0) {
                float ang = Main.GameUpdateCount * 0.07f + Main.rand.NextFloat(0.4f);
                int slot = (int)(Main.GameUpdateCount / 6) % Math.Max(1, mp.SoulChamber);
                Vector2 pos = player.MountedCenter + (ang + slot * MathHelper.TwoPi / 6f).ToRotationVector2() * 34f;
                Dust wisp = Dust.NewDustPerfect(pos, DustID.Wraith, new Vector2(0f, -0.8f), 130, default, 1.1f);
                wisp.noGravity = true;
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NetherfireBlunderbuss>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 噬魂霰弹: 命中吸魂 (计数转魂膛), 击杀直接 +1 格并回复生命。
    /// </summary>
    public class SoulEatingBullet : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/SoulEatingCannon";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity.Y += 0.03f;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.3f, 1f);

            Dust flame = Dust.NewDustDirect(
                Projectile.Center - Projectile.velocity, 4, 4, DustID.PurpleTorch,
                -Projectile.velocity.X * 0.4f, -Projectile.velocity.Y * 0.4f,
                100, default, Main.rand.NextFloat(1f, 1.7f));
            flame.noGravity = true;
            if (Main.rand.NextBool(4)) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(6, 6), 4, 4, DustID.Wraith,
                    0f, -0.5f, 120, default, 1.0f);
                soul.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.OnFire3, 300);

            // 噬魂爆裂 (克制)
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(1.5f, 2.4f));
                burst.noGravity = true;
            }

            Player owner = Main.player[Projectile.owner];
            if (Projectile.owner == Main.myPlayer) {
                var mp = owner.GetModPlayer<RevenantEXKarmaPlayer>();
                mp.AddKarma(0.4f);

                // —— 吸魂充膛: 每 8 次命中 +1 格 (觉醒 4 次); 击杀直接 +1 (觉醒 +2) ——
                int hitsNeed = mp.Awakened ? 4 : 8;
                mp.SoulHitCounter++;
                bool gained = false;
                if (mp.SoulHitCounter >= hitsNeed) {
                    mp.SoulHitCounter = 0;
                    gained = true;
                }
                if (target.life <= 0) {
                    mp.SoulChamber = Math.Min(6, mp.SoulChamber + (mp.Awakened ? 2 : 1));
                    gained = true;
                    owner.Heal(Main.rand.Next(15, 30));
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.5f }, target.Center);
                    for (int i = 0; i < 8; i++) {
                        Vector2 soulVel = (owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 10f);
                        soulVel = soulVel.RotatedByRandom(0.3f);
                        Dust soul = Dust.NewDustPerfect(target.Center, DustID.Wraith, soulVel, 80, default, 2f);
                        soul.noGravity = true;
                    }
                    // 击杀染屏定调 (短促, 走名额契约)
                    SoulEatPaletteFinisher.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                }
                else if (gained) {
                    mp.SoulChamber = Math.Min(6, mp.SoulChamber + 1);
                }
                if (gained && mp.SoulChamber > 0) {
                    // 充膛提示: 音高 = 膛位
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.45f, Pitch = -0.5f + mp.SoulChamber * 0.15f }, owner.Center);
                    ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), owner.Center,
                        ACMWeaponBurst.SoulFire, scale: 0.7f, owner: Projectile.owner);
                }
            }

            // 命中冲击演出 (噬魂紫径向辉光 + 冲击环)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 噬魂紫双层带状弹迹
            WeaponVFX.DrawProjectileTrail(Projectile, 9f,
                new Color(110, 30, 180), new Color(225, 130, 255));

            Texture2D lightShot = ACMAsset.LightShot;
            if (lightShot != null) {
                Vector2 origin = lightShot.Size() / 2f;
                Color mainColor = new Color(240, 120, 255) * 0.9f;
                mainColor.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, 0.7f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    80, default, Main.rand.NextFloat(1.3f, 2.2f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 魂爆聚束炮 (右键大招, ai[0]=消耗的魂膛数): 巨型魂弹, 命中/超时大爆 —
    /// GenericWarp 虚空扭曲 + 层叠冲击环 + 范围伤害 (爆炸半径随魂膛数增长)。
    /// </summary>
    public class SoulMawBlast : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private ref float Chamber => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.SoulMawBlast.DisplayName",
                () => "Soul Maw Blast");
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.3f;
            Lighting.AddLight(Projectile.Center, 1.2f, 0.5f, 1.6f);

            // 轻微追踪 (重弹惯性感: 转向缓慢)
            NPC target = FindClosestNPC(500f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.03f);
            }

            // 吞魂旋涡尾迹
            for (int i = 0; i < 2; i++) {
                float ang = Timer * 0.4f + i * MathHelper.Pi;
                Vector2 offset = ang.ToRotationVector2() * 20f;
                Dust vortex = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Wraith,
                    -offset * 0.1f - Projectile.velocity * 0.1f, 100, default, Main.rand.NextFloat(1.4f, 2f));
                vortex.noGravity = true;
            }
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) { closestDist = dist; closest = npc; }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(9f);
            Detonate();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Detonate();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (timeLeft <= 0)
                Detonate();
        }

        private bool _detonated;
        private void Detonate() {
            if (_detonated)
                return;
            _detonated = true;

            float radius = 180f + Chamber * 30f;
            // 范围结算 (借 Projectile.Damage 的一次性扩容判定)
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.Resize((int)(radius * 2f), (int)(radius * 2f));
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.3f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 6f);

            for (int i = 0; i < 36; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(14f, 14f);
                Dust fire = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.PurpleTorch : DustID.Wraith,
                    vel, 80, default, Main.rand.NextFloat(2f, 3.6f));
                fire.noGravity = true;
            }

            if (Projectile.owner == Main.myPlayer) {
                SoulShatterBlastFX.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, 0, Projectile.owner);
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                    ACMWeaponBurst.AbyssPurple, scale: 1.6f + Chamber * 0.1f, owner: Projectile.owner);
            }
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255)
                return false;

            float chamberFrac = MathHelper.Clamp(Chamber / 6f, 0f, 1f);
            // 弹体越重拖尾越宽
            WeaponVFX.DrawProjectileTrail(Projectile, 18f + chamberFrac * 14f,
                new Color(110, 30, 180), new Color(230, 150, 255), uvScroll: Timer * 0.04f);

            // 吞魂核 (双层柔光 + 旋涡星)
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                float pulse = 1.1f + chamberFrac * 0.6f + MathF.Sin(Timer * 0.3f) * 0.15f;
                Color core = new Color(235, 160, 255) * 0.95f;
                core.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, core, 0f, origin, pulse, SpriteEffects.None, 0);
                Color halo = new Color(120, 40, 190) * 0.55f;
                halo.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, halo, 0f, origin, pulse * 1.7f, SpriteEffects.None, 0);
            }
            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                Color starCol = new Color(255, 210, 255) * 0.8f;
                starCol.A = 0;
                Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null, starCol,
                    Projectile.rotation, star.Size() / 2f, 0.5f + chamberFrac * 0.25f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 噬魂炮口闪光弹幕 (纯视觉, damage=0): 每次开炮在炮口展开 RadialBloom 大闪 + 冲击环。绘制只在 PreDraw。
    /// </summary>
    public class SoulCannonMuzzleFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 16;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<SoulCannonMuzzleFlash>(), 0, 0f, owner);
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(1f - life, 0f, 1f);

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 10f + life * 70f, 9f, fade * 0.8f,
                new Color(235, 150, 255), new Color(120, 40, 190));
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.12f, fade * 0.9f, new Color(210, 110, 255), 0f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.5f + life * 1.5f) * 1.6f, new Color(220, 130, 255) * (fade * 0.7f));
            return false;
        }
    }

    /// <summary>
    /// 噬魂染屏演出弹幕 (纯视觉, damage=0): 击杀瞬间对全屏做短促 PaletteLUT 噬魂紫定调 (强度 ≤0.15, 占单一名额)。
    /// </summary>
    public class SoulEatPaletteFinisher : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 24;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<SoulEatPaletteFinisher>(), 0, 0f, owner);
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(life < 0.25f ? life / 0.25f : 1f - (life - 0.25f) / 0.75f, 0f, 1f);

            // 噬魂紫: 阴影偏深紫, 高光偏亮紫 (ApplyPaletteTint 内部 clamp ≤0.15 + 占名额)
            WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                new Color(60, 20, 110), new Color(210, 140, 255), fade * 0.15f, saturation: 1.1f);
            return false;
        }
    }
}
