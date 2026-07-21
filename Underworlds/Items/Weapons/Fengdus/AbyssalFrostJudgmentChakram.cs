using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 极寒渊薮九幽判官轮 - 终极回旋刃
    /// 投掷后进入环绕轨道自动作战 (52 帧自动冲刺最近敌人);
    /// 轮在场时再次点击 = 敕令冲刺: 反拉蓄势后向鼠标方向瞬发直线突袭 (玩家决策点);
    /// 每 5 次命中触发"九幽判决": 轮驻停展开虚空裂口, 500px 冰冻 AOE + 全屏冰幕。
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

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 决策点: 场上已有轮 → 本次使用变为"敕令冲刺"指令, 不掷新轮 (场上限 1)
            if (player.ownedProjectileCounts[type] >= 1) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.type != type || p.owner != player.whoAmI)
                        continue;
                    // 仅轨道态可受令 (冲刺/判决中不打断); velocity 仅作方向承载, 由信号分支消费
                    if (p.ai[0] == 1f && p.ai[2] < 1f) {
                        p.ai[2] = 1f;
                        p.velocity = (Main.MouseWorld - p.Center).SafeNormalize(Vector2.UnitX) * 0.01f;
                        p.netUpdate = true;
                    }
                }
                return false;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            player.velocity -= velocity.SafeNormalize(Vector2.Zero) * 3f; // 掷出反冲
            return false;
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

    /// <summary>
    /// 判官轮弹体。ai[0]=状态, ai[1]=Timer, ai[2]=敕令冲刺信号 (1=收到, 冲刺结束清零)。
    /// 冲刺曲线: 6 帧 pow8 反拉 → 1 帧 snap 38 → 每帧 ×1.02 递增 16 帧 → ×0.62 硬刹 2 帧回轨道。
    /// </summary>
    public class FrostJudgmentChakramProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/AbyssalFrostJudgmentChakram";

        private enum ChakramState { Flying, Orbiting, Dashing, Returning, Judgment }
        private ChakramState State {
            get => (ChakramState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }
        private ref float Timer => ref Projectile.ai[1];
        private ref float DecreeSignal => ref Projectile.ai[2];
        private ref float HitCounter => ref Projectile.localAI[0];
        private ref float OrbitAngle => ref Projectile.localAI[1];

        private const float OrbitRadius = 120f;
        private const float OrbitSpeed = 0.06f;
        private const float DashSpeed = 38f;
        private const int DashCooldown = 52;
        private const float MaxFlyDistance = 600f;
        private const int WindupFrames = 6;                            // 反拉蓄势
        private const int BrakeStart = WindupFrames + 16;              // 冲刺 16 帧后硬刹
        private const int JudgmentHold = 26;                           // 九幽判决驻停

        private int dashCooldownTimer = 0;
        private Vector2 _dashDir = Vector2.UnitX;
        private int _snapFlash = 0; // snap 后 6 帧白紫残影

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
            if (_snapFlash > 0) _snapFlash--;
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
                case ChakramState.Judgment:
                    HandleJudgment(owner);
                    break;
            }

            SpawnFrostParticles();
            Lighting.AddLight(Projectile.Center, 0.55f, 0.4f, 1.15f);
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
            // 敕令信号须在轨道运动覆写 velocity 之前消费 (velocity 此刻是方向承载)
            if (DecreeSignal >= 1f) {
                StartDash(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                return;
            }

            OrbitAngle += OrbitSpeed;
            Vector2 targetPos = owner.Center + new Vector2(MathF.Cos(OrbitAngle), MathF.Sin(OrbitAngle)) * OrbitRadius;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.2f);
            Projectile.velocity = (targetPos - Projectile.Center) * 0.5f;

            dashCooldownTimer++;

            if (dashCooldownTimer >= DashCooldown) {
                NPC target = FindClosestNPC(700f);
                if (target != null)
                    StartDash((target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX));
                dashCooldownTimer = 0;
            }

            if (Timer > 600) {
                State = ChakramState.Returning;
                Timer = 0;
            }
        }

        /// <summary>进入冲刺 (敕令与自动共用): 方向定死不再转向, 直线才快。</summary>
        private void StartDash(Vector2 dir) {
            _dashDir = dir;
            State = ChakramState.Dashing;
            Timer = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;
        }

        private void HandleDashing(Player owner) {
            // Timer 1~6 反拉, 6 末帧 snap, 7~21 递增直冲, 22~23 硬刹, 24 回轨道
            if (Timer <= WindupFrames) {
                // 蓄势期锚点角冻结 (仍随玩家平移), 反拉方向不漂移
                Vector2 orbitPos = owner.Center + new Vector2(MathF.Cos(OrbitAngle), MathF.Sin(OrbitAngle)) * OrbitRadius;
                // pow8: 前 5 帧几乎不动, 末帧突然向后吸入 40px 的 counter-motion
                float pull = MathF.Pow(Timer / WindupFrames, 8f) * 40f;
                Projectile.Center = orbitPos - _dashDir * pull;
                Projectile.velocity = Vector2.Zero;

                if (Timer >= WindupFrames) {
                    Projectile.velocity = _dashDir * DashSpeed;
                    _snapFlash = 6;
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);
                }
                return;
            }

            if (Timer < BrakeStart) {
                Projectile.velocity *= 1.02f;
                return;
            }

            if (Timer < BrakeStart + 2) {
                Projectile.velocity *= 0.62f;
                return;
            }

            EndDash(owner);
        }

        private void EndDash(Player owner) {
            State = ChakramState.Orbiting;
            Timer = 0;
            dashCooldownTimer = 0;
            DecreeSignal = 0f;
            OrbitAngle = (Projectile.Center - owner.Center).ToRotation();
            Projectile.netUpdate = true;
        }

        private void HandleJudgment(Player owner) {
            Projectile.velocity = Vector2.Zero;
            if (Timer >= JudgmentHold)
                EndDash(owner);
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
            // 霜紫混尘: 冰/紫约各半, ≤6/帧
            for (int i = 0; i < 2; i++) {
                Dust frost = Dust.NewDustDirect(
                    Projectile.Center - Vector2.One * 20, 40, 40,
                    Main.rand.NextBool() ? DustID.IceTorch : DustID.PurpleTorch,
                    Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f,
                    80, default, Main.rand.NextFloat(1.2f, 2f));
                frost.noGravity = true;
            }
            if (State == ChakramState.Dashing && Timer > WindupFrames) {
                for (int i = 0; i < 3; i++) {
                    Dust ice = Dust.NewDustDirect(
                        Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                        4, 4, Main.rand.NextBool() ? DustID.FrostStaff : DustID.PurpleTorch,
                        -Projectile.velocity.X * 0.4f, -Projectile.velocity.Y * 0.4f,
                        60, default, Main.rand.NextFloat(1.5f, 2.5f));
                    ice.noGravity = true;
                }
            }
            if (State == ChakramState.Judgment) {
                // 裂口向心吸入尘
                for (int i = 0; i < 3; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * Main.rand.NextFloat(60f, 150f);
                    Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 5f;
                    Dust pull = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.PurpleTorch,
                        vel, 70, default, Main.rand.NextFloat(1.4f, 2.2f));
                    pull.noGravity = true;
                }
            }
            if (State == ChakramState.Orbiting && Main.rand.NextBool(3)) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center, 4, 4,
                    Main.rand.NextBool() ? DustID.BlueTorch : DustID.PurpleTorch,
                    0f, -0.5f, 80, default, 1.2f);
                trail.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 600);
            target.AddBuff(BuffID.Frozen, 60);
            target.AddBuff(BuffID.BrokenArmor, 600);

            HitCounter++;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center,
                    Main.rand.NextBool() ? DustID.FrostStaff : DustID.PurpleTorch, vel, 40, default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }

            // 驻停中不重复触发判决 (接触判定仍在, 防 26 帧内连环 AOE)
            if (HitCounter % 5 == 0 && State != ChakramState.Judgment) {
                // ===== 九幽判决: 驻停 26 帧, 裂口展开 + 500px 冰冻 AOE + 全屏冰幕 =====
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 1.2f, Pitch = 0.8f }, target.Center);

                // AOE 结算仅 owner 端 (多人安全)
                if (Main.myPlayer == Projectile.owner) {
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC nearby = Main.npc[i];
                        if (!nearby.CanBeChasedBy()) continue;
                        if (Vector2.Distance(target.Center, nearby.Center) < 500f) {
                            nearby.AddBuff(BuffID.Frozen, 120);
                            nearby.AddBuff(BuffID.Frostburn2, 600);
                            nearby.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                        }
                    }
                }

                // 全屏冰幕 (本武器签名全屏时刻)
                if (Main.myPlayer == Projectile.owner)
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<FrostJudgmentFlash>(), 0, 0f, Projectile.owner);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.FengduVoid, 2f, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 6f);

                for (int i = 0; i < 24; i++) {
                    float angle = MathHelper.TwoPi / 24f * i;
                    float radius = Main.rand.NextFloat(8f, 18f);
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Dust freeze = Dust.NewDustPerfect(target.Center,
                        i % 2 == 0 ? DustID.IceTorch : DustID.PurpleTorch, vel, 40, default, Main.rand.NextFloat(2.5f, 4f));
                    freeze.noGravity = true;
                }

                // 轮在命中点驻停展开裂口
                State = ChakramState.Judgment;
                Timer = 0;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
            }
            else {
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.AbyssPurple, 1f, Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 1.5f);

                if (State == ChakramState.Dashing && Timer < BrakeStart)
                    Timer = BrakeStart - 1; // 命中即入硬刹段 (2 帧 ×0.62 后回轨道)
            }

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = Main.rand.NextFloat(-0.15f, 0.15f) }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 霜紫轨道残影 (外深紫 + 内霜白): 九幽极寒是地府之寒
            WeaponVFX.DrawProjectileTrail(Projectile, 30f,
                new Color(50, 20, 110) * 0.9f, new Color(200, 225, 255),
                ACMAsset.GlaciateWave, uvScroll: 0.05f, subdivisions: 3);

            // 冲刺冰锋 (仅 snap 后的直线段)
            if (State == ChakramState.Dashing && Timer > WindupFrames) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                ACMShaders.DrawBeam(Projectile.Center - dir * 80f, Projectile.Center + dir * 36f, 18f,
                    new Color(210, 230, 255), FengduVFX.VoidMid, 0.9f,
                    flowSpeed: 2.6f, flowScale: 2.2f, coreSharp: 2.8f);
            }

            // snap 瞬间的白紫残影 (6 帧衰减加宽光束)
            if (_snapFlash > 0) {
                float f = _snapFlash / 6f;
                Vector2 dir = Projectile.velocity.SafeNormalize(_dashDir);
                ACMShaders.DrawBeam(Projectile.Center - dir * 150f * f, Projectile.Center + dir * 40f, 24f * f,
                    Color.White, FengduVFX.VoidBright, 0.85f * f,
                    flowSpeed: 3.2f, flowScale: 1.6f, coreSharp: 3.2f);
            }

            // 九幽判决: 虚空裂口随驻停帧扩张 30→150
            if (State == ChakramState.Judgment) {
                float jt = MathHelper.Clamp(Timer / JudgmentHold, 0f, 1f);
                float riftRadius = MathHelper.Lerp(30f, 150f, jt);
                FengduVFX.DrawVoidRift(Projectile.Center, riftRadius, 0.95f - jt * 0.35f, 0.55f, 0,
                    new Color(150, 180, 255), FengduVFX.SoulCyan, seed: Projectile.whoAmI * 0.137f);
            }

            // 旋转的判官符轮
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                float pulse = 0.34f + MathF.Sin(Timer * 0.15f) * 0.08f;
                Color starColor = new Color(190, 180, 255) * 0.55f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor, -Timer * 0.06f, starOrigin, pulse, SpriteEffects.None, 0);
            }

            // 霜紫晕 + 本体
            WeaponVFX.DrawGlowBurst(Projectile.Center, Projectile.scale * 1.3f, new Color(140, 150, 255) * 0.4f);
            Color mainColor = Color.Lerp(lightColor, new Color(205, 215, 255), 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 20; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    i % 2 == 0 ? DustID.IceTorch : DustID.PurpleTorch,
                    Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f),
                    60, default, Main.rand.NextFloat(1.5f, 2.5f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 九幽判决演出 (纯视觉, 本地客户端): ElementalScreenTint 霜紫冰幕 + RadialBloom 霜爆。
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

            // 冰幕染屏 (霜紫雾 + 深冰蓝压底)
            Effect tintFx = ACMShaders.ElementalScreenTint;
            if (tintFx != null) {
                ACMShaders.SetCommonParams(tintFx, Projectile.Center, life);
                tintFx.Parameters["uTint"]?.SetValue(new Vector4(new Color(150, 140, 230).ToVector3(), 0.33f * life));
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
