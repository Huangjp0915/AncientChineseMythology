using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 棍系列共享 ModPlayer: 承载如意金箍棒的"如意值"资源循环 (0~100)。
    /// (旧版的受击减伤补偿已随定海神针重做移除。)
    /// </summary>
    public class RuyiStickPlayer : ModPlayer
    {
        /// <summary>如意值 0~100 (如意金箍棒命中积攒, 满值解锁定海神针·真)。</summary>
        public int ruyiPower;

        public void AddRuyiPower(int amount) {
            int before = ruyiPower;
            ruyiPower = Math.Clamp(ruyiPower + amount, 0, 100);
            // 蓄满提示 (仅本地玩家听见)
            if (before < 100 && ruyiPower >= 100 && Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 1f, Pitch = 0.4f }, Player.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.2f }, Player.Center);
            }
        }

        public override void PostUpdateMiscEffects() {
            // 未持金箍棒时缓慢衰减 (约 10s 从满衰到 0)
            bool holding = Player.HeldItem != null
                && Player.HeldItem.type == ModContent.ItemType<Items.Weapons.Sticks.RuyiJinguBang>();
            if (!holding && ruyiPower > 0 && Main.GameUpdateCount % 6 == 0)
                ruyiPower--;
        }
    }

    /// <summary>
    /// 如意棍/真·如意棍右键"定海神针"蓄力段: 按住右键, 棍上举渐伸 (1→2.2x) + 蓄力抖动,
    /// 三级蓄力 (每级 MaxMana 音高递升), 最后 10% 粒子静默收束; 松开在鼠标落点召唤定海神针砸落。
    /// 玩家可移动 (横向阻尼), 不再锁定坐标; 无任何免伤补偿。
    /// ai[0] = 规格 (0=如意棍, 1=真·如意棍: 蓄力更快/针更大/落地分侧针)。
    /// ai[2] = 释放标记 (owner 写 + netUpdate, 其余端跟随)。
    /// </summary>
    internal class RuyiStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";

        private const int MaxTier = 3;

        private bool IsTrue => Projectile.ai[0] >= 1f;
        private int TierFrames => IsTrue ? 24 : 30;
        private ref float Released => ref Projectile.ai[2];
        private ref float Charge => ref Projectile.localAI[0]; // 各端同步递增 (确定性)
        private Player Owner => Main.player[Projectile.owner];

        private int _lastTier;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false; // 蓄力段无判定, 伤害交给落下的神针
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool ShouldUpdatePosition() => false;

        private float Charge01 => MathHelper.Clamp(Charge / (TierFrames * MaxTier), 0f, 1f);
        private int Tier => Math.Min((int)(Charge / TierFrames), MaxTier);

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead || owner.noItems || owner.CCed) {
                Projectile.Kill();
                return;
            }

            owner.heldProj = Projectile.whoAmI;
            owner.itemAnimation = 2;
            owner.itemTime = 2;

            // owner 端读自己的鼠标, 释放经 ai[2]+netUpdate 广播
            if (Main.myPlayer == Projectile.owner && Released == 0f && !Main.mouseRight) {
                Released = 1f;
                Projectile.netUpdate = true;
                if (Tier >= 1)
                    SummonPillar(owner);
                else
                    SoundEngine.PlaySound(SoundID.MenuTick, owner.Center); // 未成级取消
            }

            if (Released == 1f) {
                Projectile.Kill();
                return;
            }

            if (Charge < TierFrames * MaxTier)
                Charge++;
            Projectile.timeLeft = 60;

            // 蓄力级提示: 音高递升 + 柔光脉冲
            if (Tier > _lastTier) {
                _lastTier = Tier;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = -0.2f + Tier * 0.25f }, owner.Center);
            }

            // 棍上举渐伸 + 蓄力抖动 (幅度随蓄力平方增长)
            float len = MathHelper.Lerp(1f, 2.2f, Charge01);
            float jitter = Charge01 * Charge01 * 2.5f;
            Projectile.rotation = -MathHelper.PiOver2;
            Projectile.scale = 1.2f * len * owner.GetAdjustedItemScale(owner.HeldItem);
            Projectile.Center = owner.MountedCenter + new Vector2(
                Main.rand.NextFloat(-jitter, jitter) + 6f * owner.direction,
                -30f - 40f * len + Main.rand.NextFloat(-jitter, jitter));
            owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, MathHelper.Pi + 0.3f * owner.direction);

            // 横向阻尼 (决策代价, 仅 owner 端)
            if (Main.myPlayer == Projectile.owner)
                owner.velocity.X *= 0.93f;

            // 汇聚流光: 密度 ∝ sqrt(蓄力), 最后 10% 静默 (爆发前的吸气)
            if (Charge01 < 0.9f && Main.rand.NextFloat() < 0.25f + 0.5f * MathF.Sqrt(Charge01)) {
                Vector2 tip = Projectile.Center + new Vector2(0f, -40f * Projectile.scale * 0.4f);
                Vector2 from = tip + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(70f, 150f);
                Dust d = Dust.NewDustPerfect(from, DustID.RedTorch, (tip - from) * 0.085f, 100, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.15f, 0.17f) * (0.4f + Charge01 * 0.6f));
        }

        /// <summary>owner 端召唤定海神针 (鼠标落点, 经生成包同步)。</summary>
        private void SummonPillar(Player owner) {
            Vector2 mouse = Main.MouseWorld;
            float x = MathHelper.Clamp(mouse.X, owner.Center.X - 1400f, owner.Center.X + 1400f);
            float y = MathHelper.Clamp(mouse.Y, owner.Center.Y - 800f, owner.Center.Y + 800f);
            Vector2 spawnPos = new(x, y - 560f);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, new Vector2(0f, 12f),
                ModContent.ProjectileType<RuyiStickSpearProjectile_3>(), Projectile.damage, Projectile.knockBack,
                Projectile.owner, Tier, IsTrue ? 1f : 0f);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 1f, Pitch = -0.35f }, owner.Center);
            WeaponVFX.AddScreenShake(owner.Center, 2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 蓄力核心致命红辉光 (随蓄力增强, 最后一级白热)
            float c01 = Charge01;
            Color glow = Color.Lerp(new Color(250, 40, 56), new Color(255, 200, 180), c01 * c01);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f + c01 * 0.6f, glow * (0.5f + c01 * 0.5f));

            // 蓄力级刻度: 已成级数以小柔光沿棍标示
            for (int i = 0; i < Tier; i++)
                WeaponVFX.DrawGlowBurst(Projectile.Center + new Vector2(0f, 26f * (i + 1)), 0.22f, new Color(255, 120, 110) * 0.8f);

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + MathHelper.ToRadians(45f), tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 定海神针: 天降巨针砸落 (如意棍/真·如意棍右键释放, 也作真·如意棍侧针复用)。
    /// poly 加速下坠 → 落地插驻 0.8s: 屏震 5~9 / Fatal 爆裂 / 双环冲击波 / 碎石。
    /// 落点预警: 下坠中在预判落点画致命红标记 (公平可读)。
    /// ai[0] = 蓄力级 1~3; ai[1] = 变体 (0 普通 / 1 真·主针: +25% 且落地分两根侧针 / 2 侧针)。
    /// </summary>
    internal class RuyiStickSpearProjectile_3 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";

        private int TierN => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private int Variant => (int)Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];
        private Player Owner => Main.player[Projectile.owner];

        private bool _landed;
        private float _landingY = -1f; // 预判落点 (各端从瓦片数据确定性计算)
        private float _fallTime;

        private float PillarLength => 300f * (1f + 0.32f * (TierN - 1)) * Variant switch { 1 => 1.25f, 2 => 0.55f, _ => 1f };
        private float TierDamageMul => TierN switch { 1 => 2f, 2 => 2.75f, _ => 3.5f };

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.rotation = MathHelper.PiOver2; // 针尖向下
            FindLanding();
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
        }

        /// <summary>向下扫描第一格实心瓦片作为预判落点 (各端同瓦片数据, 结果一致)。</summary>
        private void FindLanding() {
            int tx = (int)(Projectile.Center.X / 16f);
            int startY = (int)(Projectile.Center.Y / 16f);
            for (int ty = Math.Max(startY, 10); ty < Math.Min(startY + 140, Main.maxTilesY - 10); ty++) {
                if (WorldGen.InWorld(tx, ty) && WorldGen.SolidTile(tx, ty)) {
                    _landingY = ty * 16f;
                    return;
                }
            }
            _landingY = Projectile.Center.Y + 1400f; // 无地面: 落到极限后消散
        }

        private Vector2 TipPos => Projectile.Center + new Vector2(0f, PillarLength * 0.5f);

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.18f, 0.2f));

            // OnSpawn 不在远端运行: 落点与朝向在各端从同步状态确定性重建
            Projectile.rotation = MathHelper.PiOver2;
            if (_landingY < 0f)
                FindLanding();

            if (!_landed) {
                // poly(2) 加速下坠
                _fallTime++;
                Projectile.velocity = new Vector2(0f, MathF.Min(12f + _fallTime * _fallTime * 0.55f, 64f));

                // 速度门控拖尾尘
                if (Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-PillarLength, PillarLength) * 0.5f),
                        DustID.RedTorch, new Vector2(0f, -Projectile.velocity.Y * 0.1f), 120, default, Main.rand.NextFloat(1f, 1.7f));
                    d.noGravity = true;
                }

                if (TipPos.Y >= _landingY)
                    Land();
            }
            else {
                StateTimer++;
                Projectile.velocity = Vector2.Zero;
                // 插驻 48 帧后收缩消散
                if (StateTimer > 48f) {
                    Projectile.scale -= 0.09f;
                    if (Projectile.scale <= 0.3f)
                        Projectile.Kill();
                }
            }
        }

        private void Land() {
            _landed = true;
            StateTimer = 0f;
            // 针体对齐: 尖端插进地面 16px
            Projectile.Center = new Vector2(Projectile.Center.X, _landingY + 16f - PillarLength * 0.5f);
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;

            float shake = Variant == 2 ? 3.5f : 3f + TierN * 2f; // 5/7/9, 侧针 3.5
            WeaponVFX.AddScreenShake(TipPos, shake);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.2f + Main.rand.NextFloat(0.1f) }, TipPos);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1f, Pitch = -0.3f }, TipPos);

            ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), TipPos, ACMWeaponBurst.Fatal,
                Variant == 2 ? 0.9f : 0.8f + TierN * 0.4f, Projectile.owner);

            // 碎石 + 上抛尘
            int debris = Variant == 2 ? 10 : 20;
            for (int i = 0; i < debris; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-7f, -2f));
                Dust d = Dust.NewDustPerfect(TipPos + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f),
                    Main.rand.NextBool() ? DustID.Smoke : DustID.RedTorch, vel, 60, default, Main.rand.NextFloat(1f, 1.8f));
                d.noGravity = d.type == DustID.RedTorch;
            }

            // 真·主针: 落地分两根侧针 (owner 端生成)
            if (Variant == 1 && Main.myPlayer == Projectile.owner) {
                for (int s = -1; s <= 1; s += 2) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        new Vector2(Projectile.Center.X + s * 240f, _landingY - 480f), new Vector2(0f, 16f),
                        ModContent.ProjectileType<RuyiStickSpearProjectile_3>(), Projectile.damage, Projectile.knockBack * 0.7f,
                        Projectile.owner, TierN, 2f);
                }
            }
        }

        // 判定: 下坠全程 + 落地后 8 帧 (与视觉冲击严格对齐)
        public override bool? CanDamage() {
            if (!_landed || StateTimer < 8f)
                return base.CanDamage();
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 top = Projectile.Center - new Vector2(0f, PillarLength * 0.5f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                top, TipPos, 22f * Projectile.scale, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Projectile.Center.X ? 1 : -1;
            modifiers.FinalDamage *= _landed ? TierDamageMul : 1.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 2.5f);
            if (!_landed)
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Fatal, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 落点预警 (公平阀): 下坠中在预判落点画致命红标记
            if (!_landed && _landingY > 0f) {
                Vector2 mark = new(Projectile.Center.X, _landingY);
                float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f);
                WeaponVFX.DrawGlowBurst(mark, 0.8f * pulse, new Color(250, 40, 56) * 0.7f);
            }

            // 下坠致命红粗拖尾 (沿针体)
            if (!_landed) {
                var pts = new Vector2[6];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = Projectile.Center - new Vector2(0f, (i - 2.5f) * PillarLength * 0.18f);
                WeaponVFX.DrawRibbonTrail(pts, 13f, new Color(120, 10, 20, 170), new Color(250, 40, 56, 220),
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);
            }

            // 落地冲击环 (24 帧双环扩张)
            if (_landed && StateTimer < 24f) {
                float t = StateTimer / 24f;
                float maxR = 60f + TierN * 40f;
                WeaponVFX.DrawShockwaveRing(TipPos, 16f + t * maxR, 12f, (1f - t) * 0.9f,
                    new Color(255, 120, 110), new Color(120, 10, 20));
            }

            // 针体: 贴图沿垂直轴拉伸 (对角贴图旋 45° 摆正)
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float texDiag = tex.Size().Length() * 0.82f; // 贴图内棍身有效长度
            float stretch = PillarLength / texDiag;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * Projectile.Opacity,
                Projectile.rotation + MathHelper.ToRadians(45f), tex.Size() * 0.5f,
                Projectile.scale * stretch, SpriteEffects.None, 0);
            return false;
        }
    }
}
