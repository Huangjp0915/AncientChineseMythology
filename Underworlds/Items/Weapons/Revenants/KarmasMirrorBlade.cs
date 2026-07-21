using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 孽镜回旋刃 - 如同孽镜台般映照罪孽、去而复返的利刃 (系列副旗舰, 回旋镖近战)。
    /// 飞行命中把伤害的 35% 蓄入镜中 (上限 300); 接刀后镜影 ≥180 时,
    /// 下一投为业镜断罪投: 主刃 + 2 道折影, 主刃首个命中一次性返还全部镜影 (+3 业)。
    /// </summary>
    public class KarmasMirrorBlade : ModItem
    {
        /// <summary>镜影蓄存 (owner 侧账本; 命中蓄入, 断罪投一次性返还)。</summary>
        internal int mirrorCharge;

        /// <summary>断罪投触发阈值 / 蓄存上限。</summary>
        public const int VerdictThreshold = 180;
        public const int ChargeCap = 300;

        public bool VerdictReady => mirrorCharge >= VerdictThreshold;

        public override void SetDefaults() {
            Item.damage = 58;
            Item.crit = 10;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<KarmasMirrorBladeProj>();
            Item.shootSpeed = 16f;
        }

        /// <summary>飞行刃回报镜影 (仅 owner 客户端调用)。</summary>
        internal void AddMirror(int damageDone) {
            mirrorCharge = Math.Min(mirrorCharge + (int)(damageDone * 0.35f), ChargeCap);
        }

        public override void HoldItem(Player player) {
            if (Main.dedServ || player.whoAmI != Main.myPlayer)
                return;
            // 蓄满预告: 手中镜刃泛紫红碎光 (决策点可读)
            if (VerdictReady && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(
                    player.MountedCenter + new Vector2(player.direction * 14f, -4f) + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.SilverCoin, new Vector2(0f, -1.2f), 90, new Color(255, 140, 170), 0.95f);
                d.noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            direction = direction.RotatedByRandom(MathHelper.ToRadians(2f));
            Vector2 spawnPos = player.Center + direction * 20f;
            Vector2 vel = direction * Item.shootSpeed;

            if (VerdictReady) {
                // —— 业镜断罪投: 主刃携带全部镜影 + 2 道折影平行刃 ——
                int charge = mirrorCharge;
                mirrorCharge = 0;
                Projectile.NewProjectile(source, spawnPos, vel, type, damage, knockback, player.whoAmI,
                    0f, 0f, charge);
                Vector2 perp = new(-direction.Y, direction.X);
                for (int s = -1; s <= 1; s += 2) {
                    Projectile.NewProjectile(source, spawnPos + perp * 30f * s, vel,
                        ModContent.ProjectileType<KarmaMirrorImage>(), (int)(damage * 0.4f),
                        knockback * 0.4f, player.whoAmI);
                }
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.2f }, spawnPos);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.3f }, spawnPos);
            }
            else {
                Projectile.NewProjectile(source, spawnPos, vel, type, damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<KarmasMirrorBladeProj>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<NetherBar>(), 8)
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 孽镜回旋刃弹幕 - 旋转飞出后返回。ai[2] > 0 为断罪投主刃 (携带镜影伤害,
    /// 首个命中一次性返还并盖 mini 判决印, +3 业); 普通投命中蓄影 (+1 业)。
    /// 刃迹走 RevenantKarmaRibbon (镜面拉丝 + 折影重像, 蓄影越多热度越高)。
    /// </summary>
    public class KarmasMirrorBladeProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/KarmasMirrorBlade";

        private enum BladeState { Flying, Returning }
        private BladeState State {
            get => (BladeState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Timer => ref Projectile.ai[1];
        /// <summary>断罪投携带的镜影伤害 (>0 即断罪主刃; 返还后清零)。</summary>
        private ref float StoredMirror => ref Projectile.ai[2];

        private const float MaxDistance = 500f;
        private const float ReturnSpeed = 20f;

        private bool IsVerdict => StoredMirror > 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Timer++;

            // 高速旋转 (断罪投更快, 读成"愤怒的镜")
            Projectile.rotation += (IsVerdict ? 0.5f : 0.35f) * Projectile.direction;

            switch (State) {
                case BladeState.Flying:
                    HandleFlying(owner);
                    break;
                case BladeState.Returning:
                    HandleReturning(owner);
                    break;
            }

            SpawnMirrorParticles();

            Lighting.AddLight(Projectile.Center, IsVerdict ? 0.8f : 0.6f, IsVerdict ? 0.4f : 0.5f, 0.7f);
        }

        private void HandleFlying(Player owner) {
            Projectile.velocity *= 0.97f;

            float distanceToPlayer = Vector2.Distance(Projectile.Center, owner.Center);
            if (distanceToPlayer > MaxDistance || Projectile.velocity.Length() < 2f || Timer > 40) {
                State = BladeState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void HandleReturning(Player owner) {
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);

            float returnSpeed = MathHelper.Lerp(ReturnSpeed, ReturnSpeed * 1.5f, 1f - distance / MaxDistance);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * returnSpeed, 0.15f);

            if (distance < 35f) {
                // 接刀反馈: 蓄影量以音高播报 (听得出这一刀喂了多少镜)
                if (Projectile.owner == Main.myPlayer && !Main.dedServ) {
                    var blade = owner.HeldItem?.ModItem as KarmasMirrorBlade;
                    float fill = blade == null ? 0f : MathHelper.Clamp(blade.mirrorCharge / (float)KarmasMirrorBlade.VerdictThreshold, 0f, 1f);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.45f, Pitch = -0.2f + fill * 0.5f }, owner.Center);
                    if (blade != null && blade.VerdictReady)
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.55f }, owner.Center);
                }
                Projectile.Kill();
            }
        }

        private void SpawnMirrorParticles() {
            // 镜面碎光粒子
            if (Main.rand.NextBool(2)) {
                Dust mirror = Dust.NewDustDirect(
                    Projectile.Center - Vector2.One * 12,
                    24, 24, DustID.SilverCoin,
                    Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f,
                    80, default, Main.rand.NextFloat(0.8f, 1.3f));
                mirror.noGravity = true;
            }

            // 断罪投: 朱红业焰缠绕
            if (IsVerdict && Main.rand.NextBool(2)) {
                Dust wrath = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                    4, 4, DustID.RedTorch, 0, 0, 110, default, Main.rand.NextFloat(0.9f, 1.4f));
                wrath.noGravity = true;
                wrath.velocity = -Projectile.velocity * 0.15f;
            }
            else if (Main.rand.NextBool(3)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                    4, 4, DustID.Shadowflame, 0, 0, 150, default, Main.rand.NextFloat(0.6f, 1.0f));
                shadow.noGravity = true;
                shadow.velocity = -Projectile.velocity * 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 150);

            if (IsVerdict) {
                // —— 断罪返还: 全部镜影一次性结算 (LethalRed), +3 业, mini 判决印 ——
                int payout = (int)StoredMirror;
                StoredMirror = 0f;
                Projectile.netUpdate = true;

                if (Projectile.owner == Main.myPlayer) {
                    target.SimpleStrikeNPC(payout, hit.HitDirection, true, 0f, null, false, 0, true);
                    // mini 判决印 (damage=0 纯视觉盖印, 规模 0.8)
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<KarmicVerdict>(), 0, 0f, Projectile.owner, 0f, 0.8f);
                }
                RevenantKarma.AddKarma(Projectile, target, 3);

                // 镜面碎裂演出: 银白碎片 + 朱红结算
                for (int i = 0; i < 16; i++) {
                    Dust shard = Dust.NewDustPerfect(target.Center, DustID.SilverCoin,
                        Main.rand.NextVector2CircularEdge(7f, 7f), 60, default, Main.rand.NextFloat(1.3f, 2.0f));
                    shard.noGravity = true;
                }
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.LethalRed, scale: 1.5f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 5f);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = -0.3f }, target.Center);
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.6f, Pitch = 0.1f }, target.Center);
            }
            else {
                // —— 普通投: 蓄影 + 记名 ——
                if (Projectile.owner == Main.myPlayer) {
                    var blade = Main.player[Projectile.owner].HeldItem?.ModItem as KarmasMirrorBlade;
                    blade?.AddMirror(damageDone);
                }
                RevenantKarma.AddKarma(Projectile, target, 1);

                for (int i = 0; i < 10; i++) {
                    Dust burst = Dust.NewDustPerfect(target.Center, DustID.SilverCoin,
                        Main.rand.NextVector2CircularEdge(6f, 6f), 60, default, Main.rand.NextFloat(1.2f, 1.8f));
                    burst.noGravity = true;
                }
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.AbyssPurple, scale: hit.Crit ? 1.15f : 0.85f, owner: Projectile.owner);
                SoundEngine.PlaySound(SoundID.Item10 with {
                    Volume = 0.5f, Pitch = 0.4f + Main.rand.NextFloat(-0.1f, 0.1f)
                }, target.Center);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == BladeState.Flying) {
                State = BladeState.Returning;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f }, Projectile.position);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 镜面刃迹 (RevenantKarmaRibbon: 拉丝 + 折影; 断罪投高热 + 强折影)
            var pts = new System.Collections.Generic.List<Vector2>(Projectile.oldPos.Length);
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                pts.Add(Projectile.oldPos[i] + half);
            }
            if (pts.Count >= 2) {
                RevenantRibbonVFX.DrawKarmaRibbon(pts.ToArray(), 18f,
                    core: IsVerdict ? new Color(255, 200, 190, 235) : new Color(230, 230, 255, 210),
                    edge: IsVerdict ? new Color(170, 30, 60, 160) : new Color(110, 75, 175, 150),
                    intensity: 0.9f,
                    heat: IsVerdict ? 0.85f : 0.12f,
                    ghost: IsVerdict ? 0.7f : 0.35f,
                    uvScroll: Main.GlobalTimeWrappedHourly * 1.6f);
            }

            // 绘制主体 (断罪投泛朱)
            Color mainColor = Color.Lerp(lightColor, IsVerdict ? new Color(255, 210, 210) : new Color(230, 220, 255), 0.35f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            // BlankStar 镜面星光
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.28f + MathF.Sin(Timer * 0.15f) * 0.08f;
                Color starColor = (IsVerdict ? new Color(255, 170, 170) : new Color(205, 205, 255)) * 0.5f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, Timer * 0.1f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            // 镜面光晕
            Color glowColor = (IsVerdict ? new Color(240, 150, 150) : new Color(180, 170, 220)) * 0.32f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.SilverCoin,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    80, default, Main.rand.NextFloat(0.8f, 1.3f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 业镜折影 - 断罪投时随主刃平行飞出的镜中之像 (0.4×, 不返回, 穿透后碎散)。
    /// 半透明重影绘制 + 高 ghost 刃迹, 命中 +1 业。
    /// </summary>
    public class KarmaMirrorImage : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/KarmasMirrorBlade";

        private ref float Timer => ref Projectile.ai[0];
        private const int Life = 50;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = Life;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.5f * Projectile.direction;
            Projectile.velocity *= 0.985f;

            Lighting.AddLight(Projectile.Center, 0.35f, 0.25f, 0.5f);

            if (Main.rand.NextBool(3)) {
                Dust glint = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.SilverCoin,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    120, default, 0.8f);
                glint.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            RevenantKarma.AddKarma(Projectile, target, 1);
            target.AddBuff(BuffID.Ichor, 90);

            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.SilverCoin,
                    Main.rand.NextVector2Circular(4f, 4f), 80, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 0.65f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            float fade = MathHelper.Clamp(1f - Timer / Life, 0f, 1f);

            // 高折影刃迹 (镜中之像的语言)
            var pts = new System.Collections.Generic.List<Vector2>(Projectile.oldPos.Length);
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                pts.Add(Projectile.oldPos[i] + half);
            }
            if (pts.Count >= 2) {
                RevenantRibbonVFX.DrawKarmaRibbon(pts.ToArray(), 13f,
                    core: new Color(200, 180, 255, 200), edge: new Color(90, 60, 160, 130),
                    intensity: 0.7f * fade, heat: 0f, ghost: 0.9f,
                    uvScroll: Main.GlobalTimeWrappedHourly * 1.4f);
            }

            // 半透明重影本体 (双像错位, 读成"镜像")
            Color ghostCol = new Color(190, 180, 255) * (0.55f * fade);
            ghostCol.A = 0;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, drawPos + new Vector2(3f, 0f), null, ghostCol * 0.6f,
                Projectile.rotation + 0.15f, origin, Projectile.scale * 0.95f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, ghostCol,
                Projectile.rotation, origin, Projectile.scale * 0.95f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 镜像碎散
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SilverCoin,
                    Main.rand.NextVector2Circular(3f, 3f), 80, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.5f }, Projectile.Center);
        }
    }
}
