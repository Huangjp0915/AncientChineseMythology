using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 雷龙劫 (龙符咒每第四掷的大招控制器): 蓄 (法阵汇聚) → 落 (巨龙贯穿而下) →
    /// 劫 (两段 150% AoE + 三根雷柱 + 全屏定调) → 偿 (首次命中回血, 由 CharmNovaProj 旗标承担)。
    /// 类名保留 (本地化/兼容), 职能由"砸方块的爆炸"重铸为大招; **不再破坏任何物块**。
    /// </summary>
    public class DragonCharmExplosion : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int ChargeEnd = 40;
        private const int DescendEnd = 50;
        private const int JieEnd = 68;
        private const int LifeTime = 88;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false; //伤害全部由 Nova/雷柱承担

        public override void AI() {
            Age++;

            if (Age <= ChargeEnd) {
                //—— 蓄: 向心汇聚电尘 (~75% 进度骤停 = 尖叫前的吸气), 震屏 t² 渐起 ——
                float t = Age / ChargeEnd;
                if (Age == 1)
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
                if (!Main.dedServ && t < 0.75f && Main.rand.NextBool(2)) {
                    Vector2 spawn = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f)
                        * Main.rand.NextFloat(150f, 280f);
                    Dust d = Dust.NewDustPerfect(spawn, DustID.Electric,
                        (Projectile.Center - spawn) * 0.085f, 100, default, 1.1f);
                    d.noGravity = true;
                }
                WeaponVFX.AddScreenShake(Projectile.Center, t * t * 3f);
            }
            else if (Age <= DescendEnd) {
                //—— 落: 巨龙自天顶贯穿而下 (纯演出, 绘制在 PreDraw) ——
                if (Age == ChargeEnd + 1) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.25f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = 0.15f }, Projectile.Center);
                }
            }
            else if (Age <= JieEnd) {
                //—— 劫: 两段 AoE + 三根雷柱 (owner 生成) + 重震 ——
                if (Age == DescendEnd + 1) {
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1f, Pitch = 0.1f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Projectile.Center, 9f);
                    SpawnJie(first: true);
                }
                if (Age == DescendEnd + 9)
                    SpawnJie(first: false);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.8f, 0.72f, 0.4f)
                * MathHelper.Clamp(Age / ChargeEnd, 0f, 1f));
        }

        private void SpawnJie(bool first) {
            if (Projectile.owner != Main.myPlayer)
                return;
            int novaDmg = (int)(Projectile.damage * 1.5f);
            //首段带"偿"旗标 (首次命中回 20 血): flags = 1(heal)|2(ranged); 次段仅 2
            float flags = (first ? 3f : 2f) + CharmVFX.Dragon * 16f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CharmNovaProj>(), novaDmg, Projectile.knockBack, Projectile.owner,
                260f, flags);

            if (first) {
                //三根雷柱: 全部落在 AoE 半径内, 8f/4f 交错 — 伤害与视觉严格对齐
                for (int i = 0; i < 3; i++) {
                    float x = (i - 1) * 140f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        Projectile.Center + new Vector2(x, 0f), Vector2.Zero,
                        ModContent.ProjectileType<DragonJieBolt>(), Projectile.damage,
                        Projectile.knockBack, Projectile.owner, i * 4f);
                }
                //朱印大章
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<CharmSealFX>(), 0, 0f, Projectile.owner, CharmVFX.Dragon, 1.8f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 c = Projectile.Center;
            float chargeT = MathHelper.Clamp(Age / ChargeEnd, 0f, 1f);

            //—— 大朱印法阵: 蓄势期由小转大 + 缓旋, 之后常驻至消散 ——
            float sigilFade = Age > JieEnd ? MathHelper.Clamp(1f - (Age - JieEnd) / (LifeTime - JieEnd), 0f, 1f) : 1f;
            float flash = Age > DescendEnd && Age <= DescendEnd + 6 ? 1f - (Age - DescendEnd) / 6f : 0f;
            float sigilSize = 300f * (0.35f + 0.65f * ACMUtils.QuadOut(chargeT));
            CharmVFX.DrawTalisman(c, Age * 0.01f, sigilSize, (0.35f + 0.65f * chargeT) * sigilFade,
                1f, 0f, chargeT, flash, CharmVFX.Dragon, CharmVFX.InkGold, mode: 1);

            //—— 落: 巨龙真身贯穿而下 (t² 加速, 1 帧过屏级) ——
            if (Age > ChargeEnd && Age <= DescendEnd + 6) {
                float t = MathHelper.Clamp((Age - ChargeEnd) / (float)(DescendEnd - ChargeEnd), 0f, 1f);
                float headY = MathHelper.Lerp(-1300f, 0f, t * t);
                float fade = Age > DescendEnd ? 1f - (Age - DescendEnd) / 6f : 1f;

                //龙身路径: 自天顶向印心, 微幅蜿蜒
                var pts = new System.Collections.Generic.List<Vector2>(14);
                for (int i = 0; i < 14; i++) {
                    float seg = i / 13f;
                    float y = headY - seg * 900f;
                    if (y < -1400f)
                        break;
                    float wave = MathF.Sin(seg * 6.5f + Age * 0.4f) * 46f * seg;
                    pts.Add(c + new Vector2(wave, y));
                }
                if (pts.Count >= 2)
                    CharmVFX.DrawDragonRibbon(pts.ToArray(), 60f,
                        new Color(255, 240, 170, 235), new Color(118, 88, 215, 150),
                        energy: 1f, pulse: 1f, intensity: fade);
            }

            //—— 劫: 起爆 4 帧金紫定调 (≤0.15, 全屏名额) + 余波径向泛光 ——
            if (Age > DescendEnd && Age <= JieEnd + 8) {
                float sinceJie = Age - DescendEnd;
                if (sinceJie <= 4f) {
                    WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                        shadowTint: new Color(40, 22, 70), highlightTint: new Color(255, 226, 140),
                        intensity: 0.13f * (1f - sinceJie / 5f), saturation: 1.08f);
                }
                else {
                    float tt = 1f - MathHelper.Clamp((sinceJie - 4f) / (JieEnd + 8f - DescendEnd - 4f), 0f, 1f);
                    WeaponVFX.DrawRadialBloom(c, 0.3f, 0.85f * tt, new Color(255, 226, 140), 12f);
                }
            }

            //蓄势中心辉光 (growth³: 静默的膨胀)
            if (Age <= ChargeEnd)
                WeaponVFX.DrawGlowBurst(c, 0.4f + chargeT * chargeT * chargeT * 1.6f,
                    new Color(255, 226, 140) * (0.3f + chargeT * 0.6f));
            return false;
        }
    }

    /// <summary>
    /// 雷龙劫·雷柱: 短暂预告线后落下的垂直雷柱 (伤害窗口与视觉严格对齐)。ai[0]=起手延迟。
    /// </summary>
    public class DragonJieBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int TelegraphTime = 10;
        private const int ActiveTime = 8;
        private const float HalfWidth = 30f;
        private const float ColumnTop = 950f;
        private const float ColumnBottom = 70f;

        private ref float Age => ref Projectile.localAI[0];
        private float Delay => Projectile.ai[0];

        public override void SetStaticDefaults() {
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.DragonJieBolt.DisplayName",
                () => "Dragon Tribulation Bolt");
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override bool ShouldUpdatePosition() => false;

        private bool Active => Age > Delay + TelegraphTime && Age <= Delay + TelegraphTime + ActiveTime;

        public override bool? CanDamage() => Active;

        public override void AI() {
            Age++;
            if ((int)Age == (int)(Delay + TelegraphTime + 1)) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = 0.2f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 3f);
            }
            if (Age > Delay + TelegraphTime + ActiveTime + 10)
                Projectile.Kill();
            if (Active)
                Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.68f, 0.9f));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Active)
                return false;
            Vector2 top = Projectile.Center - new Vector2(0f, ColumnTop);
            Vector2 bottom = Projectile.Center + new Vector2(0f, ColumnBottom);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                top, bottom, HalfWidth * 2f, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, 1.2f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || Age <= Delay)
                return false;

            Vector2 top = Projectile.Center - new Vector2(0f, ColumnTop);
            Vector2 bottom = Projectile.Center + new Vector2(0f, ColumnBottom);

            if (Age <= Delay + TelegraphTime) {
                //预告细线 (渐亮)
                float t = (Age - Delay) / TelegraphTime;
                ACMShaders.DrawBeam(top, bottom, 4f, new Color(255, 240, 180, 160),
                    new Color(150, 110, 230, 60), 0.25f + t * 0.4f, coreSharp: 3f);
            }
            else if (Age <= Delay + TelegraphTime + ActiveTime + 8) {
                //雷柱主体 + 收尾淡出
                float sinceHit = Age - Delay - TelegraphTime;
                float fade = sinceHit <= ActiveTime ? 1f : 1f - (sinceHit - ActiveTime) / 8f;
                float width = HalfWidth * (0.6f + 0.4f * MathF.Min(sinceHit / 2f, 1f));
                ACMShaders.DrawBeam(top, bottom, width * fade,
                    new Color(255, 245, 200, 235), new Color(130, 95, 225, 130),
                    fade, flowSpeed: 3.2f, flowScale: 3f, coreSharp: 2.6f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.9f * fade, new Color(255, 235, 160) * fade);
            }
            return false;
        }
    }
}
