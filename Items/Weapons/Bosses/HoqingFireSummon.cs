using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bosses
{
    /// <summary>
    /// 劫骇 — 后卿掉落召唤杖。左键召唤"仪仗幽灯"(标准 minion) 绕玩家列队, 逐灯错拍吐魂焰;
    /// 右键在光标处开"小鬼门": 幽灯列队于门侧, 门内倾泻魂焰乱流后归位。
    /// 机制为后卿"幽火仪仗 / 鬼门开"的玩家化直译 (Docs/WeaponRedo/BossScatter.md §3.2)。
    /// 配色遵循后卿 V3 双色语言: 鬼绿=持续 / 腐橙=致命爆发。
    /// </summary>
    public class HoqingFireSummon : ModItem
    {
        public override void SetDefaults() {
            Item.width = 60;
            Item.height = 60;
            Item.damage = 140;
            Item.mana = 10;
            Item.useTime = Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 2f;
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<HoqingFireSummonProj>();
            Item.shootSpeed = 10f;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = ModContent.BuffType<HoqingFireSummonBuff>();
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 1, 60, 5);
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // 鬼门开: 需 ≥1 灯且场上无门
                Item.UseSound = SoundID.Item103 with { Pitch = -0.35f, Volume = 1f };
                return player.ownedProjectileCounts[ModContent.ProjectileType<HoqingFireSummonProj>()] > 0
                    && player.ownedProjectileCounts[ModContent.ProjectileType<HoqingFireSummonGate>()] == 0;
            }
            Item.UseSound = SoundID.Item113;
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                // 大招·鬼门开 (锁距 1200px); ai[1]=灯数决定倾泻量
                Vector2 target = Main.MouseWorld;
                Vector2 toTarget = target - player.Center;
                const float maxRange = 1200f;
                if (toTarget.Length() > maxRange)
                    target = player.Center + toTarget.SafeNormalize(Vector2.UnitX) * maxRange;
                int lanterns = player.ownedProjectileCounts[ModContent.ProjectileType<HoqingFireSummonProj>()];
                Projectile.NewProjectile(source, target, Vector2.Zero,
                    ModContent.ProjectileType<HoqingFireSummonGate>(), damage, knockback, player.whoAmI,
                    0f, lanterns);
                return false;
            }

            // 召唤仪仗幽灯 (标准 minion)
            player.AddBuff(Item.buffType, 2);
            var proj = Projectile.NewProjectileDirect(source, Main.MouseWorld, Vector2.Zero,
                type, damage, knockback, player.whoAmI);
            proj.originalDamage = damage; // 随召唤加成动态重算
            return false;
        }
    }

    /// <summary>仪仗幽灯 minion buff。</summary>
    public class HoqingFireSummonBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.CursedInferno;

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<HoqingFireSummonProj>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>劫骇主题色 (后卿 V3 语言: 鬼绿持续 / 腐橙爆发)。</summary>
    internal static class HoqingFireSummonVFX
    {
        public static readonly Color GhostGreen = new(110, 230, 150);
        public static readonly Color PaleGreen = new(190, 255, 205);
        public static readonly Color RotOrange = new(255, 130, 40);
        public static readonly Color DeepViolet = new(90, 60, 150);
    }

    /// <summary>
    /// 仪仗幽灯 (类名保留, 现为标准 minion) — 绕玩家等分列队, 错拍后拉前摇 → 吐魂焰 + 后坐;
    /// 场上有鬼门时改列于门侧停火。ai[0]=攻击计时。
    /// </summary>
    public class HoqingFireSummonProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";

        private int frame;
        private int recoil; // 发射后坐帧 (纯视觉)

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 18000;
        }

        public override bool? CanDamage() => false; // 灯体不撞人, 伤害全走魂焰弹
        public override bool MinionContactDamage() => false;

        private bool CheckActive(Player owner) {
            if (owner.dead || !owner.active) {
                owner.ClearBuff(ModContent.BuffType<HoqingFireSummonBuff>());
                return false;
            }
            if (owner.HasBuff(ModContent.BuffType<HoqingFireSummonBuff>()))
                Projectile.timeLeft = 2;
            return true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!CheckActive(owner))
                return;

            Projectile.localAI[0]++; // 年龄 (显形溶解)

            // 编队序号 (whoAmI 序, 各端一致) + 鬼门探测
            int index = 0, total = 0;
            Projectile gate = null;
            int gateType = ModContent.ProjectileType<HoqingFireSummonGate>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.owner != Projectile.owner)
                    continue;
                if (p.type == Type) {
                    if (p.whoAmI < Projectile.whoAmI)
                        index++;
                    total++;
                }
                else if (p.type == gateType) {
                    gate = p;
                }
            }
            total = Math.Max(total, 1);
            float time = Main.GlobalTimeWrappedHourly;

            Vector2 targetPos;
            if (gate != null) {
                // 鬼门列队: 竖椭圆轨道, 停普攻
                float ang = MathHelper.TwoPi * index / total + time * 1.4f;
                targetPos = gate.Center + new Vector2(MathF.Cos(ang) * 58f, MathF.Sin(ang) * 100f);
                Projectile.ai[0] = 0f;
            }
            else {
                // 仪仗环列队: 呼吸半径 + 灵异漂移
                float ang = MathHelper.TwoPi * index / total + time * 0.9f;
                float radius = 96f + 14f * MathF.Sin(time * 0.7f + index * 1.7f);
                Vector2 wobble = new(MathF.Sin(time * 2f + index * 2f) * 10f, MathF.Cos(time * 1.5f + index * 3f) * 16f);
                targetPos = owner.Center + ang.ToRotationVector2() * radius + wobble;

                // 攻击节拍: 错拍冷却 + 末 10f 后拉前摇
                NPC target = FindTarget(owner);
                int threshold = 52 + (index % 3) * 6;
                if (target != null) {
                    Projectile.ai[0]++;
                    float windup = MathHelper.Clamp((Projectile.ai[0] - (threshold - 10)) / 10f, 0f, 1f);
                    if (windup > 0f) {
                        Vector2 away = (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitY);
                        targetPos += away * MathF.Pow(windup, 2f) * 26f; // 吸气后拉
                    }
                    if (Projectile.ai[0] >= threshold) {
                        Projectile.ai[0] = 0f;
                        recoil = 8;
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.45f, Volume = 0.4f }, Projectile.Center);
                        if (Projectile.IsOwnedByLocalPlayer()) {
                            Vector2 vel = (target.Center + target.velocity * 6f - Projectile.Center)
                                .SafeNormalize(Vector2.UnitX) * 16f;
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                                ModContent.ProjectileType<HoqingFireSummonBolt>(), Projectile.damage,
                                Projectile.knockBack, Projectile.owner);
                        }
                    }
                }
                else if (Projectile.ai[0] > threshold - 10) {
                    Projectile.ai[0] = threshold - 10; // 无目标持满待发, 不做前摇抽搐
                }
            }

            if (recoil > 0)
                recoil--;

            // 平滑漂移 (远距瞬移追赶)
            if (Vector2.Distance(Projectile.Center, targetPos) > 1200f)
                Projectile.Center = targetPos;
            else
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.14f);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = 0f;
            Projectile.scale = 1f + 0.08f * MathF.Sin(time * 2.2f + index);

            VaultUtils.ClockFrame(ref frame, 5, 3);
            Lighting.AddLight(Projectile.Center, new Vector3(0.25f, 0.6f, 0.35f));
        }

        private NPC FindTarget(Player owner) {
            if (owner.HasMinionAttackTargetNPC) {
                NPC marked = Main.npc[owner.MinionAttackTargetNPC];
                if (marked.CanBeChasedBy())
                    return marked;
            }
            return Projectile.Center.FindClosestNPC(1100f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, frame, 4);
            Vector2 origin = rect.Size() / 2f;

            Color baseColor = Color.Lerp(HoqingFireSummonVFX.GhostGreen, HoqingFireSummonVFX.PaleGreen, 0.35f);
            float scale = Projectile.scale;

            // 召唤显形: 反向溶解 16f (鬼绿灼边)
            float age = Projectile.localAI[0];
            if (age < 16f) {
                float diss = 1f - age / 16f;
                WeaponVFX.ApplyDissolveBurn(tex, Projectile.Center, rect, baseColor,
                    Projectile.rotation, origin, scale, threshold: diss, intensity: 1f - diss * 0.3f,
                    edgeColor: new Color(120, 255, 160, 255), edgeWidth: 0.12f, noiseScale: 2.4f);
                return false;
            }

            // 双层 ribbon 拖尾 (鬼绿持续语言)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                outerColor: new Color(25, 90, 55, 140), innerColor: HoqingFireSummonVFX.PaleGreen with { A = 190 },
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            // 发射后坐闪烁
            float flash = recoil > 0 ? 0.35f * (recoil / 8f) : 0f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * (1f + flash), Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * (0.3f + flash), Projectile.rotation, origin, scale * 1.4f, SpriteEffects.None, 0f);
            if (flash > 0f)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.9f + flash * 2f, HoqingFireSummonVFX.PaleGreen * flash);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                    Main.rand.NextVector2Circular(3f, 3f), 130,
                    Color.Lerp(Color.Lime, Color.Cyan, Main.rand.NextFloat()), 1.6f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>魂焰弹 — 幽灯/鬼门吐出的追踪鬼火 (鬼绿轨迹, 腐橙爆发)。</summary>
    public class HoqingFireSummonBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 150;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            Projectile.ai[0]++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 出膛 10f 后弱追踪 (可甩开)
            if (Projectile.ai[0] > 10f) {
                NPC target = Projectile.Center.FindClosestNPC(760f);
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                        * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.035f);
                }
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                    -Projectile.velocity * 0.12f, 140,
                    Color.Lerp(Color.Lime, Color.Cyan, Main.rand.NextFloat()), 1.5f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.5f, 0.28f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Venom, 120); // 瘟疫之毒
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.1f, Volume = 0.5f }, target.Center);
            // 腐橙爆发 (致命层配色)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.CupriteBurn, scale: 0.85f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 13f,
                outerColor: new Color(25, 90, 55, 150), innerColor: HoqingFireSummonVFX.PaleGreen with { A = 200 },
                uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.8f, HoqingFireSummonVFX.GhostGreen * 0.85f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.42f, HoqingFireSummonVFX.PaleGreen * 0.9f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 120, default, 1.4f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 小鬼门 (大招) — 开门 36f → 倾泻 (灯数×3 发魂焰, 每 6f 一发) → 合拢 40f。
    /// 门体无判定; ai[0]=计时, ai[1]=灯数, ai[2]=状态 (0 开 / 1 泻 / 2 合)。
    /// </summary>
    public class HoqingFireSummonGate : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int OpenTime = 36;
        private const int CloseTime = 40;
        private const float RadX = 52f;
        private const float RadY = 96f;

        private int boltsFired;

        private float Timer => Projectile.ai[0];
        private int Lanterns => (int)Math.Max(Projectile.ai[1], 1f);
        private float State => Projectile.ai[2];
        private int TotalBolts => Lanterns * 3;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false; // 门体不判伤, 弹幕承载伤害
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
        }

        public override bool? CanDamage() => false;
        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.ai[0]++;

            switch (State) {
                case 0f: {
                    if (Timer == 1f)
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);
                    if (Timer >= OpenTime) {
                        Projectile.ai[2] = 1f;
                        Projectile.ai[0] = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                case 1f: {
                    // 倾泻: 每 6f 一发 (owner 端), 优先朝 1000px 内最近敌, 否则金角散射
                    if (Timer % 6 == 0 && boltsFired < TotalBolts) {
                        boltsFired++;
                        if (Timer % 12 == 0)
                            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.5f, Volume = 0.4f }, Projectile.Center);
                        if (Projectile.IsOwnedByLocalPlayer()) {
                            NPC target = Projectile.Center.FindClosestNPC(1000f);
                            Vector2 vel;
                            if (target != null) {
                                vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                                    .RotatedBy(Main.rand.NextFloat(-0.32f, 0.32f)) * 17f;
                            }
                            else {
                                vel = (boltsFired * 2.39996f).ToRotationVector2() * 17f; // 黄金角均匀散射
                            }
                            int dmg = (int)(Projectile.damage * 0.6f);
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                                ModContent.ProjectileType<HoqingFireSummonBolt>(), dmg, Projectile.knockBack,
                                Projectile.owner);
                        }
                    }
                    if (boltsFired >= TotalBolts && Timer >= TotalBolts * 6 + 20) {
                        Projectile.ai[2] = 2f;
                        Projectile.ai[0] = 0f;
                        Projectile.timeLeft = CloseTime;
                        Projectile.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.1f, Volume = 0.7f }, Projectile.Center);
                    }
                    break;
                }
            }

            // 门缝逸散鬼火 (节流)
            if (!Main.dedServ && State == 1f && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-RadX, RadX) * 0.4f, Main.rand.NextFloat(-RadY, RadY) * 0.8f),
                    DustID.GreenTorch, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, -0.5f)),
                    130, default, 1.5f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.3f, 0.7f, 0.42f));
        }

        /// <summary>开合插值 (0~1): 开门 easeOutCubic + 轻微过冲, 合拢反向。</summary>
        private float OpenScale() {
            if (State == 0f) {
                float t = MathHelper.Clamp(Timer / OpenTime, 0f, 1f);
                float e = 1f - MathF.Pow(1f - t, 3f);
                return e * (1f + 0.14f * MathF.Sin(t * MathHelper.Pi));
            }
            if (State == 2f)
                return MathF.Pow(MathHelper.Clamp(Projectile.timeLeft / (float)CloseTime, 0f, 1f), 2f);
            return 1f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float open = OpenScale();
            if (open <= 0.03f)
                return false;

            // 开门首 3 帧: rift 微扭曲定调 (走全屏名额契约, 名额被占自动跳过)
            if (State == 0f && Timer <= 3f && ACMShaders.RequestFullscreenSlot()) {
                Effect warp = ACMShaders.GenericWarp;
                if (warp != null) {
                    ACMShaders.SetCommonParams(warp, Projectile.Center, 0.3f);
                    warp.Parameters["uRadius"]?.SetValue(0.22f);
                    warp.Parameters["uWarpScale"]?.SetValue(0.9f);
                    warp.Parameters["uChroma"]?.SetValue(0.5f);
                    warp.Parameters["uRadialPull"]?.SetValue(0.35f);
                    warp.Parameters["uMode"]?.SetValue(3f); // rift
                    warp.Parameters["uTint"]?.SetValue(new Vector4(HoqingFireSummonVFX.GhostGreen.ToVector3(), 0.3f));
                    ACMShaders.ApplyScreenPostProcess(Main.spriteBatch, warp, bindNoise: true);
                }
            }

            // 竖椭圆门框: 双层 ribbon 闭环 (幽紫外 / 鬼绿内)
            const int segs = 22;
            var pts = new Vector2[segs + 1];
            float breathe = 1f + 0.03f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4f);
            for (int i = 0; i <= segs; i++) {
                float ang = MathHelper.TwoPi * i / segs;
                pts[i] = Projectile.Center + new Vector2(MathF.Cos(ang) * RadX, MathF.Sin(ang) * RadY) * open * breathe;
            }
            WeaponVFX.DrawRibbonTrail(pts, baseWidth: 12f * open,
                outerColor: HoqingFireSummonVFX.DeepViolet with { A = 150 },
                innerColor: HoqingFireSummonVFX.PaleGreen with { A = 210 },
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.8f, subdivisions: 1);

            // 中缝冥光 (竖直光缝, 倾泻期最亮)
            float slitBoost = State == 1f ? 1f : 0.6f;
            ACMShaders.DrawBeam(Projectile.Center - new Vector2(0f, RadY * open * 0.9f),
                Projectile.Center + new Vector2(0f, RadY * open * 0.9f),
                10f * open, HoqingFireSummonVFX.PaleGreen with { A = 200 },
                HoqingFireSummonVFX.GhostGreen with { A = 110 }, 0.75f * open * slitBoost,
                flowSpeed: 2.4f, coreSharp: 2.8f);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.6f * open, HoqingFireSummonVFX.GhostGreen * (0.45f * open));
            return false;
        }
    }
}
