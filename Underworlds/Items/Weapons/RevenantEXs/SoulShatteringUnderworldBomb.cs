using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.Revenants;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 冥岩碎魂黄泉雷 - NetherRockSoulbomb的觉醒升级版
    /// 主雷爆炸撕开"黄泉之门"(竖立门扉驻留 1.2s): 门柱魂光 + 门内虚空;
    /// 3 枚子雷从门中依次喷出砸向投掷方向扇形; 门对穿行敌人挂魂火。
    /// 觉醒形态: 门喷子雷 +2 枚。
    /// </summary>
    public class SoulShatteringUnderworldBomb : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1700;
            Item.crit = 14;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 36;
            Item.height = 36;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 16f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<SoulShatteringBombProj>();
            Item.shootSpeed = 12f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0f, 0f);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NetherRockSoulbomb>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 碎魂黄泉雷主弹: 引信 60f (渐亮警示 + 临爆抖动), 爆炸后撕开黄泉之门 (YomiGate)。
    /// </summary>
    public class SoulShatteringBombProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/SoulShatteringUnderworldBomb";
        private ref float Timer => ref Projectile.ai[0];
        private ref float HasBounced => ref Projectile.ai[1];
        private const int FuseTime = 60;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FuseTime + 30;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f) Projectile.velocity.Y = 16f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;
            // 记录投掷朝向 (门喷子雷的扇形方向)
            if (MathF.Abs(Projectile.velocity.X) > 0.5f)
                Projectile.localAI[0] = MathF.Sign(Projectile.velocity.X);

            float fuseProgress = Timer / FuseTime;
            float flicker = MathF.Sin(Timer * (0.4f + fuseProgress * 0.6f)) * 0.5f + 0.5f;
            Lighting.AddLight(Projectile.Center, 1f * flicker * fuseProgress, 0.4f * flicker * fuseProgress, 1.2f * flicker * fuseProgress);

            // 引信粒子
            for (int i = 0; i < 2; i++) {
                Dust fuse = Dust.NewDustDirect(
                    Projectile.Center + new Vector2(0, -Projectile.height * 0.4f), 4, 4, DustID.PurpleTorch,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f),
                    80, default, Main.rand.NextFloat(1.2f, 2f));
                fuse.noGravity = true;
            }
            if (fuseProgress > 0.7f && Main.rand.NextBool(2)) {
                Dust warn = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10), 4, 4, DustID.Shadowflame,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    100, default, Main.rand.NextFloat(1.5f, 2.5f));
                warn.noGravity = true;
            }

            if (Timer >= FuseTime) { Explode(); }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(6f);
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (HasBounced == 0) {
                HasBounced = 1;
                if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.4f;
                if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
                Projectile.velocity *= 0.5f;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.1f }, Projectile.Center);
                return false;
            }
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        private void Explode() {
            if (Projectile.timeLeft <= 0) return;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.position -= new Vector2(160, 160);
            Projectile.width = 320;
            Projectile.height = 320;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.4f, Pitch = -0.5f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.4f }, Projectile.Center);
            Vector2 explosionCenter = Projectile.Center;

            // 爆炸粒子 (克制版)
            for (int i = 0; i < 36; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);
                Dust fire = Dust.NewDustPerfect(explosionCenter, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(2.2f, 4f));
                fire.noGravity = true;
            }
            for (int i = 0; i < 22; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(13f, 13f);
                vel.Y -= 3f;
                Dust soul = Dust.NewDustPerfect(explosionCenter, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.8f, 3f));
                soul.noGravity = true;
            }

            Lighting.AddLight(explosionCenter, 3f, 1.5f, 4f);

            // 一段大爆演出: GenericWarp 虚空冲击扭曲 + 层叠冲击环 (仅本机)
            SoulShatterBlastFX.Spawn(Projectile.GetSource_FromThis(), explosionCenter, 0, Projectile.owner);
            WeaponVFX.AddScreenShake(explosionCenter, 6f);

            // 范围debuff
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(explosionCenter, npc.Center) < 240f) {
                    npc.AddBuff(BuffID.ShadowFlame, 600);
                    npc.AddBuff(BuffID.OnFire3, 600);
                    npc.AddBuff(BuffID.Ichor, 600);
                }
            }

            // —— 撕开黄泉之门: 门驻留并从门中喷出子雷 (owner 侧生成) ——
            if (Projectile.owner == Main.myPlayer) {
                var mp = Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>();
                int subCount = mp.Awakened ? 5 : 3;
                float facing = Projectile.localAI[0] == 0f ? 1f : Projectile.localAI[0];
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), explosionCenter, Vector2.Zero,
                    ModContent.ProjectileType<YomiGate>(), Projectile.damage / 2, Projectile.knockBack * 0.5f,
                    Projectile.owner, facing, subCount);
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255) return false;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            float fuseProgress = Timer / FuseTime;

            Color mainColor = Color.Lerp(lightColor, new Color(220, 160, 255), fuseProgress * 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null && fuseProgress > 0.2f) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float glowIntensity = (fuseProgress - 0.2f) / 0.8f;
                float pulse = 0.6f + MathF.Sin(Timer * (0.4f + fuseProgress * 0.5f)) * 0.2f;
                Color glowColor = new Color(220, 100, 255) * glowIntensity * 0.7f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, glowColor, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 黄泉之门 (主雷爆点驻留 72f): 竖立门扉 — BeamGrad 双门柱 + 门楣 + 门内虚空玄光;
    /// 每 12f 从门中喷出一枚子雷砸向投掷朝向扇形 (ai[0]=朝向, ai[1]=子雷数);
    /// 贴门敌人挂魂火。伤害本体为 0 (damage 字段只作子雷面额载体)。
    /// </summary>
    public class YomiGate : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private ref float Facing => ref Projectile.ai[0];
        private ref float SubCount => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        private ref float Spawned => ref Projectile.localAI[1];
        private const int Life = 72;
        private const float GateHalfWidth = 55f;
        private const float GateHeight = 150f;

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.YomiGate.DisplayName",
                () => "Gate of Yellow Springs");
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false; // 门本体不结算伤害
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.3f, 1.3f);

            // 开门帧
            if ((int)Timer == 1)
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f, Pitch = -0.5f }, Projectile.Center);

            // —— 每 12f 从门中喷一枚子雷 (从第 10f 起, owner 侧生成) ——
            if (Timer >= 10f && Spawned < SubCount && (int)(Timer - 10f) % 12 == 0) {
                Spawned++;
                if (Projectile.owner == Main.myPlayer) {
                    // 扇形: 朝投掷方向 15°~65° 仰角
                    float t = (Spawned - 1f) / MathF.Max(1f, SubCount - 1f);
                    float angDeg = MathHelper.Lerp(15f, 65f, t) + Main.rand.NextFloat(-6f, 6f);
                    float rad = MathHelper.ToRadians(angDeg);
                    Vector2 vel = new Vector2(MathF.Cos(rad) * Facing, -MathF.Sin(rad)) * Main.rand.NextFloat(8.5f, 11.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Projectile.Center + new Vector2(0f, -Main.rand.NextFloat(0f, 60f)), vel,
                        ModContent.ProjectileType<SoulShatteringSubBomb>(),
                        Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
                SoundEngine.PlaySound(SoundID.Item42 with { Volume = 0.7f, Pitch = -0.3f + Spawned * 0.08f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 2f);
            }

            // 门内涌出的魂气
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-GateHalfWidth * 0.7f, GateHalfWidth * 0.7f),
                    Main.rand.NextFloat(-GateHeight * 0.9f, 30f));
                Dust wisp = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Wraith : DustID.PurpleTorch,
                    new Vector2(Facing * Main.rand.NextFloat(0.5f, 2f), -Main.rand.NextFloat(0.5f, 2f)),
                    120, default, Main.rand.NextFloat(1.2f, 2f));
                wisp.noGravity = true;
            }

            // 贴门敌人挂魂火 (每 15f 一轮)
            if ((int)Timer % 15 == 0) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly) continue;
                    if (MathF.Abs(npc.Center.X - Projectile.Center.X) < GateHalfWidth + npc.width * 0.5f
                        && npc.Center.Y > Projectile.Center.Y - GateHeight && npc.Center.Y < Projectile.Center.Y + 60f) {
                        npc.AddBuff(BuffID.ShadowFlame, 240);
                        npc.AddBuff(BuffID.Frostburn2, 240);
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = Timer / Life;
            // 门扉包络: 8f 升起 → 驻留 → 尾 12f 合拢
            float open = MathHelper.Clamp(Timer / 8f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            if (open <= 0.02f)
                return false;

            Vector2 baseP = Projectile.Center + new Vector2(0f, 46f);
            float h = GateHeight * open;

            // —— 双门柱 (BeamGrad: 酆都黑紫底 + 亮紫芯) ——
            for (int s = -1; s <= 1; s += 2) {
                Vector2 bottom = baseP + new Vector2(s * GateHalfWidth, 0f);
                Vector2 top = bottom - new Vector2(0f, h);
                ACMShaders.DrawBeam(bottom, top, 16f * open,
                    new Color(180, 120, 255), new Color(25, 8, 40), open * 0.9f,
                    flowSpeed: 1.8f, flowScale: 2.2f, coreSharp: 2.2f);
            }
            // 门楣 (横梁)
            Vector2 lintelL = baseP + new Vector2(-GateHalfWidth - 10f, -h);
            Vector2 lintelR = baseP + new Vector2(GateHalfWidth + 10f, -h);
            ACMShaders.DrawBeam(lintelL, lintelR, 12f * open,
                new Color(180, 120, 255), new Color(25, 8, 40), open * 0.9f,
                flowSpeed: 1.4f, flowScale: 2f, coreSharp: 2.2f);

            // —— 门内虚空玄光 (酆都虚空: 暗紫柔光 + 上升流) ——
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 innerCenter = baseP - new Vector2(0f, h * 0.5f);
                Color voidCol = new Color(120, 60, 200) * (0.4f * open);
                voidCol.A = 0;
                Main.EntitySpriteDraw(softGlow, innerCenter - Main.screenPosition, null, voidCol, 0f,
                    softGlow.Size() / 2f, new Vector2(GateHalfWidth / 60f, h / 120f) * 1.4f, SpriteEffects.None, 0);
            }

            // 开门冲击环 (仅前 14f)
            if (Timer < 14f) {
                float ring = Timer / 14f;
                WeaponVFX.DrawShockwaveRing(baseP - new Vector2(0f, h * 0.5f), 16f + ring * 120f, 10f,
                    (1f - ring) * 0.8f, new Color(190, 130, 255), new Color(50, 15, 90));
            }
            return false;
        }
    }

    /// <summary>
    /// 碎魂黄泉雷的分裂子雷 (由黄泉之门喷出): 短引信抛物线, 落地/触敌二段爆。
    /// </summary>
    public class SoulShatteringSubBomb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/SoulShatteringUnderworldBomb";
        private ref float Timer => ref Projectile.ai[0];
        private const int SubFuseTime = 40;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SubFuseTime + 20;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
            Projectile.scale = 0.7f;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity.Y += 0.35f;
            if (Projectile.velocity.Y > 14f) Projectile.velocity.Y = 14f;
            Projectile.rotation += Projectile.velocity.X * 0.06f;

            float fuseProgress = Timer / SubFuseTime;
            Lighting.AddLight(Projectile.Center, 0.6f * fuseProgress, 0.2f * fuseProgress, 0.8f * fuseProgress);

            if (Main.rand.NextBool(2)) {
                Dust fuse = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.PurpleTorch,
                    Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2f, -0.5f),
                    100, default, Main.rand.NextFloat(1f, 1.5f));
                fuse.noGravity = true;
            }
            if (Timer >= SubFuseTime) { SubExplode(); }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(2f);
            SubExplode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity *= 0f;
            return false;
        }

        private void SubExplode() {
            if (Projectile.timeLeft <= 0) return;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
            Projectile.position -= new Vector2(100, 100);
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.Damage();

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0f }, Projectile.Center);
            Vector2 explosionCenter = Projectile.Center;

            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust fire = Dust.NewDustPerfect(explosionCenter, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(1.8f, 3f));
                fire.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                Dust soul = Dust.NewDustPerfect(explosionCenter, DustID.Wraith, vel, 80, default, Main.rand.NextFloat(1.6f, 2.6f));
                soul.noGravity = true;
            }

            Lighting.AddLight(explosionCenter, 2f, 1f, 2.5f);

            // 二段子雷演出: ElementalScreenTint 染屏 + 层叠冲击环 (仅本机)
            SoulShatterBlastFX.Spawn(Projectile.GetSource_FromThis(), explosionCenter, 1, Projectile.owner);
            WeaponVFX.AddScreenShake(explosionCenter, 4f);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(explosionCenter, npc.Center) < 150f) {
                    npc.AddBuff(BuffID.ShadowFlame, 360);
                    npc.AddBuff(BuffID.OnFire3, 360);
                }
            }

            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.alpha >= 255) return false;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            float fuseProgress = Timer / SubFuseTime;
            Color mainColor = Color.Lerp(lightColor, new Color(200, 140, 255), fuseProgress * 0.4f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null && fuseProgress > 0.3f) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float glowIntensity = (fuseProgress - 0.3f) / 0.7f;
                float pulse = 0.3f + MathF.Sin(Timer * 0.4f) * 0.1f;
                Color glowColor = new Color(180, 80, 220) * glowIntensity * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, glowColor, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 碎魂两级爆炸演出弹幕 (纯视觉, damage=0)。stage=0 主雷大爆: GenericWarp 虚空冲击扭曲 + 层叠冲击环;
    /// stage=1 子雷二段爆: ElementalScreenTint 短促紫染屏 (≤0.15) + 层叠冲击环。全屏后处理走单一名额; 绘制只在 PreDraw。
    /// </summary>
    public class SoulShatterBlastFX : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 38;
        private int Stage => (int)Projectile.ai[0];

        public static void Spawn(IEntitySource source, Vector2 worldPos, int stage, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<SoulShatterBlastFX>(), 0, 0f, owner, stage);
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
            float fade = MathHelper.Clamp(life < 0.15f ? life / 0.15f : 1f - (life - 0.15f) / 0.85f, 0f, 1f);
            bool main = Stage == 0;
            float baseR = main ? 26f : 16f;
            float grow = main ? 320f : 200f;
            SpriteBatch sb = Main.spriteBatch;

            // —— 层叠冲击环 (三环, 相位错开) ——
            for (int r = 0; r < 3; r++) {
                float phase = MathHelper.Clamp(life - r * 0.12f, 0f, 1f);
                if (phase <= 0f) continue;
                float ringFade = MathHelper.Clamp(1f - phase, 0f, 1f) * fade;
                WeaponVFX.DrawShockwaveRing(Projectile.Center, baseR + phase * grow, main ? 18f : 12f, ringFade * 0.85f,
                    new Color(220, 130, 255), new Color(90, 30, 160));
            }

            if (main) {
                // —— GenericWarp 虚空冲击扭曲 (向外推) ——
                Effect warp = ACMShaders.GenericWarp;
                if (warp != null && fade > 0.05f && ACMShaders.RequestFullscreenSlot()) {
                    ACMShaders.SetCommonParams(warp, Projectile.Center, fade);
                    warp.Parameters["uRadius"]?.SetValue(0.5f + life * 0.3f);
                    warp.Parameters["uWarpScale"]?.SetValue(1.6f);
                    warp.Parameters["uChroma"]?.SetValue(0.6f);
                    warp.Parameters["uRadialPull"]?.SetValue(-0.5f); // 向外推(爆炸)
                    warp.Parameters["uMode"]?.SetValue(4f);          // void
                    warp.Parameters["uTint"]?.SetValue(new Vector4(0.35f, 0.16f, 0.5f, 0.6f));
                    ACMShaders.ApplyScreenPostProcess(sb, warp, bindNoise: true);
                }
                if (fade > 0.4f)
                    WeaponVFX.DrawRadialBloom(Projectile.Center, 0.14f, fade * 0.8f, new Color(190, 110, 255), 8f);
            }
            else {
                // —— ElementalScreenTint 短促紫染屏 (≤0.15, 程序化 overlay) ——
                Effect tint = ACMShaders.ElementalScreenTint;
                if (tint != null && fade > 0.05f) {
                    tint.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    tint.Parameters["uIntensity"]?.SetValue(fade * 0.13f);
                    tint.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
                    tint.Parameters["uTint"]?.SetValue(new Vector4(0.4f, 0.18f, 0.55f, 0.85f));
                    tint.Parameters["uTint2"]?.SetValue(new Vector4(0.12f, 0.05f, 0.2f, 1f));
                    tint.Parameters["uVignette"]?.SetValue(0.5f);
                    tint.Parameters["uFogScale"]?.SetValue(2.8f);
                    sb.End();
                    ACMShaders.DrawFullscreenOverlay(tint, BlendState.AlphaBlend);
                    ACMShaders.RestoreDefaultBatch(sb);
                }
            }

            return false;
        }
    }
}
