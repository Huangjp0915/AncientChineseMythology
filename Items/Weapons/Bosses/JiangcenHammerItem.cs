using AncientChineseMythology.Helpers;
using InnoVault.GameContent.BaseEntity;
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
    /// 雷鸣锤 — 将臣掉落近战投掷锤。举锤蓄势 → 瞬发直线 → 顶点"听令"顿拍引落天雷 → 回收;
    /// 命中也引落雷 (每掷限 3 道); 每第 5 掷为"点将令", 命中处虚影锤依次轰落。
    /// 机制为将臣"点将砸 / 雷狱落雷 / 落地山崩"的玩家化直译 (Docs/WeaponRedo/BossScatter.md §3.3)。
    /// 配色遵循将臣 V3 语言: 雷青 (180,230,255) + 军金 (255,215,120)。
    /// </summary>
    public class JiangcenHammerItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";

        private int throwCount; // 仅 owner 端 Shoot 消费

        public override void SetDefaults() {
            Item.width = 150;
            Item.height = 132;
            Item.damage = 680;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 22;
            Item.shootSpeed = 1f;
            Item.knockBack = 6f;
            Item.shoot = ModContent.ProjectileType<JiangcenHammerProj>();
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null; // 出手音由弹幕在爆发帧分层播放
            Item.rare = ItemRarityID.Red;
            Item.value = 2000;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[Item.shoot] < 2; // 同屏至多 2 柄

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            float decree = 0f;
            if (++throwCount >= 5) {
                throwCount = 0;
                decree = 1f; // 点将令
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.4f, Volume = 1f }, player.Center);
            }
            Projectile.NewProjectile(source, player.Center, velocity.SafeNormalize(Vector2.UnitX),
                type, damage, knockback, player.whoAmI, 0f, 0f, decree);
            return false;
        }
    }

    /// <summary>雷鸣锤主题色 (将臣 V3 语言)。</summary>
    internal static class JiangcenHammerVFX
    {
        public static readonly Color ThunderCyan = new(180, 230, 255);
        public static readonly Color ArmyGold = new(255, 215, 120);
    }

    /// <summary>
    /// 掷出的雷鸣锤 (类名保留) — 四段状态机: 0 举锤蓄势 12f → 1 瞬发直线 → 2 顶点顿拍 (引落雷)
    /// → 3 回收。ai[0]=状态, ai[1]=计时(update), ai[2]=点将令。extraUpdates=1 (计时按 update 数)。
    /// </summary>
    public class JiangcenHammerProj : BaseHeldProj
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";

        private const int WindupUpdates = 24;   // 12 游戏帧
        private const int ApexUpdates = 20;     // 10 游戏帧
        private const float LaunchSpeed = 23f;  // ×2 update ≈ 46px/f
        private const float MaxFlight = 560f;

        private int boltBudget = 3;   // 命中落雷预算 (owner 端消费)
        private bool decreeDone;      // 点将令只触发一次 (owner 端消费)

        private float State => Projectile.ai[0];
        private bool Decree => Projectile.ai[2] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 900;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24; // ×2 update = 12 游戏帧
        }

        public override bool? CanDamage() => State >= 1f; // 蓄势期无判定

        public override void AI() {
            Projectile.ai[1]++;
            float t = Projectile.ai[1];

            switch (State) {
                case 0f: {
                    // 举锤蓄势: 头顶举起 + pow(4) 反向后拉 (力量住在前摇里)
                    Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    float ct = t / WindupUpdates;
                    Projectile.Center = Owner.Center + new Vector2(0f, -46f) - dir * MathF.Pow(ct, 4f) * 52f;
                    Projectile.rotation = dir.ToRotation() + MathHelper.PiOver2 - MathF.Pow(ct, 3f) * 0.5f * Owner.direction;
                    Owner.heldProj = Projectile.whoAmI;
                    Owner.direction = dir.X >= 0f ? 1 : -1;

                    if (t >= WindupUpdates) {
                        // 爆发 = 一帧 set + 音效双层 + 出手后坐
                        Projectile.ai[0] = 1f;
                        Projectile.ai[1] = 0f;
                        Projectile.velocity = dir * LaunchSpeed;
                        Projectile.localAI[0] = Projectile.Center.X; // 记录出手点
                        Projectile.localAI[1] = Projectile.Center.Y;
                        Projectile.netUpdate = true;
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f, Volume = 1.1f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = 0.2f }, Projectile.Center);
                        if (Main.myPlayer == Projectile.owner)
                            Owner.velocity -= dir * 1.2f; // 掷锤后坐 (仅 owner 端改自身)
                    }
                    break;
                }
                case 1f: {
                    // 直线飞行: 旋转速度门控
                    Projectile.rotation += 0.17f * Math.Sign(Projectile.velocity.X == 0 ? 1f : Projectile.velocity.X);
                    Vector2 launchPos = new(Projectile.localAI[0], Projectile.localAI[1]);
                    if (Vector2.Distance(Projectile.Center, launchPos) >= MaxFlight || t >= 70f) {
                        Projectile.ai[0] = 2f;
                        Projectile.ai[1] = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                case 2f: {
                    // 顶点"听令"顿拍: 硬刹 + 自旋衰减; 首拍引落天雷
                    Projectile.velocity *= 0.78f;
                    Projectile.rotation += 0.09f * (1f - t / ApexUpdates);
                    if (t == 1f) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);
                        if (Projectile.owner == Main.myPlayer) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                                Projectile.Center, Vector2.Zero,
                                ModContent.ProjectileType<JiangcenHammerSkyBolt>(),
                                (int)(Projectile.damage * 0.4f), 2f, Projectile.owner);
                        }
                    }
                    if (t >= ApexUpdates) {
                        Projectile.ai[0] = 3f;
                        Projectile.ai[1] = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                default: {
                    // 回收: 追手加速
                    Vector2 toOwner = Owner.Center - Projectile.Center;
                    if (toOwner.Length() > 3600f) {
                        Projectile.Kill();
                        return;
                    }
                    Projectile.velocity = Projectile.velocity * 0.93f + toOwner.SafeNormalize(Vector2.UnitX) * 2.1f;
                    if (Projectile.velocity.Length() > 15f)
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 15f;
                    Projectile.rotation += 0.14f * Math.Sign(Projectile.velocity.X == 0 ? 1f : Projectile.velocity.X);
                    if (Main.myPlayer == Projectile.owner && Projectile.Hitbox.Intersects(Owner.Hitbox))
                        Projectile.Kill();
                    break;
                }
            }

            // 拖尾电花 (节流; 蓄势期不撒)
            if (!Main.dedServ && State >= 1f && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                    Main.rand.NextBool() ? DustID.Electric : DustID.GoldFlame,
                    -Projectile.velocity * 0.08f, 140, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.6f, 0.85f) * (State >= 1f ? 0.9f : 0.35f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中反馈栈: 少量电花/金屑 + 预算内震屏 + 金辉爆
            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center,
                        i % 2 == 0 ? DustID.Electric : DustID.GoldFlame,
                        Main.rand.NextVector2Circular(6f, 6f), 120, default, Main.rand.NextFloat(1.1f, 1.9f));
                    d.noGravity = true;
                }
            }
            SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { Pitch = Main.rand.NextFloat(-0.15f, 0.15f) }, target.Center);
            WeaponVFX.AddScreenShake(target.Center, 2f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 1.2f, owner: Projectile.owner);

            if (Projectile.owner != Main.myPlayer)
                return;

            // 命中引落雷 (每掷 ≤3 道)
            if (boltBudget > 0) {
                boltBudget--;
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target),
                    target.Top, Vector2.Zero, ModContent.ProjectileType<JiangcenHammerSkyBolt>(),
                    (int)(Projectile.damage * 0.4f), 2f, Projectile.owner);
            }
            // 点将令: 首个命中目标上空 3 柄虚影锤依次轰落
            if (Decree && !decreeDone) {
                decreeDone = true;
                SoundEngine.PlaySound(SoundID.Item92 with { Pitch = -0.2f, Volume = 1f }, target.Center);
                for (int i = 0; i < 3; i++) {
                    Vector2 spawn = target.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), -430f);
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target),
                        spawn, Vector2.Zero, ModContent.ProjectileType<JiangcenHammerEchoStrike>(),
                        (int)(Projectile.damage * 0.6f), 4f, Projectile.owner,
                        10f + i * 20f, target.Center.Y);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = tex.GetRectangle();
            Vector2 origin = rect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 蓄势期: 末 4 帧微抖 (纯视觉) + 金辉渐亮
            if (State == 0f) {
                float ct = Projectile.ai[1] / WindupUpdates;
                if (ct > 0.66f)
                    drawPos += Main.rand.NextVector2Circular(1.6f, 1.6f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f + ct * 1.3f,
                    JiangcenHammerVFX.ArmyGold * (0.25f + 0.55f * ct));
                Main.spriteBatch.Draw(tex, drawPos, rect, lightColor, Projectile.rotation, origin,
                    Projectile.scale, SpriteEffects.None, 0f);
                return false;
            }

            // 飞行/回收: 速度门控双层拖尾 (雷青外 + 军金芯) 与残影
            float speedGate = MathHelper.Clamp((Projectile.velocity.Length() - 6f) / 16f, 0f, 1f);
            if (speedGate > 0.05f) {
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 20f * speedGate,
                    outerColor: (JiangcenHammerVFX.ThunderCyan * speedGate) with { A = 130 },
                    innerColor: (JiangcenHammerVFX.ArmyGold * speedGate) with { A = 190 },
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);

                float sengs = 0.4f * speedGate;
                for (int i = 0; i < Projectile.oldPos.Length; i += 2) {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;
                    Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color c = (Decree ? JiangcenHammerVFX.ArmyGold : JiangcenHammerVFX.ThunderCyan) * sengs;
                    c.A = 0;
                    Main.spriteBatch.Draw(tex, oldPos, rect, c,
                        Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation,
                        origin, Projectile.scale, SpriteEffects.None, 0f);
                    sengs *= 0.72f;
                }
            }

            // 顶点顿拍: 收拢脉冲环 (读作"听令")
            if (State == 2f) {
                float at = Projectile.ai[1] / ApexUpdates;
                WeaponVFX.DrawShockwaveRing(Projectile.Center, MathHelper.Lerp(90f, 26f, at), 9f,
                    (1f - at) * 0.7f, JiangcenHammerVFX.ThunderCyan, new Color(60, 90, 160));
            }

            Main.spriteBatch.Draw(tex, drawPos, rect, lightColor, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0f);

            // 点将令: 军金外辉描边
            if (Decree) {
                Color gold = JiangcenHammerVFX.ArmyGold * 0.5f;
                gold.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, rect, gold, Projectile.rotation, origin,
                    Projectile.scale * 1.08f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 天罚落雷 — 预警 14f 细雷青线 → 6f 落雷判定 (竖直雷柱, 判定与束宽对齐)。
    /// 生成点即打击点; 判定窗单次结算。
    /// </summary>
    public class JiangcenHammerSkyBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int WarnTime = 14;
        private const int StrikeTime = 6;
        private const int FadeTime = 12;
        private const float BoltTop = 520f;
        private const float HitHalfWidth = 18f;

        private float Timer => Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WarnTime + StrikeTime + FadeTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每道雷对每目标只结算一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.ai[0]++;
            if (Timer == WarnTime) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Pitch = -0.1f, Volume = 1.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.25f, Volume = 0.75f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Projectile.Center, 3f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 12; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), 0f),
                            DustID.Electric, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-7f, -2f)),
                            110, default, Main.rand.NextFloat(1f, 1.7f));
                        d.noGravity = true;
                    }
                }
            }
            if (Timer >= WarnTime)
                Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.7f, 1f));
        }

        public override bool? CanDamage() => Timer > WarnTime && Timer <= WarnTime + StrikeTime;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            var bolt = new Rectangle((int)(Projectile.Center.X - HitHalfWidth), (int)(Projectile.Center.Y - BoltTop),
                (int)(HitHalfWidth * 2f), (int)(BoltTop + 30f));
            return bolt.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Shadow, scale: 0.9f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Vector2 top = Projectile.Center - new Vector2(0f, BoltTop);
            Vector2 bottom = Projectile.Center + new Vector2(0f, 26f);

            if (Timer <= WarnTime) {
                float t = Timer / WarnTime;
                float pulse = 0.5f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f);
                ACMShaders.DrawBeam(top, bottom, 2.2f,
                    JiangcenHammerVFX.ThunderCyan with { A = 180 }, new Color(40, 70, 150, 80),
                    pulse * (0.3f + 0.7f * t), coreSharp: 2f);
            }
            else {
                float st = MathHelper.Clamp((Timer - WarnTime) / StrikeTime, 0f, 1f);
                float ft = MathHelper.Clamp((Timer - WarnTime - StrikeTime) / (float)FadeTime, 0f, 1f);
                float intensity = (1f - ft) * (0.7f + 0.3f * (1f - st));
                if (intensity > 0.02f) {
                    ACMShaders.DrawBeam(top, bottom, MathHelper.Lerp(15f, 5f, ft),
                        new Color(240, 250, 255, 235), JiangcenHammerVFX.ThunderCyan with { A = 130 },
                        intensity, flowSpeed: 3f, flowScale: 1.2f, coreSharp: 3.2f, coreGlow: 0.8f);

                    // 分叉电弧贴花: 两帧一换的横向翻转 (确定性时序, 无每帧随机分配)
                    Texture2D branch = ACMAsset.LightningBranch;
                    if (branch != null) {
                        SpriteEffects flip = (int)(Timer / 2f) % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                        Color c = JiangcenHammerVFX.ThunderCyan * (intensity * 0.85f);
                        c.A = 0;
                        float scaleY = (BoltTop + 26f) / branch.Height;
                        Main.spriteBatch.Draw(branch, top - Main.screenPosition, null, c, 0f,
                            new Vector2(branch.Width * 0.5f, 0f), new Vector2(0.8f, scaleY), flip, 0f);
                    }
                    WeaponVFX.DrawGlowBurst(bottom, 1.6f * intensity, JiangcenHammerVFX.ThunderCyan * (0.8f * intensity));
                }
            }
            return false;
        }
    }

    /// <summary>点将虚影锤 — 延迟后瞬落, 触及点将线起爆 (130px 圆, 单次)。ai[0]=延迟, ai[1]=落点 Y。</summary>
    public class JiangcenHammerEchoStrike : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";

        private const int BoomTime = 14;

        private float Delay => Projectile.ai[0];
        private float Timer => Projectile.localAI[0];
        private bool Falling => Projectile.localAI[1] == 1f;
        private bool Boomed => Projectile.localAI[1] == 2f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            if (!Falling && !Boomed) {
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = MathHelper.PiOver2 + MathF.Sin(Timer * 0.25f) * 0.1f;
                if (Timer >= Delay) {
                    Projectile.localAI[1] = 1f; // 瞬落 = 一帧 set
                    Projectile.velocity = new Vector2(0f, 30f);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.2f }, Projectile.Center);
                }
                return;
            }

            if (Falling) {
                Projectile.rotation = MathHelper.PiOver2;
                if (Projectile.Center.Y >= Projectile.ai[1]) {
                    // 触线起爆
                    Projectile.localAI[1] = 2f;
                    Projectile.localAI[0] = 0f;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.timeLeft = BoomTime;
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.25f, Volume = 0.9f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f }, Projectile.Center);
                    WeaponVFX.AddScreenShake(Projectile.Center, 3f);
                    if (!Main.dedServ) {
                        for (int i = 0; i < 12; i++) {
                            Dust d = Dust.NewDustPerfect(Projectile.Center,
                                i % 2 == 0 ? DustID.Electric : DustID.GoldFlame,
                                Main.rand.NextVector2Circular(7f, 4f) - new Vector2(0f, 2f), 110, default,
                                Main.rand.NextFloat(1.2f, 2f));
                            d.noGravity = true;
                        }
                    }
                }
            }
        }

        public override bool? CanDamage() => Boomed && Timer < 3f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => VaultUtils.CircleIntersectsRectangle(Projectile.Center, 130f, targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 1.1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            if (Boomed) {
                float t = Timer / BoomTime;
                WeaponVFX.DrawShockwaveRing(Projectile.Center, MathHelper.Lerp(24f, 150f, 1f - MathF.Pow(1f - t, 3f)),
                    12f, (1f - t) * 0.85f, JiangcenHammerVFX.ArmyGold, new Color(120, 90, 20));
                WeaponVFX.DrawGlowBurst(Projectile.Center, 2f * (1f - t), JiangcenHammerVFX.ArmyGold * (0.9f * (1f - t)));
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = tex.GetRectangle();
            float fadeIn = MathHelper.Clamp(Timer / 10f, 0f, 1f);
            float ghost = Falling ? 0.85f : 0.45f * fadeIn;

            // 虚影锤: 军金加性描影 (非实体感)
            Color gold = JiangcenHammerVFX.ArmyGold * ghost;
            gold.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rect, gold,
                Projectile.rotation, rect.Size() / 2f, 0.72f, SpriteEffects.None, 0f);
            if (Falling)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 1f, JiangcenHammerVFX.ArmyGold * 0.5f);
            return false;
        }
    }
}
