using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 极寒渊薮九幽判官轮 - 终极回旋刃
    /// 全新攻击模式：投掷后进入环绕玩家的自动战斗轨道
    /// 轨道中每40帧自动冲刺攻击最近的敌人，然后回到轨道
    /// 每5次命中触发"绝对零度审判"冻结范围内所有敌人
    /// </summary>
    public class AbyssalFrostJudgmentChakram : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 24800;
            Item.crit = 30;
            Item.DamageType = DamageClass.Melee;
            Item.width = 56;
            Item.height = 56;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 12f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<FrostJudgmentChakramProj>();
            Item.shootSpeed = 24f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<FrostJudgmentChakramProj>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<InfinityKarmaBlade>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class FrostJudgmentChakramProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/AbyssalFrostJudgmentChakram";

        private enum ChakramState { Flying, Orbiting, Dashing, Returning }
        private ChakramState State {
            get => (ChakramState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }
        private ref float Timer => ref Projectile.ai[1];
        private ref float HitCounter => ref Projectile.localAI[0];
        private ref float OrbitAngle => ref Projectile.localAI[1];

        private const float OrbitRadius = 120f;
        private const float OrbitSpeed = 0.06f;
        private const float DashSpeed = 35f;
        private const int DashCooldown = 40;
        private const float MaxFlyDistance = 600f;

        private int dashTarget = -1;
        private int dashCooldownTimer = 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || owner.GetItem().type != ModContent.ItemType<AbyssalFrostJudgmentChakram>()) { Projectile.Kill(); return; }

            Timer++;
            Projectile.rotation += 0.4f * (State == ChakramState.Dashing ? 2f : 1f);

            switch (State) {
                case ChakramState.Flying:
                    HandleFlying(owner);
                    break;
                case ChakramState.Orbiting:
                    HandleOrbiting(owner);
                    break;
                case ChakramState.Dashing:
                    HandleDashing(owner);
                    break;
                case ChakramState.Returning:
                    HandleReturning(owner);
                    break;
            }

            SpawnFrostParticles();
            Lighting.AddLight(Projectile.Center, 0.4f, 0.8f, 1.2f);
        }

        private void HandleFlying(Player owner) {
            Projectile.velocity *= 0.97f;
            float dist = Vector2.Distance(Projectile.Center, owner.Center);

            if (dist > MaxFlyDistance || Projectile.velocity.Length() < 3f || Timer > 40) {
                State = ChakramState.Orbiting;
                Timer = 0;
                dashCooldownTimer = 0;
                OrbitAngle = (Projectile.Center - owner.Center).ToRotation();
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.8f, Pitch = 0.5f }, Projectile.Center);
            }
        }

        private void HandleOrbiting(Player owner) {
            OrbitAngle += OrbitSpeed;
            Vector2 targetPos = owner.Center + new Vector2(MathF.Cos(OrbitAngle), MathF.Sin(OrbitAngle)) * OrbitRadius;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.2f);
            Projectile.velocity = (targetPos - Projectile.Center) * 0.5f;

            dashCooldownTimer++;

            if (dashCooldownTimer >= DashCooldown) {
                NPC target = FindClosestNPC(700f);
                if (target != null) {
                    dashTarget = target.whoAmI;
                    State = ChakramState.Dashing;
                    Timer = 0;
                    Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * DashSpeed;
                    SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.6f, Pitch = 0.8f }, Projectile.Center);
                }
                dashCooldownTimer = 0;
            }

            if (Timer > 600) {
                State = ChakramState.Returning;
                Timer = 0;
            }
        }

        private void HandleDashing(Player owner) {
            if (Timer > 20 || (dashTarget >= 0 && dashTarget < Main.maxNPCs && !Main.npc[dashTarget].active)) {
                State = ChakramState.Orbiting;
                Timer = 0;
                dashCooldownTimer = 0;
                OrbitAngle = (Projectile.Center - owner.Center).ToRotation();
                return;
            }

            if (dashTarget >= 0 && dashTarget < Main.maxNPCs && Main.npc[dashTarget].active) {
                Vector2 toTarget = (Main.npc[dashTarget].Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * DashSpeed, 0.15f);
            }
        }

        private void HandleReturning(Player owner) {
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float distance = toPlayer.Length();
            Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * 30f, 0.2f);
            if (distance < 40f) Projectile.Kill();
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        private void SpawnFrostParticles() {
            for (int i = 0; i < 2; i++) {
                Dust frost = Dust.NewDustDirect(
                    Projectile.Center - Vector2.One * 20, 40, 40, DustID.IceTorch,
                    Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f,
                    80, default, Main.rand.NextFloat(1.2f, 2f));
                frost.noGravity = true;
            }
            if (State == ChakramState.Dashing) {
                for (int i = 0; i < 3; i++) {
                    Dust ice = Dust.NewDustDirect(
                        Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                        4, 4, DustID.FrostStaff,
                        -Projectile.velocity.X * 0.4f, -Projectile.velocity.Y * 0.4f,
                        60, default, Main.rand.NextFloat(1.5f, 2.5f));
                    ice.noGravity = true;
                }
            }
            if (State == ChakramState.Orbiting && Main.rand.NextBool(3)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.BlueTorch,
                    0f, -0.5f, 80, default, 1.2f);
                trail.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 600);
            target.AddBuff(BuffID.Frozen, 60);
            target.AddBuff(BuffID.BrokenArmor, 600);

            HitCounter++;

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.Water, 1f, Projectile.owner);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.FrostStaff, vel, 40, default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }

            if (HitCounter % 5 == 0) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 1.2f, Pitch = 0.8f }, target.Center);

                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy()) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 500f) {
                        nearby.AddBuff(BuffID.Frozen, 120);
                        nearby.AddBuff(BuffID.Frostburn2, 600);
                        nearby.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                    }
                }

                // 绝对零度审判: 全屏冰幕 + 霜爆泛光 (本武器签名全屏时刻)
                if (Main.myPlayer == Projectile.owner)
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<FrostJudgmentFlash>(), 0, 0f, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 6f);

                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi / 24f * i;
                    float radius = Main.rand.NextFloat(8f, 18f);
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Dust freeze = Dust.NewDustPerfect(target.Center, DustID.IceTorch, vel, 40, default, Main.rand.NextFloat(2.5f, 4f));
                    freeze.noGravity = true;
                }
            }

            if (State == ChakramState.Dashing) {
                State = ChakramState.Orbiting;
                Timer = 0;
                dashCooldownTimer = 0;
                Player owner = Main.player[Projectile.owner];
                OrbitAngle = (Projectile.Center - owner.Center).ToRotation();
            }

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 冰蓝轨道残影 (双层 ribbon: 外宽深冰 + 内窄冰白) —— 公转时如一道判官冰轮的弧痕
            WeaponVFX.DrawProjectileTrail(Projectile, 30f,
                new Color(40, 120, 210) * 0.9f, new Color(210, 245, 255),
                ACMAsset.GlaciateWave, uvScroll: 0.05f, subdivisions: 3);

            // 冲刺时的 BeamGrad 冰锋 (沿速度方向的锐利冲刺轨迹)
            if (State == ChakramState.Dashing) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                ACMShaders.DrawBeam(Projectile.Center - dir * 80f, Projectile.Center + dir * 36f, 18f,
                    new Color(220, 245, 255), new Color(40, 110, 210), 0.9f,
                    flowSpeed: 2.6f, flowScale: 2.2f, coreSharp: 2.8f);
            }

            // 旋转的冰判符轮 (判官冰轮的符纹环)
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.34f + MathF.Sin(Timer * 0.15f) * 0.08f;
                Color starColor = new Color(170, 225, 255) * 0.55f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, -Timer * 0.06f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            // 冰晕 + 本体
            WeaponVFX.DrawGlowBurst(Projectile.Center, Projectile.scale * 1.3f, new Color(110, 190, 255) * 0.4f);
            Color mainColor = Color.Lerp(lightColor, new Color(200, 240, 255), 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 20; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch,
                    Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f),
                    60, default, Main.rand.NextFloat(1.5f, 2.5f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 绝对零度审判演出 (纯视觉, 本地客户端): ElementalScreenTint 冰幕 + RadialBloom 霜爆。
    /// </summary>
    public class FrostJudgmentFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 30;

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
        public override bool? CanDamage() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = MathHelper.Clamp(Projectile.timeLeft / (float)Life, 0f, 1f);

            // 冰幕染屏 (上=冰白雾, 下=深冰蓝压底)
            Effect tintFx = ACMShaders.ElementalScreenTint;
            if (tintFx != null) {
                ACMShaders.SetCommonParams(tintFx, Projectile.Center, life);
                tintFx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Frost.ToVector3(), 0.33f * life));
                tintFx.Parameters["uTint2"]?.SetValue(new Vector4(TelegraphColors.DeepFrost.ToVector3(), 0f));
                tintFx.Parameters["uVignette"]?.SetValue(0.46f);
                tintFx.Parameters["uFogScale"]?.SetValue(2.5f);
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawFullscreenOverlay(tintFx, BlendState.AlphaBlend);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 霜爆泛光 (向外炸开的冰白核)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.22f, life * 0.85f, TelegraphColors.IceWhite, 12f);
            return false;
        }
    }
}
