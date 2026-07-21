using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 酆都万劫寂灭黑帝刀 - 系列旗舰近战大刀。
    /// channel 手持弹幕三连击: 下劈/上撩放黑紫金芯刀气扇, 第三击"万劫寂灭诏"甩出大刀气
    /// 并在光标处召出虚空漩涡 (帝诏垂落 + 裂口收口)。
    /// 非 Boss 敌人 &lt;25% 血处决; 连锁审判仅第三击暴击触发。
    /// </summary>
    public class CelestialImperatorGreatblade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 12800;
            Item.crit = 28;
            Item.DamageType = DamageClass.Melee;
            Item.width = 90;
            Item.height = 90;
            Item.useTime = 20;              // 仅控制起手, 连击节奏由手持弹幕自管
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 16f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = null;           // 音效由手持弹幕分层管理 (前摇 whoosh / 爆发帧)
            Item.autoReuse = true;
            Item.channel = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<ImperatorBladeHeld>();
            Item.shootSpeed = 1f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<ImperatorBladeHeld>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<YamasDeicide>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 黑帝刀手持弹幕 - 三连击波形载体。
    /// ai[0]=combo (0 下劈 / 1 上撩 / 2 过顶重劈), ai[1]=段内计时, ai[2]=瞄准角 (owner 写入并同步)。
    /// 波形: 前摇 42% quadratic in-out 背摆 → 爆发 12% 1-(1-t)^16 极锐甩出 (唯一伤害窗)
    /// → 收招 46% quintic in-out 回正。按住循环, 松开在当前击收招完毕后收刀。
    /// </summary>
    public class ImperatorBladeHeld : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/CelestialImperatorGreatblade";
        public override LocalizedText DisplayName => Language.GetOrRegister(
            "Mods.AncientChineseMythology.Projectiles.ImperatorBladeHeld.DisplayName", () => "万劫寂灭黑帝刀");

        private const float WindupFrac = 0.42f;
        private const float BurstFrac = 0.12f;
        private const float SwingArc = 1.5f;      // 爆发甩至 +1.5 rad
        private const int TrailLen = 12;

        private Player Owner => Main.player[Projectile.owner];
        private ref float ComboStep => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float AimAngle => ref Projectile.ai[2];

        private bool IsThird => (int)ComboStep == 2;
        /// <summary>第 1/3 击向下劈, 第 2 击向上撩。</summary>
        private float StrikeSign => (int)ComboStep == 1 ? -1f : 1f;
        private float BackAmt => IsThird ? 1.35f : 1f;
        private float AtkSpeed => Math.Clamp(Owner.GetTotalAttackSpeed(Projectile.DamageType), 0.4f, 3f);
        private float Period => 42f / AtkSpeed * (IsThird ? 1.35f : 1f);
        private float CurT => MathHelper.Clamp(Timer / Period, 0f, 1f);
        private bool InBurst => CurT >= WindupFrac && CurT < WindupFrac + BurstFrac;
        private float TipReach => 112f * Projectile.scale;

        // 一次性事件与残影为各端本地视觉状态; gameplay 状态全在 ai[]
        private readonly Vector2[] _tipTrail = new Vector2[TrailLen];
        private int _tipCount;
        private bool _whooshPlayed, _burstFired;
        private int _lastCombo = -1;
        private float _prevTimer;

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
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void OnSpawn(IEntitySource source) {
            AimAngle = Projectile.velocity.ToRotation();
            Projectile.velocity = Vector2.Zero;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60; // 常驻手持, 存亡由连击循环决定
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Projectile.scale = Owner.GetAdjustedItemScale(Owner.HeldItem);

            // 段切换 (本地推进或 net 同步, 容忍小幅回拨抖动) → 复位一次性事件与残影
            if ((int)ComboStep != _lastCombo || Timer < _prevTimer - 4f) {
                _lastCombo = (int)ComboStep;
                _whooshPlayed = false;
                _burstFired = false;
                _tipCount = 0;
            }
            _prevTimer = Timer;

            float period = Period;
            float t = CurT;

            // 前摇期跟手重瞄 (鼠标仅 owner 读取)
            if (Projectile.owner == Main.myPlayer && t < WindupFrac) {
                float aim = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
                if (MathF.Abs(MathHelper.WrapAngle(aim - AimAngle)) > 0.02f) {
                    AimAngle = aim;
                    Projectile.netUpdate = true;
                }
            }

            Projectile.spriteDirection = MathF.Cos(AimAngle) >= 0f ? 1 : -1;
            Owner.ChangeDir(Projectile.spriteDirection);
            Projectile.rotation = AimAngle + SwingOffset(t) * Projectile.spriteDirection * StrikeSign;

            // 刀锚在手
            float armRot = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Vector2 hand = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRot);
            hand.Y += Owner.gfxOffY;
            Projectile.Center = hand;

            // 前摇 70% 低鸣 (蓄势可读)
            if (!_whooshPlayed && t >= WindupFrac * 0.7f) {
                _whooshPlayed = true;
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Pitch = IsThird ? -0.55f : -0.4f,
                    Volume = IsThird ? 1.1f : 0.85f
                }, Owner.Center);
            }
            // 爆发首帧: 刀气/漩涡生成 + 重音 (帧跳/攻速漂移下用标志保证单次)
            if (!_burstFired && t >= WindupFrac) {
                _burstFired = true;
                FireBurst();
            }

            // 刀尖历史只记爆发附近 (残影速度门控: 前摇/收招尾不显形)
            if (t >= WindupFrac && t < WindupFrac + BurstFrac + 0.12f) {
                for (int i = TrailLen - 1; i > 0; i--)
                    _tipTrail[i] = _tipTrail[i - 1];
                _tipTrail[0] = hand + Projectile.rotation.ToRotationVector2() * TipReach;
                _tipCount = Math.Min(_tipCount + 1, TrailLen);
            }

            SpawnSwingDust(t, hand);
            Vector2 mid = hand + Projectile.rotation.ToRotationVector2() * TipReach * 0.6f;
            float glow = InBurst ? 1f : 0.45f;
            Lighting.AddLight(mid, 0.5f * glow, 0.25f * glow, 0.9f * glow);
            if (IsThird)
                Lighting.AddLight(mid, 0.35f * glow, 0.28f * glow, 0.1f * glow);

            Timer++;

            if (Timer >= period) {
                // 续段/收刀决策仅 owner 做, 远端钳住等同步 (防连击段错位)
                if (Projectile.owner == Main.myPlayer) {
                    if (!Owner.channel) {
                        Projectile.Kill();
                        return;
                    }
                    ComboStep = ((int)ComboStep + 1) % 3;
                    Timer = 0f;
                    AimAngle = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
                    Projectile.netUpdate = true;
                }
                else {
                    Timer = period;
                }
            }
        }

        /// <summary>手感核心曲线: 背摆 0→-BackAmt → 极锐甩至 +1.5 → 回正。</summary>
        private float SwingOffset(float t) {
            if (t < WindupFrac) {
                float p = t / WindupFrac;
                float e = p < 0.5f ? 2f * p * p : 1f - MathF.Pow(-2f * p + 2f, 2f) / 2f;
                return -BackAmt * e;
            }
            if (t < WindupFrac + BurstFrac) {
                float p = (t - WindupFrac) / BurstFrac;
                return MathHelper.Lerp(-BackAmt, SwingArc, 1f - MathF.Pow(1f - p, 16f));
            }
            float q = MathHelper.Clamp((t - WindupFrac - BurstFrac) / (1f - WindupFrac - BurstFrac), 0f, 1f);
            float e2 = q < 0.5f ? 16f * q * q * q * q * q : 1f - MathF.Pow(-2f * q + 2f, 5f) / 2f;
            return MathHelper.Lerp(SwingArc, 0f, e2);
        }

        /// <summary>爆发帧事件: 音效/屏震各端自播; 弹幕仅 owner 端生成。</summary>
        private void FireBurst() {
            if (IsThird) {
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.25f, Pitch = -0.25f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.9f }, Owner.Center);
                WeaponVFX.AddScreenShake(Owner.Center, 5f);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Volume = 1f,
                    Pitch = Main.rand.NextFloat(-0.1f, 0.1f)
                }, Owner.Center);
            }

            if (!Main.dedServ) {
                Vector2 fxDir = AimAngle.ToRotationVector2();
                int n = IsThird ? 16 : 10;
                for (int i = 0; i < n; i++) {
                    Vector2 vel = fxDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, IsThird ? 14f : 10f);
                    Dust d = Dust.NewDustPerfect(Owner.MountedCenter + fxDir * 40f,
                        Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.PurpleTorch,
                        vel, 60, default, Main.rand.NextFloat(1.8f, 2.8f));
                    d.noGravity = true;
                }
                if (IsThird) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 vel = fxDir.RotatedByRandom(0.9f) * Main.rand.NextFloat(3f, 9f);
                        Dust g = Dust.NewDustPerfect(Owner.MountedCenter + fxDir * 40f,
                            DustID.GoldFlame, vel, 40, default, Main.rand.NextFloat(1.6f, 2.4f));
                        g.noGravity = true;
                    }
                }
            }

            if (Projectile.owner != Main.myPlayer)
                return;

            var source = Projectile.GetSource_FromThis();
            Vector2 aimDir = AimAngle.ToRotationVector2();
            int slashType = ModContent.ProjectileType<ImperatorSlash>();

            if (!IsThird) {
                // 第 1/2 击: ±8° 三连刀气扇
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = aimDir.RotatedBy(MathHelper.ToRadians(8f * i)) * 22f;
                    Projectile.NewProjectile(source, Owner.MountedCenter + aimDir * 50f, vel, slashType,
                        (int)(Projectile.damage * 1.4f), Projectile.knockBack * 0.5f, Projectile.owner);
                }
            }
            else {
                // 万劫寂灭诏: 大刀气 + 光标处虚空漩涡 (限距 500px)
                Projectile.NewProjectile(source, Owner.MountedCenter + aimDir * 50f, aimDir * 22f, slashType,
                    (int)(Projectile.damage * 2.2f), Projectile.knockBack, Projectile.owner, 1.5f);

                Vector2 anchor = Main.MouseWorld;
                Vector2 offset = anchor - Owner.MountedCenter;
                if (offset.Length() > 500f)
                    anchor = Owner.MountedCenter + offset.SafeNormalize(Vector2.UnitX) * 500f;
                Projectile.NewProjectile(source, anchor, Vector2.Zero,
                    ModContent.ProjectileType<ImperatorVoidEruption>(),
                    (int)(Projectile.damage * 2.5f), Projectile.knockBack * 2f, Projectile.owner);
            }
        }

        private void SpawnSwingDust(float t, Vector2 hand) {
            if (Main.dedServ)
                return;
            Vector2 bladeDir = Projectile.rotation.ToRotationVector2();
            if (InBurst) {
                // 沿挥动切向抛洒 (方向 = 角速度符号)
                Vector2 tangent = bladeDir.RotatedBy(MathHelper.PiOver2 * Projectile.spriteDirection * StrikeSign);
                for (int i = 0; i < 4; i++) {
                    Vector2 pos = hand + bladeDir * TipReach * Main.rand.NextFloat(0.35f, 1f);
                    Dust d = Dust.NewDustPerfect(pos,
                        Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.PurpleTorch,
                        tangent * Main.rand.NextFloat(2f, 6f), 70, default, Main.rand.NextFloat(1.5f, 2.4f));
                    d.noGravity = true;
                }
                if (IsThird) {
                    for (int i = 0; i < 2; i++) {
                        Dust g = Dust.NewDustPerfect(hand + bladeDir * TipReach * Main.rand.NextFloat(0.5f, 1f),
                            DustID.GoldFlame, tangent * Main.rand.NextFloat(2f, 5f), 40, default,
                            Main.rand.NextFloat(1.4f, 2f));
                        g.noGravity = true;
                    }
                }
            }
            else if (IsThird && t < WindupFrac && Main.rand.NextBool(2)) {
                // 第三击蓄势: 金尘向刀身汇聚
                Vector2 focus = hand + bladeDir * TipReach * 0.7f;
                Dust g = Dust.NewDustPerfect(focus + Main.rand.NextVector2Circular(40f, 40f), DustID.GoldFlame);
                g.noGravity = true;
                g.velocity = (focus - g.position) * 0.14f;
                g.scale = Main.rand.NextFloat(1f, 1.5f);
            }
        }

        // 伤害窗与爆发段视觉严格对齐
        public override bool? CanDamage() => InBurst ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (TipReach + 16f);
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 40f, ref point);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (TipReach + 16f);
            Utils.PlotTileLine(start, end, 40f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 900);
            target.AddBuff(BuffID.OnFire3, 900);
            target.AddBuff(BuffID.Ichor, 900);

            bool isOwner = Projectile.owner == Main.myPlayer;

            // 处决: 非 Boss 且 <25% 血直接斩杀
            if (!target.boss && target.life > 0 && target.life < target.lifeMax * 0.25f) {
                if (isOwner)
                    target.SimpleStrikeNPC(target.life + 10, hit.HitDirection, true, 0f, null, false, 0, true);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.8f }, target.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.LethalRed, 2.4f, Projectile.owner);
                SpawnJudgment(target.Center, execute: true);
                WeaponVFX.AddScreenShake(target.Center, 7f);
                return;
            }

            if (hit.Crit) {
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.1f, Pitch = -0.6f }, target.Center);
                SpawnJudgment(target.Center, execute: false);
                if (IsThird) {
                    // 连锁审判仅第三击暴击触发
                    WeaponVFX.AddScreenShake(target.Center, 6f);
                    if (isOwner) {
                        for (int i = 0; i < Main.maxNPCs; i++) {
                            NPC nearby = Main.npc[i];
                            if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI)
                                continue;
                            if (Vector2.Distance(target.Center, nearby.Center) < 600f) {
                                nearby.SimpleStrikeNPC(damageDone, hit.HitDirection, false, 0f, null, false, 0, true);
                                nearby.AddBuff(BuffID.ShadowFlame, 600);
                            }
                        }
                    }
                }
                else {
                    WeaponVFX.AddScreenShake(target.Center, 2f);
                }
                return;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.FengduVoid, 1.1f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
            SoundEngine.PlaySound(SoundID.NPCHit4 with {
                Volume = 0.6f,
                Pitch = Main.rand.NextFloat(-0.25f, 0.1f)
            }, target.Center);
        }

        // 全屏审判演出 (纯视觉, 仅 owner 端生成)
        private void SpawnJudgment(Vector2 worldPos, bool execute) {
            if (Main.dedServ || Main.myPlayer != Projectile.owner)
                return;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), worldPos, Vector2.Zero,
                ModContent.ProjectileType<ImperatorJudgmentFlash>(), 0, 0f, Projectile.owner, execute ? 1f : 0f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float t = CurT;
            float burstEnd = WindupFrac + BurstFrac;
            Vector2 bladeDir = Projectile.rotation.ToRotationVector2();

            // 挥弧残影 (仅爆发~收招前段显形, 黑紫底 + 第三击金芯)
            if (_tipCount >= 2 && t >= WindupFrac && t < 0.72f) {
                float fade = t < burstEnd ? 1f : 1f - (t - burstEnd) / (0.72f - burstEnd);
                var pts = new List<Vector2>(_tipCount);
                for (int i = 0; i < _tipCount; i++)
                    pts.Add(_tipTrail[i]);
                Color inner = (IsThird ? FengduVFX.ImperialGoldHi : FengduVFX.VoidBright) * fade;
                WeaponVFX.DrawRibbonTrail(pts.ToArray(), 26f * Projectile.scale,
                    FengduVFX.VoidDark * (0.9f * fade), inner,
                    uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);
            }

            // 第三击前摇: 帝金蓄势辉光
            if (IsThird && t < WindupFrac) {
                float p = t / WindupFrac;
                WeaponVFX.DrawGlowBurst(Projectile.Center + bladeDir * TipReach * 0.55f,
                    0.5f + 0.9f * p * p, FengduVFX.ImperialGold * (0.25f + 0.5f * p));
            }

            // 刀身: 锚在手上, 贴图对角朝向补 PiOver4 (参照矛的 spriteDirection 翻转处理)
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin;
            float rotOffset;
            SpriteEffects fx;
            if (Projectile.spriteDirection > 0) {
                origin = new Vector2(0, tex.Height);
                rotOffset = MathHelper.PiOver4;
                fx = SpriteEffects.None;
            }
            else {
                origin = new Vector2(tex.Width, tex.Height);
                rotOffset = MathHelper.Pi - MathHelper.PiOver4;
                fx = SpriteEffects.FlipHorizontally;
            }
            float drawScale = 1.05f * Projectile.scale;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotOffset, origin, drawScale, fx, 0);

            // 爆发段: 刃身加亮 + 锋线光束 (第三击金芯)
            if (InBurst) {
                Color glowC = (IsThird ? FengduVFX.ImperialGold : FengduVFX.VoidMid) * 0.6f;
                glowC.A = 0;
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, glowC,
                    Projectile.rotation + rotOffset, origin, drawScale * 1.07f, fx, 0);
                ACMShaders.DrawBeam(Projectile.Center + bladeDir * 18f, Projectile.Center + bladeDir * TipReach,
                    17f * Projectile.scale,
                    IsThird ? FengduVFX.ImperialGoldHi : FengduVFX.VoidBright, FengduVFX.VoidDark, 0.85f,
                    flowSpeed: 2.4f, flowScale: 2.2f, coreSharp: 2.8f);
            }
            return false;
        }
    }

    /// <summary>
    /// 黑帝刀气 - 爆发帧掷出的弧形刀气。ai[0]&gt;0 = 第三击大刀气 (承载 scale)。
    /// 配色: 拖尾外 VoidDark 系 + 内帝金芯。
    /// </summary>
    public class ImperatorSlash : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/CelestialImperatorGreatblade";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 10;
            Projectile.timeLeft = 50;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.alpha = 30;
        }

        public override void AI() {
            // 大刀气尺寸应用一次 (各端从同步的 ai[0] 各自展开)
            if (Projectile.ai[0] > 0f && Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.scale = Projectile.ai[0];
                Projectile.Resize((int)(70 * Projectile.scale), (int)(70 * Projectile.scale));
            }

            Projectile.alpha += 4;
            if (Projectile.alpha > 255) {
                Projectile.Kill();
                return;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            float brightness = (255 - Projectile.alpha) / 255f;
            Lighting.AddLight(Projectile.Center, 0.55f * brightness, 0.3f * brightness, 1.0f * brightness);

            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(20, 20),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    80, default, Main.rand.NextFloat(1.8f, 2.6f));
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust shadow = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                    4, 4, DustID.Shadowflame, 0f, -1.5f, 100, default, 2f);
                shadow.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.OnFire3, 600);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.FengduVoid, 1f, Projectile.owner);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 60,
                    default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = (255 - Projectile.alpha) / 255f;
            bool empowered = Projectile.ai[0] > 0f;

            // 双层刀气拖尾 (外宽虚空黑紫 + 内窄帝金芯), 沿历史点构成弧形扫劈
            Color outer = Color.Lerp(FengduVFX.VoidDark, FengduVFX.VoidMid, 0.35f) * opacity;
            Color inner = FengduVFX.ImperialGoldHi * opacity;
            WeaponVFX.DrawProjectileTrail(Projectile, 46f * Projectile.scale, outer, inner,
                ACMAsset.GlaciateWave, uvScroll: -0.04f, subdivisions: 3);

            // 刀刃锋线 (金芯黑紫边), 即时挥砍的锐利前沿
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 tip = Projectile.Center + dir * 60f * Projectile.scale;
            Vector2 tail = Projectile.Center - dir * 70f * Projectile.scale;
            ACMShaders.DrawBeam(tail, tip, 26f * Projectile.scale,
                empowered ? FengduVFX.ImperialGoldHi : FengduVFX.ImperialGold,
                FengduVFX.VoidDark, opacity * 0.9f,
                flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f);

            // 刃心柔光
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.4f + opacity) * Projectile.scale,
                FengduVFX.VoidMid * (opacity * 0.7f));
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f),
                    60, default, Main.rand.NextFloat(1.8f, 2.6f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 虚空吞噬漩涡 - 第三击"万劫寂灭诏"终结技: 帝诏垂落 + 虚空裂口收口, 将敌人拖入深渊。
    /// </summary>
    public class ImperatorVoidEruption : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/CelestialImperatorGreatblade";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 45;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity *= 0.92f;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy())
                    continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < 400f && dist > 30f) {
                    Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * 4f;
                    npc.velocity += pull;
                }
            }

            float progress = Timer / 45f;
            Lighting.AddLight(Projectile.Center, 0.7f * (1f - progress), 0.35f * (1f - progress), 1.3f * (1f - progress));

            // 向心吸卷尘 (黑紫为主)
            for (int i = 0; i < 4; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(20f, 120f) * (1f - progress * 0.5f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 3f;
                Dust d = Dust.NewDustPerfect(pos, DustID.PurpleTorch, vel, 40, default, Main.rand.NextFloat(2f, 3.2f));
                d.noGravity = true;
            }
            for (int i = 0; i < 2; i++) {
                Vector2 vel = new Vector2(0, -Main.rand.NextFloat(4f, 12f)).RotatedByRandom(MathHelper.TwoPi);
                Dust wraith = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(50, 50),
                    DustID.Wraith, vel, 100, default, 3f);
                wraith.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 900);
            target.AddBuff(BuffID.OnFire3, 900);
            target.AddBuff(BuffID.Ichor, 900);
            target.AddBuff(BuffID.BrokenArmor, 900);
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / 45f;
            float opacity = 1f - progress;
            float appear = MathHelper.Clamp(Timer / 6f, 0f, 1f);

            // 帝诏卷轴带: 从漩涡上空 260px 垂落至涡心 (前 40 帧展开, 生命末段淡出)
            float unroll = MathHelper.Clamp(Timer / 40f, 0f, 1f);
            float bandFade = MathHelper.Clamp((1f - progress) / 0.3f, 0f, 1f);
            FengduVFX.DrawDecreeBand(Projectile.Center - new Vector2(0f, 260f), Projectile.Center,
                26f, unroll, 0.9f * bandFade, glyphFreq: 9f, seed: Projectile.whoAmI * 0.31f);

            // 虚空裂口 (140→60 随进度收口)
            float riftR = MathHelper.Lerp(140f, 60f, progress);
            FengduVFX.DrawVoidRift(Projectile.Center, riftR,
                appear * MathHelper.Lerp(0.95f, 0.5f, progress), 0.5f, 0,
                FengduVFX.VoidMid, FengduVFX.VoidBright, seed: Projectile.whoAmI * 0.137f);

            // 吸卷冲击环 (向心收口的可读预警)
            float ringR = MathHelper.Lerp(150f, 24f, progress);
            WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 16f, opacity * 0.8f,
                FengduVFX.VoidBright, FengduVFX.VoidDark);
            return false;
        }

        // 第三击虚空漩涡的签名时刻: GenericWarp 黑洞吸卷扭曲 (45 帧短暂, 走名额仲裁)
        public override void PostDraw(Color lightColor) {
            if (Main.dedServ || Main.gameMenu)
                return;
            float progress = Timer / 45f;
            float warp = MathHelper.Clamp(1f - progress, 0f, 1f);
            if (warp < 0.05f || !ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;
            ACMShaders.SetCommonParams(fx, Projectile.Center, warp);
            fx.Parameters["uRadius"]?.SetValue(0.55f);
            fx.Parameters["uWarpScale"]?.SetValue(1.5f);
            fx.Parameters["uChroma"]?.SetValue(0.7f);
            fx.Parameters["uRadialPull"]?.SetValue(0.9f); // 向心吸入
            fx.Parameters["uMode"]?.SetValue(4f);          // void 黑洞档
            fx.Parameters["uTint"]?.SetValue(new Vector4(0.32f, 0.12f, 0.5f, 0.7f));
            SpriteBatch sb = Main.spriteBatch;
            ACMShaders.ApplyScreenPostProcess(sb, fx, bindNoise: true);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.8f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 5f);
            for (int i = 0; i < 24; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(18f, 18f);
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, vel, 40,
                    default, Main.rand.NextFloat(2.5f, 4f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new Vector2(0, -Main.rand.NextFloat(6f, 16f)).RotatedByRandom(MathHelper.TwoPi);
                Dust death = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 80,
                    default, Main.rand.NextFloat(2f, 3.2f));
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 全屏审判演出 (纯视觉, 本地客户端): 处决=暗红定调; 暴击=黑紫定调。
    /// ElementalScreenTint 短暂染屏 + RadialBloom 核心泛光 (单次"短暂定调")。
    /// </summary>
    public class ImperatorJudgmentFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 26;

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

            float life = MathHelper.Clamp(Projectile.timeLeft / (float)Life, 0f, 1f); // 1→0
            bool execute = Projectile.ai[0] > 0.5f;
            Color tint = execute ? new Color(140, 16, 22) : new Color(70, 24, 130);
            Color tintLow = execute ? new Color(30, 2, 6) : new Color(14, 4, 30);
            Color bloom = execute ? new Color(255, 60, 70) : new Color(180, 110, 255);

            // ElementalScreenTint 短暂染屏 (不读 screenTarget, 不占全屏名额)
            Effect tintFx = ACMShaders.ElementalScreenTint;
            if (tintFx != null) {
                ACMShaders.SetCommonParams(tintFx, Projectile.Center, life);
                tintFx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), 0.34f * life));
                tintFx.Parameters["uTint2"]?.SetValue(new Vector4(tintLow.ToVector3(), 0f));
                tintFx.Parameters["uVignette"]?.SetValue(0.5f);
                tintFx.Parameters["uFogScale"]?.SetValue(2.4f);
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                ACMShaders.DrawFullscreenOverlay(tintFx, BlendState.AlphaBlend);
                ACMShaders.RestoreDefaultBatch(sb);
            }

            // 核心泛光 (RadialBloom 占全屏名额, 名额被占自动退化柔光)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.2f, life * 0.85f, bloom, 10f);
            return false;
        }
    }
}
