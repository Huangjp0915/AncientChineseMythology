using AncientChineseMythology.Helpers;
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

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 阎罗锤 - 地府阎王的审判之锤，投掷回旋武器。
    /// 重做"惊堂一拍"：命中首个敌人或飞至最远点时，锤子悬停上抬(前摇) → 垂直下砸 →
    /// 拍案judgment：金色审判冲击 + 符环 + AoE 判决脉冲(附混乱)，然后回旋返回。
    /// </summary>
    public class YamasGavel : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 54; //基伤 -7% 换每投一次拍案 AoE (论证见 Docs/WeaponRedo/Umbrals.md §6)
            Item.crit = 4;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<YamasGavelProjectile>();
            Item.shootSpeed = 14f;
        }

        public override bool CanUseItem(Player player) {
            //一次一锤 (拍案节奏完整可读)
            return player.ownedProjectileCounts[ModContent.ProjectileType<YamasGavelProjectile>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            direction = direction.RotatedByRandom(MathHelper.ToRadians(2f));

            Projectile.NewProjectileDirect(source, player.Center + direction * 30f, direction * Item.shootSpeed,
                type, damage, knockback, player.whoAmI);

            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.2f }, player.Center);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 阎罗锤投射物 - 飞行 → (命中/最远点) 惊堂一拍状态机 → 返回。
    /// 拍案: 悬停上抬 10f (旋转对齐锤头朝下) → 3f 一瞬下砸 → 金色审判脉冲 → 回旋。
    /// </summary>
    public class YamasGavelProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Umbrals/YamasGavel";

        private enum GavelState { Flying, SlamRaise, SlamStrike, Returning }
        private GavelState State {
            get => (GavelState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Timer => ref Projectile.ai[1];
        private const float MaxDistance = 450f;
        private const float ReturnSpeed = 18f;
        private const int RaiseTime = 10;   //上抬前摇
        private const int StrikeTime = 4;   //一瞬下砸
        private const float RaiseHeight = 46f;
        private const float SlamDrop = 78f;

        private float spinSpeed = 0.3f;
        private Vector2 slamAnchor; //拍案锚点 (进入 SlamRaise 时的位置)

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Timer++;

            switch (State) {
                case GavelState.Flying:
                    HandleFlying(owner);
                    break;
                case GavelState.SlamRaise:
                    HandleSlamRaise();
                    break;
                case GavelState.SlamStrike:
                    HandleSlamStrike();
                    break;
                case GavelState.Returning:
                    HandleReturning(owner);
                    break;
            }

            SpawnFlameParticles();
            Lighting.AddLight(Projectile.Center, 1f, 0.7f, 0.2f);
        }

        private void BeginSlam() {
            State = GavelState.SlamRaise;
            Timer = 0;
            slamAnchor = Projectile.Center;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = MathHelper.WrapAngle(Projectile.rotation); //归一后指数收敛到正立
            Projectile.netUpdate = true;
            //提锤肃静: 高频小锣
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.4f }, Projectile.Center);
        }

        private void HandleFlying(Player owner) {
            Projectile.velocity *= 0.97f;
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.5f, 0.02f);
            Projectile.rotation += spinSpeed * Projectile.direction;

            //最远点/减速/超时 → 惊堂一拍
            float distanceToPlayer = Vector2.Distance(Projectile.Center, owner.Center);
            if (distanceToPlayer > MaxDistance || Projectile.velocity.Length() < 2f || Timer > 45) {
                BeginSlam();
            }
        }

        private void HandleSlamRaise() {
            //远端客户端可能经 netUpdate 直接进入本状态而未跑 BeginSlam → 惰性补锚点
            if (slamAnchor == Vector2.Zero) {
                slamAnchor = Projectile.Center;
                Projectile.rotation = MathHelper.WrapAngle(Projectile.rotation);
            }

            //上抬: 二次 in-out, 同时旋转减速对齐"锤头朝下" (可读前摇)
            float t = MathHelper.Clamp(Timer / RaiseTime, 0f, 1f);
            float e = t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
            Projectile.Center = slamAnchor + new Vector2(0f, -RaiseHeight * e);

            //旋转指数收敛到 0 (正立姿态)
            Projectile.rotation *= 0.72f;

            if (Timer >= RaiseTime) {
                State = GavelState.SlamStrike;
                Timer = 0;
                Projectile.netUpdate = true;
            }
        }

        private void HandleSlamStrike() {
            if (slamAnchor == Vector2.Zero)
                slamAnchor = Projectile.Center;

            //一瞬下砸: poly(6) ease-out (几乎全部行程在头两帧)
            float t = MathHelper.Clamp(Timer / StrikeTime, 0f, 1f);
            float e = 1f - MathF.Pow(1f - t, 6f);
            Projectile.Center = slamAnchor + new Vector2(0f, -RaiseHeight + (RaiseHeight + SlamDrop) * e);
            Projectile.rotation = 0f;

            if (Timer >= StrikeTime) {
                //—— 拍案! 惊堂金判决 ——
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.35f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.1f + Main.rand.NextFloat(0.15f) }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 5f);

                //金焰迸溅
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 14f),
                        DustID.GoldFlame, new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-6f, -1f)),
                        100, default, Main.rand.NextFloat(1.4f, 2.2f));
                    d.noGravity = true;
                }

                //判决脉冲 (0.9x AoE + 符环演出, owner 端生成)
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<YamasVerdictSlam>(), (int)(Projectile.damage * 0.9f),
                        Projectile.knockBack, Projectile.owner);
                }

                State = GavelState.Returning;
                Timer = 0;
                Projectile.netUpdate = true;
            }
        }

        private void HandleReturning(Player owner) {
            Projectile.rotation += MathHelper.Lerp(spinSpeed, 0.6f, 0.05f) * Projectile.direction;
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.6f, 0.05f);

            Vector2 toPlayer = owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);

            float returnSpeed = MathHelper.Lerp(ReturnSpeed, ReturnSpeed * 1.5f, 1f - distance / MaxDistance);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * returnSpeed, 0.15f);

            if (distance < 35f) {
                Projectile.Kill();
            }
        }

        private void SpawnFlameParticles() {
            //拍案前摇期收声敛气 (爆发前的静默 — 粒子刻意停掉)
            if (State == GavelState.SlamRaise)
                return;

            if (Main.rand.NextBool(2)) {
                Dust flame = Dust.NewDustDirect(Projectile.Center - Vector2.One * 16, 32, 32, DustID.Torch,
                    Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100, new Color(255, 200, 50),
                    Main.rand.NextFloat(1.5f, 2.2f));
                flame.noGravity = true;
            }

            if (Main.rand.NextBool(3)) {
                Dust hellfire = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(20, 20), 4, 4,
                    DustID.InfernoFork, 0, 0, 150, default, Main.rand.NextFloat(1.2f, 1.8f));
                hellfire.noGravity = true;
                hellfire.velocity = -Projectile.velocity * 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //阎罗审判：地狱火
            target.AddBuff(BuffID.OnFire, 180);

            for (int i = 0; i < 6; i++) {
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(8f, 8f), 100, default, Main.rand.NextFloat(1.8f, 2.5f));
                burst.noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 1.1f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 3.5f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f }, target.Center);

            //飞行中命中首个敌人 → 就地惊堂一拍
            if (State == GavelState.Flying) {
                BeginSlam();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == GavelState.Flying) {
                BeginSlam();
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f }, Projectile.position);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            //飞行/返回时的黄金烈焰拖尾 (拍案悬停期不画 — 静默前摇)
            if (State == GavelState.Flying || State == GavelState.Returning) {
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 26f,
                    outerColor: new Color(255, 90, 25, 150), innerColor: new Color(255, 225, 120, 200),
                    uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);
            }
            //下砸一瞬: 垂直速度线
            else if (State == GavelState.SlamStrike) {
                ACMShaders.DrawBeam(Projectile.Center + new Vector2(0f, -70f), Projectile.Center + new Vector2(0f, 10f),
                    halfWidth: 10f, core: new Color(255, 240, 190, 220), edge: new Color(200, 110, 20, 0),
                    intensity: 0.9f, flowSpeed: 3f, flowScale: 1.5f, coreSharp: 2.5f);
            }

            //拍案前摇: 锤体金光渐盈 (蓄势可读)
            Color mainColor = Color.Lerp(lightColor, new Color(255, 230, 150), 0.4f);
            if (State == GavelState.SlamRaise) {
                float t = MathHelper.Clamp(Timer / RaiseTime, 0f, 1f);
                mainColor = Color.Lerp(mainColor, new Color(255, 245, 200), t * 0.5f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f + t * 0.5f, new Color(255, 210, 90) * (0.35f + t * 0.45f));
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            //黄金光晕
            Color glowColor = new Color(255, 200, 50) * 0.4f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GoldFlame, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default,
                    Main.rand.NextFloat(1.2f, 1.8f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 阎罗审判符环 - 命中点短驻的程序化金色法阵 (纯视觉, ArenaRunic)。
    /// 重做后由 <see cref="YamasVerdictSlam"/> 承担拍案主演出, 本类保留 (public 类型不删除契约;
    /// 兼容旧存档/本地化键), 不再由锤主动生成。
    /// </summary>
    public class YamasJudgmentRune : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 34;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.6f, 0.45f, 0.12f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float life = 1f - Projectile.timeLeft / (float)LifeTime; // 0→1
            float intensity = MathF.Sin(life * MathHelper.Pi) * 0.9f;
            WeaponVFX.DrawShockwaveRing(Projectile.Center, 30f + life * 60f, 8f, intensity,
                new Color(255, 230, 140), new Color(200, 120, 30));
            return false;
        }
    }

    /// <summary>
    /// 判决脉冲 - 惊堂一拍的 AoE 判决：短暂扩张的金色审判领域, 命中附混乱。
    /// 伤害窗口(前 10f)与冲击环扩张严格对齐; ArenaRunic 金环铺地演出。
    /// </summary>
    public class YamasVerdictSlam : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 30;
        private const int DamageEnd = 10;
        private const float MaxRadius = 150f;

        private int LifeFrame => LifeTime - Projectile.timeLeft;
        private float RadiusNow => MaxRadius * (1f - MathF.Pow(1f - MathHelper.Clamp(LifeFrame / (float)DamageEnd, 0f, 1f), 4f));

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.YamasVerdictSlam.DisplayName",
                () => "Court Verdict");
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一拍一判
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.8f, 0.6f, 0.15f);
        }

        public override bool? CanDamage() => LifeFrame < DamageEnd;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //扩张环带判定 (与冲击环视觉对齐)
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < RadiusNow + 40f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //惊堂震慑: 混乱
            target.AddBuff(BuffID.Confused, 120);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 0.9f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float lifeT = LifeFrame / (float)LifeTime;
            float fade = 1f - lifeT;

            //审判金环 (ArenaRunic decal, 落点铺地; 着色器缺失退化为冲击环)
            Effect fx = ACMShaders.ArenaRunic;
            float intensity = MathF.Sin(MathHelper.Clamp(lifeT * 1.15f, 0f, 1f) * MathHelper.Pi) * 0.85f;
            if (fx != null && intensity > 0.01f) {
                ACMShaders.WorldDecalParams(Projectile.Center, MaxRadius * 1.05f, out Vector2 uv, out float radiusFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(intensity);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(new Color(255, 220, 120).ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(new Color(255, 120, 30).ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(10f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
            }

            //扩张冲击环 (伤害窗口内更亮 — 判定可读)
            float ringAlpha = LifeFrame < DamageEnd ? 0.95f : fade * 0.6f;
            WeaponVFX.DrawShockwaveRing(Projectile.Center, RadiusNow, 12f, ringAlpha,
                new Color(255, 235, 160), new Color(200, 110, 25));

            //拍点柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.2f * fade, new Color(255, 200, 90) * fade);
            return false;
        }
    }
}
