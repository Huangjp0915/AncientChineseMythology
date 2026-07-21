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
    /// 生死因果万魔录 - CodexofFate的觉醒升级版
    /// 左键释放 2 枚重万魔符 (强追踪); 每第 5 次施法书页翻涌, 放出"魔首大符"
    /// (2.2×, 命中必展开万魔法阵链雷 — 可运营的大招节奏, 替代随机暴击链雷)。
    /// 觉醒形态: 每次施法额外从身后飞出 2 枚魔影符 (0.5×, 不耗蓝)。
    /// </summary>
    public class CodexofMyriadDemons : ModItem
    {
        private int castCount;

        public override void SetDefaults() {
            Item.damage = 550;
            Item.crit = 18;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8;
            Item.width = 44;
            Item.height = 44;
            Item.useTime = 8;
            Item.useAnimation = 8;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<MyriadDemonRune>();
            Item.shootSpeed = 20f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var mp = player.GetModPlayer<RevenantEXKarmaPlayer>();
            castCount++;

            if (castCount >= 5) {
                // —— 第 5 次施法: 魔首大符 (2.2×, 穿透+2, 命中必链雷) ——
                castCount = 0;
                Projectile.NewProjectile(source, position, velocity * 0.9f,
                    ModContent.ProjectileType<MyriadDemonLord>(), (int)(damage * 2.2f), knockback * 1.5f, player.whoAmI);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.25f }, position);
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = -0.4f }, position);
                // 书页翻涌爆发
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                    Dust page = Dust.NewDustPerfect(position, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(1.6f, 2.6f));
                    page.noGravity = true;
                }
                WeaponVFX.AddScreenShake(player, 2.5f);
            }
            else {
                // 常规: 2 枚重万魔符
                for (int i = 0; i < 2; i++) {
                    Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(7));
                    perturbedSpeed *= Main.rand.NextFloat(0.92f, 1.08f);
                    Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI);
                }
            }

            // 觉醒形态: 身后斜两侧追加 2 枚魔影符 (0.5×)
            if (mp.Awakened) {
                for (int i = 0; i < 2; i++) {
                    Vector2 back = -velocity.SafeNormalize(Vector2.UnitX).RotatedBy((i == 0 ? 1f : -1f) * 0.7f) * 12f;
                    Projectile.NewProjectile(source, player.Center + back * 3f, -back * 1.4f,
                        type, (int)(damage * 0.5f), knockback * 0.5f, player.whoAmI, 0f, 1f);
                }
            }

            // 施法粒子 (克制)
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust page = Dust.NewDustPerfect(position, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(1.2f, 2f));
                page.noGravity = true;
            }
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position = player.Center + velocity.SafeNormalize(Vector2.Zero) * 30f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CodexofFate>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 万魔符弹幕 (ai[1]=1 为觉醒魔影符, 偏暗色): 强追踪重符, 命中魂火爆。
    /// </summary>
    public class MyriadDemonRune : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/CodexofMyriadDemons";
        private ref float RotationTimer => ref Projectile.ai[0];
        private bool Shade => Projectile.ai[1] >= 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            RotationTimer++;
            Projectile.rotation += 0.2f;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 1.2f);

            if (RotationTimer > 8f) {
                NPC target = FindClosestNPC(700f);
                if (target != null) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.08f);
                }
            }

            // 符文尾迹 (每帧 1 + 概率电弧)
            Dust rune = Dust.NewDustDirect(
                Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(10, 10),
                4, 4, Shade ? DustID.Shadowflame : DustID.PurpleTorch,
                -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                80, default, Main.rand.NextFloat(1.2f, 2f));
            rune.noGravity = true;
            if (Main.rand.NextBool(3)) {
                Dust arc = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15), 4, 4, DustID.Electric,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 60, default, 1.2f);
                arc.noGravity = true;
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
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(0.8f);

            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.Electrified, 300);

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust bolt = Dust.NewDustPerfect(target.Center, DustID.Electric, vel, 60, default, Main.rand.NextFloat(1.3f, 2.2f));
                bolt.noGravity = true;
            }

            // 命中冲击演出 (径向辉光 + 冲击环), 走 ACMWeaponBurst 暗冥紫主题
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.4f : 1f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 2f);

            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.3f + Main.rand.NextFloat(-0.1f, 0.1f) }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 双层带状拖尾 (魔影符偏暗)
            Color outer = Shade ? new Color(60, 25, 110) : new Color(90, 40, 170);
            Color inner = Shade ? new Color(150, 100, 220) : new Color(200, 150, 255);
            WeaponVFX.DrawProjectileTrail(Projectile, 18f, outer, inner, uvScroll: RotationTimer * 0.02f);

            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcIndex = (int)(RotationTimer * 0.12f) % 4;
                int arcHeight = arcSheet.Height / 4;
                Rectangle sourceRect = new Rectangle(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                Vector2 arcOrigin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
                Color arcColor = new Color(200, 120, 255) * 0.5f;
                arcColor.A = 0;
                float arcScale = 0.18f + MathF.Sin(RotationTimer * 0.25f) * 0.03f;
                Main.EntitySpriteDraw(arcSheet, Projectile.Center - Main.screenPosition, sourceRect, arcColor, Projectile.rotation + MathHelper.PiOver2, arcOrigin, arcScale, SpriteEffects.None, 0);
            }

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                Color mainGlow = (Shade ? new Color(160, 90, 230) : new Color(220, 120, 255)) * 0.8f;
                mainGlow.A = 0;
                float pulse = 0.7f + MathF.Sin(RotationTimer * 0.25f) * 0.12f;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, mainGlow, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
            }

            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                Color starColor = new Color(240, 180, 255) * 0.7f;
                starColor.A = 0;
                float starScale = 0.3f + MathF.Sin(RotationTimer * 0.35f) * 0.08f;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, RotationTimer * 0.2f, starOrigin, starScale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 魔首大符 (每第 5 次施法): 12f 显形前摇 (书页汇聚、缓慢推进) → 加速追踪;
    /// 首次命中必展开万魔法阵链雷 (MyriadDemonRuneField) 并连锁 5 名敌人。
    /// </summary>
    public class MyriadDemonLord : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/CodexofMyriadDemons";
        private ref float Timer => ref Projectile.ai[0];
        private ref float Chained => ref Projectile.localAI[0];
        private const int WindupTime = 12;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.MyriadDemonLord.DisplayName",
                () => "Demon Lord Sigil");
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.12f + MathHelper.Clamp(Timer / 60f, 0f, 1f) * 0.15f;
            Lighting.AddLight(Projectile.Center, 1.1f, 0.5f, 1.5f);

            if (Timer < WindupTime) {
                // 显形前摇: 缓慢推进 + 书页汇聚
                Projectile.velocity *= 0.9f;
                if (Main.rand.NextBool(2)) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(30f, 90f);
                    Dust converge = Dust.NewDustPerfect(pos, DustID.PurpleTorch,
                        (Projectile.Center - pos) * 0.1f, 80, default, Main.rand.NextFloat(1.4f, 2f));
                    converge.noGravity = true;
                }
                return;
            }
            if ((int)Timer == WindupTime) {
                // 爆发帧: 弹速释放
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 17f;
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = 0.2f }, Projectile.Center);
            }

            NPC target = FindClosestNPC(820f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.1f);
            }

            for (int i = 0; i < 2; i++) {
                Dust rune = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(14, 14),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    70, default, Main.rand.NextFloat(1.6f, 2.4f));
                rune.noGravity = true;
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
                Main.player[Projectile.owner].GetModPlayer<RevenantEXKarmaPlayer>().AddKarma(4f);

            target.AddBuff(BuffID.ShadowFlame, 420);
            target.AddBuff(BuffID.Electrified, 420);

            // —— 首次命中: 万魔法阵链雷 (必触发, 连锁 5 敌) ——
            if (Chained == 0f) {
                Chained = 1f;
                int chainCount = 0;
                for (int i = 0; i < Main.maxNPCs && chainCount < 5; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 420f) {
                        nearby.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.Electrified, 240);
                        chainCount++;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = 0.25f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
                if (Projectile.owner == Main.myPlayer)
                    MyriadDemonRuneField.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 5f);
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 1.6f, owner: Projectile.owner);

            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(9f, 9f);
                Dust bolt = Dust.NewDustPerfect(target.Center, DustID.Electric, vel, 60, default, Main.rand.NextFloat(1.6f, 2.6f));
                bolt.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float windup = MathHelper.Clamp(Timer / WindupTime, 0f, 1f);

            // 双层带状拖尾 (更宽更亮)
            WeaponVFX.DrawProjectileTrail(Projectile, 26f,
                new Color(110, 40, 200), new Color(230, 170, 255), uvScroll: Timer * 0.03f);

            // 大符本体: 主贴图放大 + 双层辉光 + 电弧环
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float scale = 1.15f * MathHelper.Lerp(0.4f, 1f, windup);
            Color mainColor = Color.Lerp(lightColor, new Color(235, 190, 255), 0.55f) * MathHelper.Lerp(0.4f, 1f, windup);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Color glow = new Color(210, 110, 255) * (0.5f + windup * 0.4f);
                glow.A = 0;
                float pulse = (1.2f + MathF.Sin(Timer * 0.2f) * 0.18f) * scale;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, glow, 0f,
                    softGlow.Size() / 2f, pulse, SpriteEffects.None, 0);
            }

            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcIndex = (int)(Timer * 0.15f) % 4;
                int arcHeight = arcSheet.Height / 4;
                Rectangle sourceRect = new Rectangle(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                Vector2 arcOrigin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
                Color arcColor = new Color(220, 140, 255) * (0.4f + windup * 0.3f);
                arcColor.A = 0;
                Main.EntitySpriteDraw(arcSheet, Projectile.Center - Main.screenPosition, sourceRect, arcColor,
                    -Projectile.rotation, arcOrigin, 0.3f * scale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            for (int i = 0; i < 20; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f),
                    80, default, Main.rand.NextFloat(1.8f, 2.8f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 万魔法阵演出弹幕 (纯视觉, damage=0): 魔首大符命中瞬间在敌群中心展开 ArenaRunic 万魔法阵地纹,
    /// 并用 BeamGrad 向周围 NPC 拉出多目标电网。绘制只在 PreDraw, 命中阶段仅 <see cref="Spawn"/> 触发。
    /// </summary>
    public class MyriadDemonRuneField : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 44;
        private const float FieldRadius = 360f;
        private const float WebRange = 420f;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<MyriadDemonRuneField>(), 0, 0f, owner);
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

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.6f, 0.3f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)Life;            // 0→1
            float fade = MathHelper.Clamp(life < 0.2f ? life / 0.2f : 1f - (life - 0.2f) / 0.8f, 0f, 1f);
            Color primary = new Color(170, 110, 255);
            Color secondary = new Color(70, 30, 130);

            SpriteBatch sb = Main.spriteBatch;

            // —— ArenaRunic 万魔法阵地纹 (扩张 + 呼吸) ——
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                float radius = FieldRadius * (0.45f + life * 0.55f);
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float rFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(rFrac);
                fx.Parameters["uIntensity"]?.SetValue(fade * 0.85f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(14f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);

                sb.End();
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // —— BeamGrad 多目标电网: 中心 → 周围敌人 (最多 6 条) ——
            int web = 0;
            for (int i = 0; i < Main.maxNPCs && web < 6; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage)
                    continue;
                if (Vector2.Distance(Projectile.Center, npc.Center) > WebRange)
                    continue;
                ACMShaders.DrawBeam(Projectile.Center, npc.Center, 7f * fade,
                    new Color(220, 180, 255), new Color(120, 60, 220), fade * 0.9f,
                    flowSpeed: 3.2f, flowScale: 3f, coreSharp: 2.6f);
                web++;
            }

            // —— 中心核辉光 (峰值期申请全屏名额, 退化为柔光) ——
            if (fade > 0.4f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.08f, fade * 0.7f, new Color(160, 110, 245), 10f);

            return false;
        }
    }
}
