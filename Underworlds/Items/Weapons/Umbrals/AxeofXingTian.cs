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
    /// 刑天之斧 - 无首战神刑天的战斧（幽冥系列旗舰）。
    /// 重做：手持弹幕三段挥舞（前摇-爆发-收招重量曲线）；命中积攒怨气，
    /// 满怨气下一挥自动变为"无首怒斩"——放大重挥 + 释放怨气撕裂波（专属着色器 UmbralGrudgeWave）。
    /// 刑天不屈保留：低血增伤 + 破甲 + 演出狂暴化。
    /// </summary>
    public class AxeofXingTian : ModItem
    {
        /// <summary>怨气 0~100，命中积攒，满则下一挥怒斩（owner 端资源，弹幕生成走 Shoot 天然同步）。</summary>
        internal float wrath;
        /// <summary>挥舞交替侧（0=自上劈落 1=自下撩起）。</summary>
        internal int swingSide;

        public const float WrathMax = 100f;

        public override void SetDefaults() {
            Item.damage = 62;
            Item.crit = 6;
            Item.DamageType = DamageClass.Melee;
            Item.width = 56;
            Item.height = 56;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot; //手持弹幕承载挥舞
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(gold: 6);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = null; //音效由挥舞弹幕分层播放
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<XingTianAxeSwing>();
            Item.shootSpeed = 1f;
            Item.ArmorPenetration = 10;
        }

        internal static bool IsBerserk(Player player) => player.statLife < player.statLifeMax2 * 0.5f;

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<XingTianAxeSwing>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool wrathSwing = wrath >= WrathMax;
            if (wrathSwing)
                wrath = 0f;
            swingSide ^= 1;

            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.MountedCenter, dir, type, damage, knockback, player.whoAmI,
                wrathSwing ? 1f : 0f, swingSide);
            return false;
        }

        public override void HoldItem(Player player) {
            //怨气缓慢消散（鼓励攒满即用）
            if (wrath > 0f && wrath < WrathMax) {
                wrath -= 0.04f;
                if (wrath < 0f)
                    wrath = 0f;
            }

            //满怨气：斧上怨红火星环绕（可读的"已充能"广播）
            if (wrath >= WrathMax && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(player.Center + Main.rand.NextVector2Circular(34f, 34f), 0, 0,
                    DustID.Blood, 0f, -1.2f, 120, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            //刑天不屈：血量越低伤害越高（最高+30%）
            float healthRatio = (float)player.statLife / player.statLifeMax2;
            if (healthRatio < 0.5f) {
                damage += 0.3f * (1f - healthRatio * 2f);
            }
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient<SoulFragment>(6).AddIngredient<UmbralStoneItem>(22).AddTile(TileID.Anvils).Register();
        }
    }

    /// <summary>
    /// 刑天斧挥舞弹幕 - 三段重量曲线：前摇(二次in-out回拉) → 爆发(poly(14) 一瞬劈落) → 收招(五次方settle+后坐)。
    /// ai[0]=1 为怒斩（放大重挥 + 爆发帧释放怨气撕裂波）；ai[1]=挥舞侧。伤害窗口严格对齐爆发段。
    /// </summary>
    public class XingTianAxeSwing : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Umbrals/AxeofXingTian";

        private bool WrathSwing => Projectile.ai[0] >= 1f;
        private int Side => (int)Projectile.ai[1] == 0 ? 1 : -1;
        private ref float Timer => ref Projectile.localAI[0];

        private Player Owner => Main.player[Projectile.owner];

        //三段时长（帧, 除以攻速; 刑天不屈狂暴 +12% 挥速）——前摇长而可读, 爆发极短, 收招平滑 (MOTION.md §1)
        private float SpeedScale => Owner.GetTotalAttackSpeed(DamageClass.Melee) * (AxeofXingTian.IsBerserk(Owner) ? 1.12f : 1f);
        private float AnticTime => (WrathSwing ? 15f : 11f) / SpeedScale;
        private float StrikeTime => 5f / SpeedScale;
        private float RecoverTime => (WrathSwing ? 12f : 9f) / SpeedScale;

        //挥舞弧线（相对瞄准方向的偏角, 乘 Side 与 spriteDirection）
        private const float AnticStart = -1.05f;  //出手时已半举
        private const float AnticEnd = -2.05f;    //前摇拉满
        private const float StrikeEnd = 1.55f;    //爆发劈过
        private const float RecoverEnd = 1.1f;    //收招回收

        private float AxeScale => (WrathSwing ? 1.5f : 1.15f);
        private float BladeReach => 76f * AxeScale;

        //斧尖轨迹环形缓冲（拖尾只在爆发段绘制 — 速度门控修饰）
        private readonly Vector2[] _tipTrail = new Vector2[10];
        private int _tipCount;
        private bool _struck;          //已进入爆发段（音效/波仅一次）
        private bool _waveSpawned;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.XingTianAxeSwing.DisplayName",
                () => "Axe of Xing Tian");
        }

        public override void SetDefaults() {
            Projectile.width = 72;
            Projectile.height = 72;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一挥一判
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        /// <summary>当前挥舞偏角（三段曲线求值）。</summary>
        private float SwingOffset(out float phase01, out int act) {
            float t = Timer;
            if (t < AnticTime) {
                act = 0;
                phase01 = t / AnticTime;
                //二次 in-out 回拉：缓起缓止, 可读蓄势
                float e = phase01 < 0.5f ? 2f * phase01 * phase01 : 1f - MathF.Pow(-2f * phase01 + 2f, 2f) / 2f;
                return MathHelper.Lerp(AnticStart, AnticEnd, e);
            }
            t -= AnticTime;
            if (t < StrikeTime) {
                act = 1;
                phase01 = t / StrikeTime;
                //高次 ease-out：几乎全部角距离在最初几帧 — "劈"的一瞬 (怒斩更锐)
                float e = 1f - MathF.Pow(1f - phase01, WrathSwing ? 20f : 14f);
                return MathHelper.Lerp(AnticEnd, StrikeEnd, e);
            }
            act = 2;
            phase01 = MathHelper.Clamp((t - StrikeTime) / RecoverTime, 0f, 1f);
            //五次方 in-out settle
            float r = phase01 < 0.5f ? 16f * MathF.Pow(phase01, 5f) : 1f - MathF.Pow(-2f * phase01 + 2f, 5f) / 2f;
            return MathHelper.Lerp(StrikeEnd, RecoverEnd, r);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            float aim = Projectile.velocity.ToRotation();
            float offset = SwingOffset(out float phase, out int act);
            float worldAngle = aim + offset * Side * Projectile.spriteDirection;
            Projectile.rotation = worldAngle;
            Owner.direction = Projectile.spriteDirection;

            //手臂跟随
            float armRotation = worldAngle - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
            Vector2 handPos = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);
            handPos.Y += Owner.gfxOffY;
            Projectile.Center = handPos;

            Vector2 bladeDir = worldAngle.ToRotationVector2();
            Vector2 tip = handPos + bladeDir * BladeReach;

            //斧尖历史（环形缓冲, 无每帧分配）
            for (int i = _tipTrail.Length - 1; i > 0; i--)
                _tipTrail[i] = _tipTrail[i - 1];
            _tipTrail[0] = tip;
            if (_tipCount < _tipTrail.Length)
                _tipCount++;

            bool berserk = AxeofXingTian.IsBerserk(Owner);

            //爆发段进入帧：一瞬的声与撼 (冲击链: 帧-震-音)
            if (act >= 1 && !_struck) {
                _struck = true;
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = -0.1f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
                if (WrathSwing) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Owner.Center, 8f);
                }
                else {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = 0.1f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Owner.Center, berserk ? 3f : 2f);
                }
            }

            //怒斩：爆发中段释放怨气撕裂波（owner 端生成, 伤害随弹幕同步）
            if (WrathSwing && act == 1 && phase > 0.45f && !_waveSpawned) {
                _waveSpawned = true;
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 waveVel = aim.ToRotationVector2() * 13f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.MountedCenter + aim.ToRotationVector2() * 40f,
                        waveVel, ModContent.ProjectileType<XingTianWrathWave>(),
                        (int)(Projectile.damage * 0.9f), Projectile.knockBack, Projectile.owner);
                }
            }

            //收招后坐（仅 owner 端动自身速度）
            if (act == 2 && phase < 0.3f && Projectile.owner == Main.myPlayer) {
                Owner.velocity -= aim.ToRotationVector2() * (WrathSwing ? 0.55f : 0.22f);
            }

            //爆发段血怨火星沿刃 (粒子 ∝ 动能)
            if (act == 1 || (act == 2 && phase < 0.25f)) {
                int count = WrathSwing ? 3 : (berserk ? 2 : 1);
                for (int i = 0; i < count; i++) {
                    Vector2 dustPos = Vector2.Lerp(handPos, tip, Main.rand.NextFloat(0.45f, 1f));
                    Dust d = Dust.NewDustPerfect(dustPos,
                        WrathSwing || berserk ? DustID.Blood : DustID.Wraith,
                        bladeDir.RotatedBy(MathHelper.PiOver2 * Side * Projectile.spriteDirection) * Main.rand.NextFloat(2f, 6f),
                        110, default, Main.rand.NextFloat(1.1f, 1.6f));
                    d.noGravity = true;
                }
            }
            //前摇段: 斧刃聚怨微光 (蓄势可读)
            else if (act == 0 && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(8f, 8f), DustID.Shadowflame,
                    (handPos - tip) * 0.02f, 140, default, 0.9f);
                d.noGravity = true;
            }

            Lighting.AddLight(tip, WrathSwing ? 0.7f : 0.3f, WrathSwing ? 0.1f : 0.25f, WrathSwing ? 0.12f : 0.4f);

            Timer++;
            if (Timer >= AnticTime + StrikeTime + RecoverTime)
                Projectile.Kill();
        }

        public override bool? CanDamage() {
            //伤害窗口严格对齐爆发段（含收招头 2 帧的余势）
            SwingOffset(out float phase, out int act);
            return act == 1 || (act == 2 && Timer - AnticTime - StrikeTime < 2f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * BladeReach;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end,
                26f * AxeScale, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * BladeReach;
            Utils.PlotTileLine(start, end, 24f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool berserk = AxeofXingTian.IsBerserk(Owner);
            if (berserk)
                target.AddBuff(BuffID.Ichor, 120);

            //积攒怨气（owner 端资源）
            if (Owner.HeldItem?.ModItem is AxeofXingTian axe && !WrathSwing) {
                axe.wrath = MathHelper.Clamp(axe.wrath + (berserk ? 18f : 12f), 0f, AxeofXingTian.WrathMax);
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                WrathSwing || berserk ? ACMWeaponBurst.LethalRed : ACMWeaponBurst.SoulFire,
                scale: WrathSwing ? 1.6f : (berserk ? 1.3f : 1f), owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, WrathSwing ? 5f : (berserk ? 3.5f : 2.5f));
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = -0.3f + Main.rand.NextFloat(0.2f) }, target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            SwingOffset(out float phase, out int act);

            //爆发段+收招头几帧才画拖尾（门控修饰: 只在快的时刻表达快）
            if ((act == 1 || (act == 2 && phase < 0.3f)) && _tipCount >= 2) {
                int n = Math.Min(_tipCount, _tipTrail.Length);
                var pts = new Vector2[n];
                Array.Copy(_tipTrail, pts, n);
                Color outer = WrathSwing ? new Color(150, 20, 30, 160) : new Color(45, 70, 145, 140);
                Color inner = WrathSwing ? new Color(255, 90, 80, 210) : new Color(155, 215, 255, 190);
                WeaponVFX.DrawRibbonTrail(pts, (WrathSwing ? 30f : 20f) * AxeScale, outer, inner,
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
            }

            //斧体（手柄锚点挥舞式绘制）
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin;
            float drawRotation;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) {
                origin = new Vector2(4f, texture.Height - 4f);
                drawRotation = Projectile.rotation + MathHelper.PiOver4;
                effects = SpriteEffects.None;
            }
            else {
                origin = new Vector2(texture.Width - 4f, texture.Height - 4f);
                drawRotation = Projectile.rotation + MathHelper.Pi - MathHelper.PiOver4;
                effects = SpriteEffects.FlipHorizontally;
            }

            //怒斩蓄势: 前摇期斧体染怨红渐深
            Color axeColor = lightColor;
            if (WrathSwing) {
                float charge = act == 0 ? phase : 1f;
                axeColor = Color.Lerp(lightColor, new Color(255, 120, 110), 0.35f * charge);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, axeColor,
                drawRotation, origin, AxeScale, effects, 0);

            //怒斩爆发帧: 斧尖径向泛光 (名额满自动退化)
            if (WrathSwing && act == 1) {
                Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() * BladeReach;
                WeaponVFX.DrawRadialBloom(tip, 0.09f, 0.8f, new Color(255, 70, 60), 6f);
            }

            return false;
        }
    }

    /// <summary>
    /// 怨气撕裂波 - 刑天怒斩释放的前进弧形怨气波（旗舰大招时刻）。
    /// 视觉走专属着色器 UmbralGrudgeWave（屏幕空间弧带 decal, 不占全屏名额）;
    /// 起爆头 4 帧 PaletteLUT 血色染屏定调（占全屏名额, 短暂克制）。
    /// </summary>
    public class XingTianWrathWave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";

        private const int LifeTime = 42;
        private int LifeFrame => LifeTime - Projectile.timeLeft;
        /// <summary>波已行进距离（各端由同步的 velocity 本地积分, 仅供绘制）。</summary>
        private ref float Traveled => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.XingTianWrathWave.DisplayName",
                () => "Wrath of the Headless");
        }

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        public override void AI() {
            Traveled += Projectile.velocity.Length();
            Projectile.velocity *= 0.965f; //波前渐衰 (硬发射软收尾)
            Projectile.rotation = Projectile.velocity.ToRotation();

            //波前怨火粒子
            if (Main.rand.NextBool(2)) {
                Vector2 perp = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                Vector2 p = Projectile.Center + perp * Main.rand.NextFloat(-70f, 70f);
                Dust d = Dust.NewDustPerfect(p, Main.rand.NextBool() ? DustID.Blood : DustID.Shadowflame,
                    Projectile.velocity * 0.25f, 120, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.55f, 0.12f, 0.2f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //弧形波前 → 垂直于行进方向的弦线判定, 弦宽随波前扩张 (与着色器弧张角 ~sin(0.48) 对齐)
            Vector2 perp = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
            float halfSpan = MathHelper.Clamp((60f + Traveled) * 0.46f, 50f, 170f);
            Vector2 a = Projectile.Center - perp * halfSpan;
            Vector2 b = Projectile.Center + perp * halfSpan;
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a, b, 34f, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.LethalRed, scale: 1.1f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            float lifeT = LifeFrame / (float)LifeTime;      // 0→1
            float intensity = MathHelper.Clamp(lifeT < 0.12f ? lifeT / 0.12f : 1f - (lifeT - 0.12f) / 0.88f, 0f, 1f);

            //起爆头 4 帧: 血色染屏定格 (占全屏名额, ≤0.13 强度)
            if (LifeFrame < 4) {
                WeaponVFX.ApplyPaletteTint(sb,
                    shadowTint: new Color(45, 8, 14), highlightTint: new Color(255, 120, 110),
                    intensity: 0.13f * (1f - LifeFrame / 4f), saturation: 1.05f);
            }

            //弧形怨气撕裂波 (专属着色器; 波心=发波原点, 半径=已行进距离)
            Effect fx = WeaponVFX.GetEffect("UmbralGrudgeWave");
            float radiusPx = 60f + Traveled;
            Vector2 origin = Projectile.Center - Projectile.rotation.ToRotationVector2() * radiusPx;
            if (fx != null) {
                ACMShaders.WorldDecalParams(origin, radiusPx, out Vector2 uvCenter, out float radiusFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uvCenter);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(intensity);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uDirection"]?.SetValue(Projectile.rotation);
                fx.Parameters["uArcWidth"]?.SetValue(0.62f);
                fx.Parameters["uColorCore"]?.SetValue(new Color(255, 60, 55).ToVector4());
                fx.Parameters["uColorEdge"]?.SetValue(new Color(70, 100, 200).ToVector4());
                ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
            }
            else {
                //着色器缺失退化: 冲击环保证反馈
                WeaponVFX.DrawShockwaveRing(Projectile.Center, 50f, 22f, intensity,
                    new Color(255, 90, 80), new Color(60, 40, 140));
            }

            //波前柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.3f * intensity, new Color(230, 60, 60) * intensity);
            return false;
        }
    }
}
