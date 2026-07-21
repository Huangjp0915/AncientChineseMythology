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
    /// 黄泉寂灭冥罗杖 - StaveofNetherflow的觉醒升级版
    /// 发射 1 枚寂灭主球 (强追踪) + 2 枚侍球 (0.28×); 主球命中展开"冥府罗网"
    /// 真实束缚区 (区内减速 + 周期魂蚀 tick), 3 秒后月蚀坍缩 — 收拢并炸出坍缩爆。
    /// 觉醒形态: 罗网半径 ×1.35, tick 间隔 20→14。
    /// </summary>
    public class StaveofNetherEclipse : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 2600;
            Item.crit = 14;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<NetherEclipseOrb>();
            Item.shootSpeed = 14f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 staffTip = player.Center + velocity.SafeNormalize(Vector2.Zero) * 60f;

            // 寂灭主球
            Projectile.NewProjectile(source, staffTip, velocity, type, damage, knockback, player.whoAmI, 0f, 0f);
            // 双侍球 (0.28×, 斜两侧)
            for (int i = -1; i <= 1; i += 2) {
                Vector2 perturbedVel = velocity.RotatedBy(MathHelper.ToRadians(i * 14f)) * 0.9f;
                Projectile.NewProjectile(source, staffTip, perturbedVel, type,
                    (int)(damage * 0.28f), knockback * 0.5f, player.whoAmI, 0f, 1f);
            }

            // 施法粒子
            for (int i = 0; i < 10; i++) {
                Vector2 vel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(MathHelper.ToRadians(40)) * Main.rand.NextFloat(3f, 8f);
                Dust cast = Dust.NewDustPerfect(staffTip, DustID.Wraith, vel, 100, default, Main.rand.NextFloat(1.4f, 2.2f));
                cast.noGravity = true;
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<StaveofNetherflow>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 寂灭冥球 (ai[1]=1 为侍球): 主球强追踪, 命中展开冥府罗网真实束缚区; 侍球轻追踪小爆。
    /// </summary>
    public class NetherEclipseOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/StaveofNetherEclipse";
        private ref float Timer => ref Projectile.ai[0];
        private bool Attendant => Projectile.ai[1] >= 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.15f;

            if (Timer > 12f) {
                NPC target = FindClosestNPC(Attendant ? 450f : 650f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), Attendant ? 0.05f : 0.08f);
                }
            }

            float pulse = 0.8f + MathF.Sin(Timer * 0.18f) * 0.2f;
            Lighting.AddLight(Projectile.Center, 0.4f * pulse, 1f * pulse, 1.2f * pulse);

            // 冥罗旋涡粒子 (侍球减半)
            if (!Attendant || Main.rand.NextBool(2)) {
                float angle = Timer * 0.35f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(10f, 22f);
                Dust vortex = Dust.NewDustDirect(
                    Projectile.Center + offset, 4, 4, DustID.Wraith,
                    -offset.X * 0.15f, -offset.Y * 0.15f,
                    100, default, Main.rand.NextFloat(1.2f, 2f));
                vortex.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f, 4, 4, DustID.Shadowflame,
                    -Projectile.velocity.X * 0.25f, -Projectile.velocity.Y * 0.25f,
                    120, default, Main.rand.NextFloat(1f, 1.8f));
                trail.noGravity = true;
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
            target.AddBuff(BuffID.ShadowFlame, 360);
            target.AddBuff(BuffID.Slow, 300);

            for (int i = 0; i < (Attendant ? 10 : 20); i++) {
                float angle = MathHelper.TwoPi / 20f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(6f, 12f);
                Dust vortex = Dust.NewDustPerfect(target.Center, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.8f, 3f));
                vortex.noGravity = true;
            }

            if (Projectile.owner == Main.myPlayer) {
                var mp = Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>();
                mp.AddKarma(Attendant ? 0.8f : 2.5f);

                // —— 主球命中: 展开冥府罗网真实束缚区 (每球仅首个命中展开一次) ——
                if (!Attendant && Projectile.localAI[1] == 0f) {
                    Projectile.localAI[1] = 1f;
                    float radius = mp.Awakened ? 350f : 260f;
                    int tick = mp.Awakened ? 14 : 20;
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<NetherNetField>(),
                        (int)(Projectile.damage * 0.25f), 0f, Projectile.owner, radius, tick);
                }
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.NetherGrudge, scale: Attendant ? 0.9f : 1.3f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, Attendant ? 1.5f : 3f);

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = Attendant ? 0.4f : 0.6f, Pitch = 0.5f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            float sizeMul = Attendant ? 0.62f : 1f;

            // 冥罗青蓝双层带状拖尾
            WeaponVFX.DrawProjectileTrail(Projectile, 16f * sizeMul,
                new Color(30, 110, 150), new Color(110, 240, 255),
                uvScroll: Timer * 0.025f);

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float pulse1 = (1f + MathF.Sin(Timer * 0.18f) * 0.15f) * sizeMul;
                Color innerGlow = new Color(100, 250, 255) * 0.8f;
                innerGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, innerGlow, 0f, glowOrigin, pulse1, SpriteEffects.None, 0);
                float pulse2 = (1.3f + MathF.Sin(Timer * 0.12f + 1f) * 0.2f) * sizeMul;
                Color outerGlow = new Color(50, 140, 200) * 0.4f;
                outerGlow.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, outerGlow, 0f, glowOrigin, pulse2, SpriteEffects.None, 0);
            }

            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                Color starColor = new Color(140, 230, 255) * 0.6f;
                starColor.A = 0;
                float starScale = (0.35f + MathF.Sin(Timer * 0.3f) * 0.1f) * sizeMul;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, Timer * 0.12f, starOrigin, starScale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < (Attendant ? 10 : 18); i++) {
                Vector2 vel = Main.rand.NextVector2Circular(7f, 7f);
                Dust death = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.6f, 2.6f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 冥府罗网束缚区 (真实伤害区, ai[0]=半径, ai[1]=tick 间隔帧):
    /// 区内敌人减速 + 周期魂蚀 (usesLocalNPCImmunity 周期结算, 0 击退);
    /// 180f 后月蚀坍缩 18f — 半径收拢、亮度上冲, 终帧坍缩爆 (×3.2 tick 面额)。
    /// ArenaRunic 罗网地纹 + tick 同步冲击环脉冲。
    /// </summary>
    public class NetherNetField : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private ref float Radius => ref Projectile.ai[0];
        private ref float TickCd => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        private const int HoldTime = 180;
        private const int CollapseTime = 18;
        private bool _finalBurst;

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.NetherNetField.DisplayName",
                () => "Nether Net Zone");
        }

        public override void SetDefaults() {
            Projectile.width = 520;
            Projectile.height = 520;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = HoldTime + CollapseTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void OnSpawn(IEntitySource source) {
            if (Radius <= 0f)
                Radius = 260f;
            if (TickCd > 0f)
                Projectile.localNPCHitCooldown = (int)TickCd;
            Projectile.Resize((int)(Radius * 2f), (int)(Radius * 2f));
            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.7f, Pitch = -0.35f }, Projectile.Center);
        }

        public override bool ShouldUpdatePosition() => false;

        // 圆形判定 (方框裁圆)
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float curRadius = CurrentRadius();
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, Projectile.Center) <= curRadius * curRadius;
        }

        private float CurrentRadius() {
            if (Timer <= HoldTime)
                return Radius;
            float c = (Timer - HoldTime) / CollapseTime;
            return MathHelper.Lerp(Radius, Radius * 0.25f, c * c); // 月蚀坍缩: 越缩越快
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (_finalBurst)
                modifiers.FinalDamage *= 3.2f; // 坍缩爆 (≈0.8× 主球名义)
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.3f, 0.8f, 1f);

            // 区内减速 (每 10f 一轮)
            if ((int)Timer % 10 == 0) {
                float curRadius = CurrentRadius();
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    if (Vector2.Distance(Projectile.Center, npc.Center) < curRadius + npc.width * 0.5f) {
                        npc.AddBuff(BuffID.Slow, 60);
                        npc.AddBuff(BuffID.ShadowFlame, 90);
                    }
                }
            }

            // 罗网吸入魂雾 (converging)
            if (Timer <= HoldTime && Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(Radius * 0.5f, Radius * 1.05f);
                Dust pull = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Wraith : DustID.BlueTorch,
                    (Projectile.Center - pos) * 0.02f, 110, default, Main.rand.NextFloat(1.2f, 1.9f));
                pull.noGravity = true;
            }

            // —— 月蚀坍缩 ——
            if (Timer == HoldTime) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.45f }, Projectile.Center);
            }
            if (Timer > HoldTime && (int)Timer % 6 == 0) {
                float cur = CurrentRadius();
                Projectile.Resize((int)(cur * 2f), (int)(cur * 2f));
            }
            if (Timer >= HoldTime + CollapseTime - 1 && !_finalBurst) {
                // 终帧坍缩爆: 清免疫窗重结算一轮 (×3.2)
                _finalBurst = true;
                for (int i = 0; i < Main.maxNPCs; i++)
                    Projectile.localNPCImmunity[i] = 0;
                Projectile.Resize((int)(Radius * 1.1f), (int)(Radius * 1.1f));
                Projectile.Damage();

                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.4f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 5f);
                if (Projectile.owner == Main.myPlayer)
                    ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                        ACMWeaponBurst.NetherGrudge, scale: 1.8f, owner: Projectile.owner);
                for (int i = 0; i < 26; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Wraith, vel, 70, default, Main.rand.NextFloat(1.8f, 3f));
                    burst.noGravity = true;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 120);
            target.AddBuff(BuffID.ShadowFlame, 180);
            if (Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(0.5f);
            // tick 命中轻演出 (低频, 不轰炸)
            if (Main.rand.NextBool(3))
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.NetherGrudge, scale: 0.7f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float total = HoldTime + CollapseTime;
            float life = Timer / total;
            float fadeIn = MathHelper.Clamp(Timer / 12f, 0f, 1f);
            bool collapsing = Timer > HoldTime;
            float collapseFrac = collapsing ? (Timer - HoldTime) / CollapseTime : 0f;

            float curRadius = CurrentRadius();
            Color primary = Color.Lerp(new Color(110, 240, 255), new Color(200, 250, 255), collapseFrac);
            Color secondary = new Color(30, 110, 160);
            SpriteBatch sb = Main.spriteBatch;

            // —— ArenaRunic 冥罗网法阵地纹 (坍缩期亮度上冲) ——
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                ACMShaders.WorldDecalParams(Projectile.Center, curRadius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(fadeIn * (0.7f + collapseFrac * 0.3f));
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(12f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);

                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // —— tick 同步脉冲环 (读得出伤害节拍) ——
            float tickInterval = MathF.Max(Projectile.localNPCHitCooldown, 1f);
            float tickPhase = (int)Timer % (int)tickInterval / tickInterval;
            if (!collapsing)
                WeaponVFX.DrawShockwaveRing(Projectile.Center, curRadius * (0.35f + tickPhase * 0.62f), 9f,
                    (1f - tickPhase) * 0.45f * fadeIn, primary, secondary);

            // 坍缩期: 白青亮环收拢 + 终爆辉光
            if (collapsing) {
                WeaponVFX.DrawShockwaveRing(Projectile.Center, curRadius, 13f,
                    0.85f, new Color(220, 250, 255), new Color(60, 150, 200));
                if (collapseFrac > 0.7f)
                    WeaponVFX.DrawRadialBloom(Projectile.Center, 0.09f, (collapseFrac - 0.7f) / 0.3f * 0.7f,
                        new Color(140, 230, 255), 6f);
            }

            return false;
        }
    }
}
