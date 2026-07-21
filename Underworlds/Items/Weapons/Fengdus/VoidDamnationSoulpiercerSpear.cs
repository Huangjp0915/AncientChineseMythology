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
    /// 虚空断罪永劫穿心矛 - 终极近战矛
    /// channel 连刺循环: 按住持续 后拉→穿刺→收矛, 每第 4 刺为"永劫穿心"大刺
    /// (蓄力更长/刺程 300/伤害 ×1.6/回血 ×2), 大刺沿突刺线撕开强化虚空裂隙。
    /// 命中无视防御 + 回血; 击杀触发魂碎溶解 + 600px 虚空连锁。
    /// </summary>
    public class VoidDamnationSoulpiercerSpear : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 22000;
            Item.crit = 22;
            Item.DamageType = DamageClass.Melee;
            Item.width = 70;
            Item.height = 70;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = null; // 音效全由 held proj 按节奏管理
            Item.autoReuse = true;
            Item.channel = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<VoidSoulpiercerProjectile>();
            Item.shootSpeed = 6f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<VoidSoulpiercerProjectile>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<OblivionSoulhook>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 穿心矛 held proj。ai[0]=阶段, ai[1]=阶段内 Timer, ai[2]=连刺序号 (0~3 循环, 3=永劫穿心大刺)。
    /// localAI[0]=当前矛身伸出距离。
    /// </summary>
    public class VoidSoulpiercerProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/VoidDamnationSoulpiercerSpear";

        private enum AttackStage { Prepare, Thrust, Retract }
        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set { Projectile.ai[0] = (float)value; Timer = 0; }
        }
        private ref float Timer => ref Projectile.ai[1];
        private ref float ThrustIndex => ref Projectile.ai[2];
        private ref float ThrustDistance => ref Projectile.localAI[0];

        private bool IsBigThrust => ThrustIndex >= 3f;
        private float MaxThrustDistance => IsBigThrust ? 300f : 180f;
        private float PullbackDistance => IsBigThrust ? 50f : 34f;
        private const float BaseOffset = 6f;
        private Player Owner => Main.player[Projectile.owner];
        // 大刺 Prepare ×1.5: 更长的抬手 = 大招可读前摇
        private float PrepareTime => (IsBigThrust ? 15f : 10f) / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 6f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 9f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        // 矛尖残影 (仅 Thrust 段记录, 本地视觉)
        private readonly Vector2[] tipTrail = new Vector2[6];
        private int tipTrailCount;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        // 位置每帧手动锚定到手部, velocity 只作瞄准方向的同步载体
        public override bool ShouldUpdatePosition() => false;

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) { Projectile.Kill(); return; }
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;
            Projectile.timeLeft = 120; // channel 循环期间不因超时消失

            switch (CurrentStage) {
                case AttackStage.Prepare: HandlePrepare(); break;
                case AttackStage.Thrust: HandleThrust(); break;
                case AttackStage.Retract: HandleRetract(); break;
            }

            UpdatePositionAndRotation();

            if (CurrentStage == AttackStage.Thrust) RecordTipTrail();
            else tipTrailCount = 0;

            SpawnVoidParticles();
            Lighting.AddLight(Projectile.Center, 0.3f, 0.18f, 0.55f);
            Timer++;
        }

        private void HandlePrepare() {
            // 渐加速后拉, 提前 2 帧到位 → 末 2 帧冻结 (微顿 = 吸气)
            float freezeSpan = MathF.Max(PrepareTime - 2f, 1f);
            float t = MathHelper.Clamp(Timer / freezeSpan, 0f, 1f);
            ThrustDistance = -PullbackDistance * MathF.Pow(t, 2.2f);

            // 大刺蓄力音: 音高随进度爬升
            if (IsBigThrust && (int)Timer % 5 == 0) {
                float cp = MathHelper.Clamp(Timer / PrepareTime, 0f, 1f);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.35f + cp * 0.35f, Pitch = -0.6f + cp * 1.1f }, Projectile.Center);
            }

            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f, Volume = 1.4f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
            }
        }

        private void HandleThrust() {
            // poly(10) ease-out: 首帧即走完约 8 成 → 爆发感
            float t = MathHelper.Clamp(Timer / ThrustTime, 0f, 1f);
            ThrustDistance = MathHelper.Lerp(-PullbackDistance, MaxThrustDistance, 1f - MathF.Pow(1f - t, 10f));

            if (Timer >= ThrustTime) {
                if (IsBigThrust) FireBigFinisher();
                CurrentStage = AttackStage.Retract;
            }
        }

        private void HandleRetract() {
            // quintic ease-in-out 收矛
            float t = MathHelper.Clamp(Timer / RetractTime, 0f, 1f);
            float ease = t < 0.5f ? 16f * t * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 5f) / 2f;
            ThrustDistance = MathHelper.Lerp(MaxThrustDistance, 0f, ease);

            if (Timer >= RetractTime) {
                if (Owner.channel) {
                    // 连刺循环: 序号 0→3 后归 0, 第 4 刺进入大招节奏
                    ThrustIndex = (ThrustIndex + 1f) % 4f;
                    CurrentStage = AttackStage.Prepare;
                    Projectile.netUpdate = true;
                } else {
                    Projectile.Kill();
                }
            }
        }

        /// <summary>大刺收招帧: 沿突刺线撕开强化虚空裂隙 (裂隙 = 大招专属奖励, 普通刺不生成)。</summary>
        private void FireBigFinisher() {
            WeaponVFX.AddScreenShake(Projectile.Center, 6f);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);

            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 tip = Owner.MountedCenter + direction * (BaseOffset + MaxThrustDistance + 90f);

            // 刺尖一次性金紫爆点 (≤40 预算内)
            for (int i = 0; i < 12; i++) {
                Vector2 vel = direction.RotatedByRandom(0.7f) * Main.rand.NextFloat(4f, 12f);
                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.PurpleTorch;
                Dust d = Dust.NewDustPerfect(tip, dustType, vel, 60, default, Main.rand.NextFloat(1.8f, 3f));
                d.noGravity = true;
            }

            if (Main.myPlayer == Projectile.owner) {
                Vector2 riftStart = Owner.MountedCenter + direction * BaseOffset;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), riftStart, direction,
                    ModContent.ProjectileType<VoidRiftLine>(), Projectile.damage / 3, 0f, Projectile.owner,
                    MaxThrustDistance + 90f, 0f, 1f); // ai[2]=1 强化标记
            }
        }

        private void UpdatePositionAndRotation() {
            // 仅 owner 读鼠标; 方向写入 velocity 同步给其他客户端
            if (Main.myPlayer == Projectile.owner) {
                Vector2 aim = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
                if ((aim - Projectile.velocity).LengthSquared() > 0.0004f) {
                    Projectile.velocity = aim;
                    Projectile.netUpdate = true;
                }
            }
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation();
            Projectile.spriteDirection = direction.X > 0 ? 1 : -1;
            Owner.direction = Projectile.spriteDirection;
            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
            Vector2 handPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);
            handPosition.Y += Owner.gfxOffY;
            Projectile.Center = handPosition + direction * (BaseOffset + ThrustDistance);
        }

        private void RecordTipTrail() {
            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() * 70f;
            for (int i = Math.Min(tipTrailCount, tipTrail.Length - 1); i > 0; i--)
                tipTrail[i] = tipTrail[i - 1];
            tipTrail[0] = tip;
            tipTrailCount = Math.Min(tipTrailCount + 1, tipTrail.Length);
        }

        private void SpawnVoidParticles() {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 tipPos = Projectile.Center + dir * 55f;

            if (CurrentStage == AttackStage.Thrust) {
                // 突刺尾焰: 3 紫蓝焰 + 2 暗影焰 (环境预算 ≤6/帧)
                for (int i = 0; i < 3; i++) {
                    int dustType = Main.rand.NextBool(3) ? DustID.BlueTorch : DustID.PurpleTorch;
                    Dust flame = Dust.NewDustDirect(
                        tipPos + Main.rand.NextVector2Circular(15, 15), 4, 4, dustType,
                        -dir.X * 4f + Main.rand.NextFloat(-2f, 2f),
                        -dir.Y * 4f + Main.rand.NextFloat(-2f, 2f),
                        80, default, Main.rand.NextFloat(2f, 3f));
                    flame.noGravity = true;
                }
                for (int i = 0; i < 2; i++) {
                    Dust crack = Dust.NewDustDirect(
                        tipPos + Main.rand.NextVector2Circular(20, 20), 4, 4, DustID.Shadowflame,
                        0f, -2f, 120, default, 2.5f);
                    crack.noGravity = true;
                }
            }
            else if (IsBigThrust && CurrentStage == AttackStage.Prepare) {
                // 大刺蓄力: 金尘向矛尖汇聚 (充能可读)
                for (int i = 0; i < 2; i++) {
                    Vector2 p = tipPos + Main.rand.NextVector2CircularEdge(42f, 42f);
                    Dust g = Dust.NewDustPerfect(p, DustID.GoldFlame, (tipPos - p) * 0.16f, 100, default, 1.3f);
                    g.noGravity = true;
                }
            }

            if (Main.rand.NextBool(2)) {
                Dust ambient = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15), 4, 4, DustID.Wraith,
                    0f, -1f, 100, default, 1.5f);
                ambient.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 大刺命中回血 ×2
            int healAmount = Main.rand.Next(30, 80);
            if (IsBigThrust) healAmount *= 2;
            Owner.Heal(healAmount);

            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.BrokenArmor, 600);
            target.AddBuff(BuffID.Slow, 600);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.FengduVoid, 1.1f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, IsBigThrust ? 2f : 1.5f);

            // 回魂尘: 魂青染色, 飞向持有者
            for (int i = 0; i < 10; i++) {
                Vector2 vel = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 16f);
                vel = vel.RotatedByRandom(MathHelper.ToRadians(35));
                Dust soul = Dust.NewDustPerfect(target.Center, DustID.IceTorch, vel, 60, FengduVFX.SoulCyan, Main.rand.NextFloat(2f, 3.5f));
                soul.noGravity = true;
            }

            if (target.life <= 0) {
                Owner.Heal(Main.rand.Next(50, 100));
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.5f }, target.Center);

                // 击杀: 虚空溶解魂碎演出 + 致命泛光
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.LethalRed, 1.8f, Projectile.owner);
                if (Main.myPlayer == Projectile.owner)
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<VoidSoulShatter>(), 0, 0f, Projectile.owner, target.rotation);

                // 连锁伤害仅 owner 端结算 (多人安全)
                if (Main.myPlayer == Projectile.owner) {
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC nearby = Main.npc[i];
                        if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                        if (Vector2.Distance(target.Center, nearby.Center) < 600f) {
                            nearby.SimpleStrikeNPC(damageDone, hit.HitDirection, false, 0f, null, false, 0, true);
                            nearby.AddBuff(BuffID.ShadowFlame, 600);
                        }
                    }
                }

                for (int i = 0; i < 20; i++) {
                    float angle = MathHelper.TwoPi / 20f * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(10f, 20f);
                    Dust ring = Dust.NewDustPerfect(target.Center, DustID.IceTorch, vel, 40, FengduVFX.SoulCyan, Main.rand.NextFloat(2.5f, 4f));
                    ring.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.2f + Main.rand.NextFloat(-0.15f, 0.15f) }, target.Center);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            modifiers.Defense.Flat -= target.defense;
            if (IsBigThrust) modifiers.FinalDamage *= 1.6f; // 永劫穿心
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 90f);
            float collisionPoint = 0f;
            float width = IsBigThrust ? 52f : 35f; // 大刺判定 ×1.5
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, width, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 90f);
            Utils.PlotTileLine(start, end, 35f, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() => CurrentStage == AttackStage.Thrust;

        public override bool PreDraw(ref Color lightColor) {
            // 残影在矛体之下
            if (CurrentStage == AttackStage.Thrust && tipTrailCount >= 2) {
                Vector2[] pts = new Vector2[tipTrailCount];
                Array.Copy(tipTrail, pts, tipTrailCount);
                WeaponVFX.DrawRibbonTrail(pts, IsBigThrust ? 30f : 22f,
                    new Color(60, 20, 110), FengduVFX.SoulCyan, ACMAsset.SoftGlow, uvScroll: 0.08f, subdivisions: 2);
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2;
            float rotationOffset;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) { rotationOffset = MathHelper.PiOver4; effects = SpriteEffects.None; }
            else { rotationOffset = MathHelper.Pi - MathHelper.PiOver4; effects = SpriteEffects.FlipHorizontally; }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            Vector2 dir = Projectile.rotation.ToRotationVector2();

            if (IsBigThrust && CurrentStage == AttackStage.Prepare) {
                // 大刺蓄力: 矛身金纹渐亮 + 高频微抖 (充能不稳定感)
                float charge = MathHelper.Clamp(Timer / PrepareTime, 0f, 1f);
                Vector2 jitter = Main.rand.NextVector2Circular(1.5f, 1.5f) * charge;
                Vector2 shaftBase = Projectile.Center - dir * 40f + jitter;
                Vector2 shaftTip = Projectile.Center + dir * 70f + jitter;
                ACMShaders.DrawBeam(shaftBase, shaftTip, MathHelper.Lerp(6f, 14f, charge),
                    FengduVFX.ImperialGoldHi, FengduVFX.VoidMid, 0.25f + 0.75f * charge,
                    flowSpeed: 3f, flowScale: 2.5f, coreSharp: 2.5f);
                WeaponVFX.DrawGlowBurst(shaftTip, 0.5f + charge * 0.9f, FengduVFX.ImperialGold * (0.3f + 0.5f * charge));
            }

            if (CurrentStage == AttackStage.Thrust) {
                Color glowColor = FengduVFX.VoidMid * 0.6f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.15f, effects, 0);

                // 魂青矛锋 (手柄根部→矛尖); 大刺加宽并叠金辉
                Vector2 tip = Projectile.Center + dir * 70f;
                Vector2 baseP = Projectile.Center - dir * 40f;
                ACMShaders.DrawBeam(baseP, tip, IsBigThrust ? 20f : 16f,
                    new Color(140, 230, 235), new Color(40, 50, 140), 0.9f,
                    flowSpeed: 2.4f, flowScale: 2.2f, coreSharp: 3f);
                WeaponVFX.DrawGlowBurst(tip, IsBigThrust ? 1.9f : 1.4f, FengduVFX.SoulCyan * 0.6f);
                if (IsBigThrust)
                    WeaponVFX.DrawGlowBurst(tip, 1.1f, FengduVFX.ImperialGoldHi * 0.4f);
            }
            return false;
        }
    }

    /// <summary>
    /// 虚空裂隙 - 大刺突刺路径上的持续伤害区域。
    /// ai[0]=RiftLength, ai[1]=Timer, ai[2]=强化标记 (1=永劫穿心裂隙: 更宽/更快命中/沿线脉冲牵引)。
    /// </summary>
    public class VoidRiftLine : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/VoidDamnationSoulpiercerSpear";
        private ref float RiftLength => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private bool Enhanced => Projectile.ai[2] >= 1f;
        private float LifeTime => Enhanced ? 90f : 120f;

        // 脉冲亮闪包络 (本地视觉)
        private float pulseFlash;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.alpha = 255;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Enhanced) {
                Projectile.timeLeft = 90;
                Projectile.localNPCHitCooldown = 14;
            }
        }

        public override void AI() {
            // 远端客户端 OnSpawn 不执行, 首帧从同步的 velocity 补推 rotation
            if (Timer == 0f && Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float opacity = MathHelper.Clamp(1f - Timer / LifeTime, 0f, 1f);
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            pulseFlash *= 0.88f;

            // 强化版每 30 帧沿线脉冲: 线上敌人被轻拖向线中心 (与奇点炸弹同款全端牵引)
            if (Enhanced && Timer > 0 && (int)Timer % 30 == 0) {
                pulseFlash = 1f;
                Vector2 lineCenter = Projectile.Center + dir * RiftLength * 0.5f;
                Vector2 lineEnd = Projectile.Center + dir * RiftLength;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.CanBeChasedBy()) continue;
                    float pt = 0f;
                    if (Collision.CheckAABBvLineCollision(npc.position, npc.Size, Projectile.Center, lineEnd, 70f, ref pt))
                        npc.velocity += (lineCenter - npc.Center).SafeNormalize(Vector2.Zero) * 2f;
                }
                for (int i = 0; i < 10; i++) {
                    float t = Main.rand.NextFloat();
                    Dust p = Dust.NewDustPerfect(Projectile.Center + dir * t * RiftLength, DustID.IceTorch,
                        Main.rand.NextVector2Circular(3f, 3f), 60, FengduVFX.SoulCyan, Main.rand.NextFloat(1.8f, 2.8f));
                    p.noGravity = true;
                }
            }

            int crackCount = (int)(RiftLength / 20f);
            for (int i = 0; i < Math.Min(crackCount / 3, 5); i++) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Projectile.Center + dir * t * RiftLength + Main.rand.NextVector2Circular(10, 10);
                int dustType = Main.rand.NextBool(3) ? DustID.BlueTorch : DustID.PurpleTorch;
                Dust crack = Dust.NewDustPerfect(pos, dustType,
                    Main.rand.NextVector2Circular(2f, 2f) + new Vector2(0, -1f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f) * opacity);
                crack.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Projectile.Center + dir * t * RiftLength;
                Dust shadow = Dust.NewDustPerfect(pos, DustID.Shadowflame,
                    new Vector2(0, -Main.rand.NextFloat(1f, 3f)), 120, default, 1.5f * opacity);
                shadow.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center + dir * RiftLength * 0.5f, 0.3f * opacity, 0.15f * opacity, 0.6f * opacity);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * RiftLength;
            float point = 0f;
            float width = Enhanced ? 35f : 25f; // 强化宽 ×1.4
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, width, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = MathHelper.Clamp(1f - Timer / LifeTime, 0f, 1f);
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 riftStart = Projectile.Center;
            Vector2 riftEnd = Projectile.Center + dir * RiftLength;

            // 强化版开幕: 刺尖终点撕开一次性虚空裂口 decal (前 30 帧)
            if (Enhanced && Timer < 30f) {
                float pop = MathHelper.Clamp(Timer / 5f, 0f, 1f); // 快速张开
                FengduVFX.DrawVoidRift(riftEnd, 90f * (0.55f + 0.45f * pop), 0.9f * (1f - Timer / 30f),
                    0.7f, 0, FengduVFX.VoidMid, FengduVFX.SoulCyan, seed: Projectile.whoAmI * 0.173f);
            }

            // BeamGrad 裂缝主体: 魂青芯 + 虚空紫缘, 脉冲时增亮
            float flicker = 0.8f + MathF.Sin(Timer * 0.35f) * 0.15f;
            float widthMul = Enhanced ? 1.4f : 1f;
            ACMShaders.DrawBeam(riftStart, riftEnd, MathHelper.Lerp(4f, 14f, opacity) * widthMul,
                new Color(160, 225, 235), new Color(40, 14, 90), opacity * flicker * (1f + pulseFlash * 0.7f),
                flowSpeed: 1.2f, flowScale: 3f, coreSharp: 2f);

            if (pulseFlash > 0.05f)
                WeaponVFX.DrawGlowBurst(Projectile.Center + dir * RiftLength * 0.5f,
                    1.6f * pulseFlash, FengduVFX.SoulCyan * (0.7f * pulseFlash));

            Texture2D lightningBranch = ACMAsset.LightningBranch;
            if (lightningBranch != null) {
                Vector2 origin = new Vector2(lightningBranch.Width / 2f, lightningBranch.Height);
                int segments = Math.Max(1, (int)(RiftLength / 100f));
                float segLen = RiftLength / segments;

                for (int s = 0; s < segments; s++) {
                    Vector2 segPos = Projectile.Center + dir * (s * segLen + segLen * 0.5f) - Main.screenPosition;
                    Color riftColor = Color.Lerp(FengduVFX.VoidMid, FengduVFX.SoulCyan, (float)s / segments) * opacity * 0.6f;
                    riftColor.A = 0;
                    float scaleX = 0.06f;
                    float scaleY = segLen / lightningBranch.Height * 1.2f;
                    float flickerOffset = MathF.Sin(Timer * 0.3f + s * 1.5f) * 0.02f;
                    Main.EntitySpriteDraw(lightningBranch, segPos, null, riftColor,
                        Projectile.rotation + MathHelper.PiOver2, origin, new Vector2(scaleX + flickerOffset, scaleY), SpriteEffects.None, 0);
                }
            }

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                for (int i = 0; i < 3; i++) {
                    float t = (i + 0.5f) / 3f;
                    Vector2 pos = Projectile.Center + dir * t * RiftLength - Main.screenPosition;
                    Color glow = Color.Lerp(FengduVFX.VoidMid, FengduVFX.SoulCyan, 0.35f) * opacity * 0.4f;
                    glow.A = 0;
                    float pulse = 0.6f + MathF.Sin(Timer * 0.2f + i) * 0.15f;
                    Main.EntitySpriteDraw(softGlow, pos, null, glow, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        // 签名时刻: GenericWarp rift 扭曲带 — 仅强化裂隙且仅开场 40 帧 (短暂定调契约), 普通裂隙不占全屏名额
        public override void PostDraw(Color lightColor) {
            if (Main.dedServ || Main.gameMenu || !Enhanced || Timer >= 40f)
                return;
            float warp = (1f - Timer / 40f) * 0.85f;
            if (warp < 0.05f || !ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;
            Vector2 mid = Projectile.Center + Projectile.rotation.ToRotationVector2() * RiftLength * 0.5f;
            ACMShaders.SetCommonParams(fx, mid, warp);
            fx.Parameters["uRadius"]?.SetValue(0.5f);
            fx.Parameters["uWarpScale"]?.SetValue(1.6f);
            fx.Parameters["uChroma"]?.SetValue(0.6f);
            fx.Parameters["uRadialPull"]?.SetValue(0.4f);
            fx.Parameters["uMode"]?.SetValue(3f); // rift 裂隙档
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.16f, 0.22f, 0.6f, 0.5f));
            ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, fx, bindNoise: true);
        }
    }

    /// <summary>
    /// 虚空魂碎 (纯视觉, 本地客户端): 用 DissolveBurn 把一枚虚空魂盘灼烧消融, 表现"穿心击杀魂飞魄散"。
    /// </summary>
    public class VoidSoulShatter : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 26;
        private float Spin => Projectile.ai[0];

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
            float threshold = 1f - life; // 0→1 逐渐溶解

            Texture2D disc = ACMAsset.SoftGlow;
            if (disc != null) {
                Vector2 origin = disc.Size() / 2f;
                Color baseCol = new Color(120, 90, 220) * 0.9f;
                baseCol.A = 0;
                WeaponVFX.ApplyDissolveBurn(disc, Projectile.Center, null, baseCol,
                    Spin, origin, 2.4f, threshold, life, new Color(170, 120, 255),
                    edgeWidth: 0.12f, noiseScale: 2.5f);
            }

            // 魂碎崩散冲击环
            float r = MathHelper.Lerp(10f, 110f, 1f - life);
            WeaponVFX.DrawShockwaveRing(Projectile.Center, r, 12f, life * 0.7f,
                new Color(190, 130, 255), new Color(40, 12, 80));
            return false;
        }
    }
}
