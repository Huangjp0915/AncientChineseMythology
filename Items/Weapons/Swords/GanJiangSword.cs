using AncientChineseMythology.Helpers;
using AncientChineseMythology.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Swords
{
    /// <summary>
    /// 干将剑 (系列旗舰) — 干将莫邪雌雄双剑, "双剑合璧"重做:
    /// 左键三连段 (正斩→逆斩→双剑十字旋斩), 每段前摇→poly(9) 爆发→收招, 影随莫邪虚剑补斩 (×0.35);
    /// 任意命中积攒"剑鸣"(上限 10, 剑柄光点广播)。
    /// 右键: 满 10 鸣 →"合鸣·雌雄合璧"大招 (交叉双巨剑气 + 阴阳双鱼盘 + 短暂染屏);
    /// 未满鸣 → 莫邪虚剑寻敌斩 (×0.8, 保留原右键 DNA)。
    /// 修复旧版 CanUseItem/Shoot/HoldItem 三处发射互相打架与挂机挂发问题。
    /// </summary>
    public class GanJiangSword : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/GanJiangSword";

        private int comboStep;      // 0 正斩 / 1 逆斩 / 2 十字旋斩
        private int comboIdleTimer;

        public override void SetDefaults() {
            Item.damage = 84;
            Item.crit = 24;
            Item.DamageType = DamageClass.Melee;
            Item.width = 68;
            Item.height = 68;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 0, 1, 4);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GanJiangSwordProj>();
            Item.shootSpeed = 1f;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.useTime = 34;
                Item.useAnimation = 34;
                // 大招/寻敌斩进行中不可再发
                return player.ownedProjectileCounts[ModContent.ProjectileType<GanJiangUnityRite>()] == 0
                    && player.ownedProjectileCounts[ModContent.ProjectileType<GanJiangSwordProj_2>()] == 0;
            }
            Item.useTime = 22;
            Item.useAnimation = 22;
            // 上一段未收招完不可再挥 (连段节奏由弹幕驱动)
            return player.ownedProjectileCounts[ModContent.ProjectileType<GanJiangSwordProj>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                var resonance = player.GetModPlayer<GanJiangResonancePlayer>();
                if (resonance.Resonance >= GanJiangResonancePlayer.MaxResonance) {
                    // 合鸣·雌雄合璧
                    resonance.Consume();
                    Projectile.NewProjectile(source, player.Center, velocity.SafeNormalize(Vector2.UnitX),
                        ModContent.ProjectileType<GanJiangUnityRite>(), damage, knockback, player.whoAmI);
                }
                else {
                    // 莫邪虚剑寻敌斩
                    int targetIdx = FindTargetNearCursor(player, 600f);
                    Projectile.NewProjectile(source, player.Center, velocity.SafeNormalize(Vector2.UnitX),
                        ModContent.ProjectileType<GanJiangSwordProj_2>(), (int)(damage * 0.8f), knockback,
                        player.whoAmI, 1f, targetIdx);
                }
                return false;
            }

            // 左键三连段
            int step = comboStep;
            float mult = step == 2 ? 1.3f : 1f;
            Projectile.NewProjectile(source, player.MountedCenter, velocity, type,
                (int)(damage * mult), knockback, player.whoAmI, step);
            comboStep = (comboStep + 1) % 3;
            comboIdleTimer = 0;
            return false;
        }

        private static int FindTargetNearCursor(Player player, float maxDist) {
            int best = -1;
            float bestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;
                float d = Vector2.Distance(Main.MouseWorld, npc.Center);
                if (d < bestDist && Vector2.Distance(player.Center, npc.Center) < 1100f) {
                    bestDist = d;
                    best = npc.whoAmI;
                }
            }
            return best;
        }

        public override void UpdateInventory(Player player) {
            comboIdleTimer = Math.Min(comboIdleTimer + 1, 75);
            if (comboIdleTimer >= 75)
                comboStep = 0;
        }
    }

    /// <summary>
    /// 剑鸣共鸣层 — 干将莫邪任意命中 +1 鸣 (上限 10), 满鸣解锁"合鸣·雌雄合璧"。
    /// owner 端权威 (近战命中在 owner 客户端结算), 仅本地手感层。
    /// </summary>
    public class GanJiangResonancePlayer : ModPlayer
    {
        public const int MaxResonance = 10;

        public int Resonance { get; private set; }

        public void AddResonance(int amount) {
            bool wasFull = Resonance >= MaxResonance;
            Resonance = Math.Clamp(Resonance + amount, 0, MaxResonance);
            // 满鸣瞬间广播: 双色合鸣光 + 清鸣
            if (!wasFull && Resonance >= MaxResonance && Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 0.9f }, Player.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 14; i++) {
                        Dust d = Dust.NewDustPerfect(Player.Center, Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch,
                            Main.rand.NextVector2Circular(4f, 4f), 100, default, Main.rand.NextFloat(1f, 1.5f));
                        d.noGravity = true;
                    }
                }
            }
        }

        public void Consume() => Resonance = 0;

        public override void UpdateDead() => Resonance = 0;

        public override void PostUpdate() {
            // 满鸣持有干将时的呼吸金光提示 (纯本地表现)
            if (Resonance >= MaxResonance && Player.HeldItem?.type == ModContent.ItemType<GanJiangSword>() && !Main.dedServ) {
                float breathe = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);
                Lighting.AddLight(Player.Center, 0.5f * breathe, 0.4f * breathe, 0.2f * breathe);
                if (Main.rand.NextBool(9)) {
                    Dust d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(20f, 26f),
                        Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch, new Vector2(0f, -1.2f), 140, default, 0.9f);
                    d.noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 合鸣·雌雄合璧 (大招控制器, 无伤害) — 前摇 25f: 双剑交叉高举、流光汇聚、末 6f 静默收束 →
    /// 爆发帧: 放出赤金×青蓝交叉双巨剑气 + 屏震 8 + 起爆 10f 染屏 (≤0.12, 走全屏名额) →
    /// 余韵: 交点展开阴阳双鱼盘 (GanJiangUnity.fx, 开→持→收)。
    /// </summary>
    public class GanJiangUnityRite : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/GanJiangSword";
        public override LocalizedText DisplayName => Language.GetOrRegister(
            "Mods.AncientChineseMythology.Projectiles.GanJiangUnityRite.DisplayName", () => "合鸣·雌雄合璧");

        private const int WindupTime = 25;
        private const int AfterTime = 62;   // 爆发后余韵 (决定盘的开合)
        private const int Life = WindupTime + AfterTime;

        private Player Owner => Main.player[Projectile.owner];
        private bool fired;
        private Vector2 fireAnchor; // 爆发帧的玩家位置 (盘心 = 锚点 + 方向×190, 方向迟到同步可自纠)
        private Vector2 DiscCenter => fireAnchor + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 190f;

        private int Age => Life - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            int age = Age;
            Projectile.Center = Owner.MountedCenter;

            if (age < WindupTime) {
                // 前摇锁手 (仪式感)
                Owner.itemAnimation = 2;
                Owner.itemTime = 2;
                Owner.heldProj = Projectile.whoAmI;

                // 流光汇聚 (末 6f 静默收束 — 爆发前的吸气)
                if (!Main.dedServ && age < WindupTime - 6 && Main.rand.NextBool(2)) {
                    Vector2 apex = Owner.MountedCenter + new Vector2(0f, -46f);
                    Dust d = Dust.NewDustPerfect(apex + Main.rand.NextVector2CircularEdge(120f, 120f) * Main.rand.NextFloat(0.5f, 1f),
                        Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch);
                    d.noGravity = true;
                    d.velocity = (apex - d.position) * 0.09f;
                    d.scale = Main.rand.NextFloat(1f, 1.5f);
                }
                if (age == WindupTime - 6)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f, Volume = 0.7f }, Owner.Center);
            }
            else if (!fired) {
                fired = true;
                Fire();
            }
        }

        /// <summary>爆发帧: owner 端放出双巨剑气; 各端记录盘心 + 屏震。</summary>
        private void Fire() {
            // 重新锚定瞄准 (前摇期间允许调整光标)
            if (Projectile.owner == Main.myPlayer) {
                Projectile.velocity = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
                Projectile.netUpdate = true;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            fireAnchor = Owner.MountedCenter;

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.15f, Volume = 1.1f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1f }, Owner.Center);
            WeaponVFX.AddScreenShake(Owner.Center, 8f);

            if (Projectile.owner == Main.myPlayer) {
                // 交叉双巨剑气: 两侧错位起步, 内倾角交汇 (各 ×2.5)
                int dmg = (int)(Projectile.damage * 2.5f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + perp * 30f,
                    dir.RotatedBy(-0.1f) * 17f, ModContent.ProjectileType<GanJiangUnityBlade>(),
                    dmg, Projectile.knockBack, Projectile.owner, 0f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter - perp * 30f,
                    dir.RotatedBy(0.1f) * 17f, ModContent.ProjectileType<GanJiangUnityBlade>(),
                    dmg, Projectile.knockBack, Projectile.owner, 1f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            SpriteBatch sb = Main.spriteBatch;

            if (age < WindupTime) {
                DrawCrossedBlades(age);
                return false;
            }

            int since = age - WindupTime;

            // 起爆 10f: 双色染屏定调 (≤0.12, 占全屏名额, 名额被占自动跳过)
            if (since < 10) {
                float tt = 1f - since / 10f;
                WeaponVFX.ApplyPaletteTint(sb,
                    shadowTint: new Color(40, 30, 60), highlightTint: new Color(255, 220, 150),
                    intensity: 0.12f * tt, saturation: 1.08f);
            }

            // 阴阳双鱼盘: 开 (12f) → 持 → 收 (末 16f)
            float open = MathHelper.Clamp(since / 12f, 0f, 1f);
            float close = MathHelper.Clamp((AfterTime - since) / 16f, 0f, 1f);
            float intensity = MathF.Pow(open, 0.6f) * close;
            if (intensity > 0.02f) {
                Vector2 discCenter = DiscCenter;
                Effect fx = WeaponVFX.GetEffect("GanJiangUnity");
                if (fx != null) {
                    ACMShaders.WorldDecalParams(discCenter, 150f, out Vector2 uvCenter, out float radiusFrac, out float aspect);
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uIntensity"]?.SetValue(intensity * 0.85f);
                    fx.Parameters["uCenter"]?.SetValue(uvCenter);
                    fx.Parameters["uRadius"]?.SetValue(radiusFrac * open);
                    fx.Parameters["uAspect"]?.SetValue(aspect);
                    fx.Parameters["uColorA"]?.SetValue(new Color(255, 175, 80).ToVector4());
                    fx.Parameters["uColorB"]?.SetValue(new Color(90, 200, 255).ToVector4());
                    fx.Parameters["uSpin"]?.SetValue(2.4f);

                    sb.End();
                    ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
                    ACMShaders.RestoreDefaultBatch(sb);
                }
                WeaponVFX.DrawGlowBurst(discCenter, 0.7f + open * 0.5f, new Color(255, 230, 180) * (intensity * 0.6f));
            }
            return false;
        }

        /// <summary>前摇: 双剑交叉高举, 由两侧汇拢 + 汇聚点辉光。</summary>
        private void DrawCrossedBlades(int age) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float t = age / (float)WindupTime;
            float rise = MathF.Pow(t, 0.7f);
            Vector2 apex = Owner.MountedCenter + new Vector2(0f, -30f - 18f * rise);
            float spread = MathHelper.Lerp(0.95f, 0.4f, rise); // 双剑收拢

            // 赤金干将 (左倾) + 青蓝莫邪 (右倾), 交叉高举; 贴图对角朝向补 -PiOver4, 原点=剑柄
            Color ganColor = new Color(255, 190, 120) { A = 80 };
            Color moyeColor = new Color(150, 220, 255) { A = 80 };
            float alpha = 0.45f + 0.5f * rise;
            Vector2 hilt = new(6f, tex.Height - 6f);
            Main.spriteBatch.Draw(tex, apex - Main.screenPosition, null, ganColor * alpha,
                -MathHelper.PiOver4 - spread, hilt, 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, apex - Main.screenPosition, null, moyeColor * alpha,
                -MathHelper.PiOver4 + spread, hilt, 1f, SpriteEffects.None, 0f);

            // 汇聚点辉光 (末 6f 收束变小 — 爆前收缩)
            float glowScale = age >= WindupTime - 6 ? MathHelper.Lerp(0.9f, 0.35f, (age - (WindupTime - 6)) / 6f) : 0.3f + 0.6f * rise;
            WeaponVFX.DrawGlowBurst(apex + new Vector2(0f, -14f), glowScale,
                Color.Lerp(new Color(255, 200, 120), new Color(160, 225, 255), 0.5f + 0.5f * MathF.Sin(age * 0.5f)) * (0.4f + 0.5f * rise));
        }
    }

    /// <summary>
    /// 合鸣巨剑气 — 雌雄一对交叉飞行的巨型剑气 (ai[0]=0 干将赤金 / 1 莫邪青蓝), 各 ×2.5 穿透。
    /// </summary>
    public class GanJiangUnityBlade : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Swords/GanJiangSword";
        public override LocalizedText DisplayName => Language.GetOrRegister(
            "Mods.AncientChineseMythology.Projectiles.GanJiangUnityBlade.DisplayName", () => "合鸣剑气");

        private bool IsMoye => Projectile.ai[0] >= 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 66;
            Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 55;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Projectile.velocity *= 1.012f; // 越飞越急

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    IsMoye ? DustID.IceTorch : DustID.GoldFlame, -Projectile.velocity * 0.1f, 120, default,
                    Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, IsMoye ? new Vector3(0.2f, 0.45f, 0.6f) : new Vector3(0.6f, 0.42f, 0.15f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                IsMoye ? ACMWeaponBurst.Gem : ACMWeaponBurst.Crimson, scale: 1.6f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 2.5f);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = IsMoye ? 0.2f : -0.2f, Volume = 0.9f }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Color outer = IsMoye ? new Color(40, 140, 220, 160) : new Color(215, 60, 25, 160);
            Color inner = IsMoye ? new Color(200, 245, 255, 210) : new Color(255, 225, 170, 210);

            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 26f, outer, inner,
                uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - dir * 52f, Projectile.Center + dir * 52f, 16f,
                inner, outer, 0.9f, coreSharp: 2.6f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Color body = IsMoye ? new Color(170, 230, 255) : new Color(255, 205, 140);
            body.A = 40;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, body,
                Projectile.rotation, tex.Size() * 0.5f, 1.15f, SpriteEffects.None, 0);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f, (IsMoye ? new Color(120, 210, 255) : new Color(255, 190, 110)) * 0.7f);
            return false;
        }
    }
}
