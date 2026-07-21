using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Spectres;
using AncientChineseMythology.Underworlds.Tiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Umbrals
{
    /// <summary>
    /// 魂灯杖 - 引导亡魂的灯笼法杖，召唤武器
    /// 肉后初期，召唤真正的"幽灯" minion <see cref="SoulLanternMinion"/>: 环绕玩家盘旋, 自动向点名/最近敌人喷射魂火弹。
    /// (原先仅生成原版 LostSoulFriendly, 并非真 minion —— 本次已补全为标准 minion 机制。)
    /// </summary>
    public class SoulLanternStaff : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults() {
            Item.damage = 35; //基础伤害
            Item.DamageType = DamageClass.Summon; //召唤伤害类型
            Item.mana = 10; //魔力消耗
            Item.width = 42; //物品宽度
            Item.height = 42; //物品高度
            Item.useTime = 30; //使用时间
            Item.useAnimation = 30; //使用动画时间
            Item.useStyle = ItemUseStyleID.Swing; //挥舞风格
            Item.knockBack = 2f; //击退
            Item.value = Item.buyPrice(gold: 5); //物品价值
            Item.rare = ItemRarityID.LightRed; //肉后稀有度
            Item.UseSound = SoundID.Item44; //召唤声音
            Item.autoReuse = false; //不自动连击
            Item.noMelee = true; //不造成近战伤害
            Item.shoot = ModContent.ProjectileType<SoulLanternMinion>(); //召唤幽灯 minion
            Item.shootSpeed = 8f; //弹幕速度
            Item.buffType = ModContent.BuffType<SoulLanternMinionBuff>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //刷新召唤 buff
            player.AddBuff(Item.buffType, 2);

            //在玩家附近召唤幽灯
            Vector2 spawnPos = player.Center + new Vector2(0, -40f);
            var minion = Projectile.NewProjectileDirect(source, spawnPos, Vector2.Zero, type, damage, knockback, player.whoAmI);
            minion.originalDamage = Item.damage;

            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(25).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>幽灯召唤 buff (维持 minion 存活)。</summary>
    public class SoulLanternMinionBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<SoulLanternMinion>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>
    /// 幽灯 minion - 环绕玩家盘旋, 点名/就近自动向敌人喷射魂火弹 <see cref="SoulLanternBolt"/>。
    /// 重做"引魂大火"：每第 6 发变为引魂大火（1.5 倍体积更亮, 命中裂为 3 枚寻敌小魂火）;
    /// 大火前灯芯渐盈可读, 开火带 3px 反冲（质量感）。灯体程序化绘制, 不依赖新 PNG。
    /// </summary>
    public class SoulLanternMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private ref float AttackCooldown => ref Projectile.localAI[0];
        /// <summary>已喷射发数（每第 6 发引魂大火）。localAI 各端独立推进, 大火标记随弹幕 ai 同步。</summary>
        private ref float ShotCounter => ref Projectile.localAI[1];
        private float pulsePhase;

        private const float FireRange = 760f;
        private const int FireRate = 38;
        private const int GreatFlameEvery = 6;

        public override void SetStaticDefaults() {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false; //本体不直接接触伤害, 靠喷射魂火弹

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            //玩家失效则结束
            if (!player.active || player.dead) {
                player.ClearBuff(ModContent.BuffType<SoulLanternMinionBuff>());
                Projectile.Kill();
                return;
            }
            if (player.HasBuff(ModContent.BuffType<SoulLanternMinionBuff>())) {
                Projectile.timeLeft = 2;
            }

            pulsePhase += 0.12f;
            if (AttackCooldown > 0)
                AttackCooldown--;

            //环绕玩家盘旋的目标位 (按 whoAmI 错开相位, 多灯不重叠)
            float phaseOffset = Projectile.whoAmI * 1.7f;
            float orbitAngle = Main.GlobalTimeWrappedHourly * 1.6f + phaseOffset;
            float orbitRadius = 78f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + phaseOffset) * 16f;
            Vector2 idlePos = player.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle) * 0.55f) * orbitRadius
                              + new Vector2(0, -30f);

            //寻找目标
            NPC target = FindTarget(player);

            //朝向: 有目标看向目标, 否则随飘动
            Vector2 toIdle = idlePos - Projectile.Center;
            float dist = toIdle.Length();
            //远离时收紧追随 (避免落后玩家)
            float followSpeed = MathHelper.Clamp(dist * 0.12f, 1.5f, 22f);
            if (dist > 2000f) {
                //传送回玩家 (防丢)
                Projectile.Center = player.Center + new Vector2(0, -40f);
                Projectile.velocity = Vector2.Zero;
            }
            else if (dist > 8f) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toIdle.SafeNormalize(Vector2.Zero) * followSpeed, 0.2f);
            }
            else {
                Projectile.velocity *= 0.85f;
            }

            //喷射魂火弹 (每第 GreatFlameEvery 发 → 引魂大火)
            if (target != null && AttackCooldown <= 0) {
                AttackCooldown = FireRate;
                ShotCounter++;
                bool great = ShotCounter >= GreatFlameEvery;
                if (great)
                    ShotCounter = 0;

                Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 vel = dir * (great ? 9.5f : 11f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        ModContent.ProjectileType<SoulLanternBolt>(), Projectile.damage, Projectile.knockBack,
                        Projectile.owner, 0f, 0f, great ? 1f : 0f);
                }

                //开火反冲 (质量感: 灯体被喷口顶回)
                Projectile.velocity -= dir * (great ? 5f : 3f);

                if (great)
                    SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                else
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.4f, Pitch = 0.25f + ShotCounter * 0.05f }, Projectile.Center);
            }

            //引魂大火将至: 灯芯渐盈粒子 (充能可读)
            if (ShotCounter >= GreatFlameEvery - 1 && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.GreenTorch, (Projectile.Center - Main.rand.NextVector2Circular(20f, 20f)) * 0.001f, 100, default, 1.1f);
                d.noGravity = true;
                d.velocity = (Projectile.Center - d.position) * 0.08f;
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.55f, 0.45f);
        }

        private NPC FindTarget(Player player) {
            //优先点名目标
            if (player.HasMinionAttackTargetNPC) {
                NPC targeted = Main.npc[player.MinionAttackTargetNPC];
                if (targeted.active && targeted.CanBeChasedBy() && !targeted.friendly &&
                    Vector2.Distance(targeted.Center, Projectile.Center) < FireRange * 1.5f) {
                    return targeted;
                }
            }
            NPC closest = null;
            float closestDist = FireRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly)
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            //引魂大火充能: 灯体渐盈变亮 (第 5 发后)
            float charged = ShotCounter >= GreatFlameEvery - 1 ? 0.5f + 0.2f * MathF.Sin(pulsePhase * 2.5f) : 0f;

            //灯体: 青黄魂火核 (SpectreHelper 青核 + 黄晕)
            SpectreHelper.DrawSpectreCore(Main.spriteBatch, Projectile.Center,
                SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow,
                scale: 0.55f + 0.05f * MathF.Sin(pulsePhase) + charged * 0.18f, pulsePhase: pulsePhase);

            //核心径向泛光 (走全屏名额, 多灯/同屏时自动退化为柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.04f + charged * 0.015f, 0.45f + charged * 0.3f,
                SpectreHelper.SpectreCyan, 6f);
            return false;
        }
    }

    /// <summary>
    /// 幽灯魂火弹 - 召唤伤害, 温和追踪敌人, 青黄魂火 ribbon 拖尾 + 核辉光。
    /// ai[2]=1 为引魂大火（1.5 倍体积更亮, 命中/消亡裂为 3 枚 30% 伤害寻敌小魂火）;
    /// ai[0]=1 为分裂小魂火（更小, 不再分裂）。
    /// </summary>
    public class SoulLanternBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private bool IsGreat => Projectile.ai[2] >= 1f;
        private bool IsSplitling => Projectile.ai[0] >= 1f;
        private bool _split; //防命中+消亡双重分裂

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.light = 0.4f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //体积语言: 大火 1.5x / 小魂火 0.6x
            Projectile.scale = IsGreat ? 1.5f : (IsSplitling ? 0.6f : 1f);

            //温和追踪 (小魂火追得更急 — "引魂"感)
            NPC target = FindTarget(560f);
            if (target != null) {
                float speed = Projectile.velocity.Length();
                if (speed < 1f)
                    speed = 11f;
                float steer = IsSplitling ? 0.16f : 0.08f;
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 dir = Vector2.Lerp(Projectile.velocity.SafeNormalize(Vector2.UnitX), toTarget, steer);
                Projectile.velocity = dir.SafeNormalize(Vector2.UnitX) * speed;
            }

            float lightMul = IsGreat ? 1.8f : 1f;
            Lighting.AddLight(Projectile.Center, 0.25f * lightMul, 0.45f * lightMul, 0.4f * lightMul);
            if (Main.rand.NextBool(IsGreat ? 2 : 4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                    Main.rand.NextVector2Circular(1f, 1f), 150, default, IsGreat ? 1.2f : 0.8f);
                d.noGravity = true;
            }
        }

        /// <summary>引魂大火命中/消亡: 裂为 3 枚 30% 伤害寻敌小魂火 (owner 端生成, 仅一次)。</summary>
        private void SplitGreatFlame() {
            if (!IsGreat || _split || Projectile.owner != Main.myPlayer)
                return;
            _split = true;
            int splitDamage = System.Math.Max(1, (int)(Projectile.damage * 0.3f));
            for (int i = 0; i < 3; i++) {
                Vector2 vel = (Projectile.velocity.SafeNormalize(Vector2.UnitX) * 7f)
                    .RotatedBy(MathHelper.ToRadians(-50f + 50f * i)) ;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    Type, splitDamage, Projectile.knockBack * 0.5f, Projectile.owner, 1f);
            }
        }

        private NPC FindTarget(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            Player player = Main.player[Projectile.owner];
            if (player.HasMinionAttackTargetNPC) {
                NPC t = Main.npc[player.MinionAttackTargetNPC];
                if (t.active && t.CanBeChasedBy() && !t.friendly &&
                    Vector2.Distance(t.Center, Projectile.Center) < maxDist * 1.5f)
                    return t;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly)
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SplitGreatFlame();
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.SoulFire, scale: IsGreat ? 1.2f : (IsSplitling ? 0.45f : 0.65f), owner: Projectile.owner);
            for (int i = 0; i < (IsGreat ? 6 : 3); i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenTorch,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 120, default, IsGreat ? 1.4f : 1f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //撞墙/超时也分裂 (大火价值不因脱靶归零)
            if (timeLeft > 0)
                SplitGreatFlame();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            //青黄魂火双层 ribbon 拖尾 (外青 + 内黄; 尺寸随体积语言)
            float widthMul = IsGreat ? 1.7f : (IsSplitling ? 0.55f : 1f);
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f * widthMul,
                outerColor: new Color(30, 120, 110, 150), innerColor: new Color(255, 220, 120, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            //核辉光
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.6f * widthMul, SpectreHelper.SpectreCyan);
            if (IsGreat)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.45f, SpectreHelper.SpectreYellow * 0.8f);
            return false;
        }
    }
}
