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

            for (int i = 0; i < 20; i++) {
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

                for (int i = 0; i < 60; i++) {
                    float angle = MathHelper.TwoPi / 60f * i;
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

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(60, 120, 200), new Color(200, 240, 255), progress) * progress * 0.5f;
                trailColor.A = 0;
                float scale = Projectile.scale * progress;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0);
            }

            Color mainColor = Color.Lerp(lightColor, new Color(200, 240, 255), 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Texture2D glaciate = ACMAsset.GlaciateWave;
            if (glaciate != null && State == ChakramState.Dashing) {
                Vector2 gOrigin = glaciate.Size() / 2f;
                Color iceTrail = new Color(150, 220, 255) * 0.5f;
                iceTrail.A = 0;
                Main.EntitySpriteDraw(glaciate, Projectile.Center - Main.screenPosition, null, iceTrail, Projectile.velocity.ToRotation(), gOrigin, new Vector2(0.6f, 0.25f), SpriteEffects.None, 0);
            }

            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.3f + MathF.Sin(Timer * 0.15f) * 0.1f;
                Color starColor = new Color(180, 230, 255) * 0.6f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, Timer * 0.1f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            Color glowColor = new Color(120, 200, 255) * 0.3f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

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
}
