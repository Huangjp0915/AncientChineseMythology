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
    /// 鬼牙 — 赢勾掉落近战刀。左键三段连斩 (斩3 挥出鬼牙刀气);
    /// 右键布"冥刃斩线": 预警线 → 双冥刃对冲 → 交叉处决; 每第 3 条线展开米字刀阵。
    /// 机制为赢勾"SaberHell 刀阵 / 居合斩线"的玩家化直译 (Docs/WeaponRedo/BossScatter.md §3.4)。
    /// </summary>
    public class YingouKnife : ModItem
    {
        private int comboStep;      // 左键连段 0/1/2 (2=重斩)
        private int lineCount;      // 右键斩线计数 (第 3 条 → 米字刀阵)
        private uint lastUseTick;   // 连段超时 (60f 未挥重置)

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 342;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 12;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = 2000;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.shootSpeed = 8f;
            Item.shoot = ModContent.ProjectileType<SaberHellFriendly>();
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (Main.GameUpdateCount - lastUseTick > 60)
                comboStep = 0;

            if (player.altFunctionUse == 2) {
                // 右键·布线: 34f 出手成本, 该次不带近战判定
                Item.useTime = Item.useAnimation = 34;
                Item.noMelee = true;
                Item.scale = 1f;
                Item.UseSound = SoundID.Item71 with { Pitch = -0.3f };
            }
            else {
                bool heavy = comboStep == 2;
                Item.useTime = Item.useAnimation = heavy ? 15 : 12;
                Item.noMelee = false;
                Item.scale = heavy ? 1.15f : 1f;
                Item.UseSound = heavy
                    ? SoundID.Item71 with { Pitch = 0.15f, Volume = 1.1f }
                    : SoundID.Item1 with { Pitch = Main.rand.NextFloat(-0.1f, 0.1f) };
            }
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            lastUseTick = Main.GameUpdateCount;

            if (player.altFunctionUse == 2) {
                // 光标处布冥刃斩线 (限距 1400px); 线方向 = 垂直于视线 (沿用旧语义)
                Vector2 target = Main.MouseWorld;
                Vector2 toTarget = target - player.Center;
                const float maxRange = 1400f;
                if (toTarget.Length() > maxRange)
                    target = player.Center + toTarget.SafeNormalize(Vector2.UnitX) * maxRange;
                Vector2 lineDir = velocity.GetNormalVector().SafeNormalize(Vector2.UnitY);

                if (++lineCount >= 3) {
                    lineCount = 0;
                    // 大招·米字刀阵: 3 条线 0°/60°/120°, 0/8/16f 错拍对冲
                    SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.1f, Volume = 1.1f }, target);
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(source, target,
                            lineDir.RotatedBy(MathHelper.Pi / 3f * i), type,
                            damage, knockback, player.whoAmI, i * 8f);
                    }
                }
                else {
                    Projectile.NewProjectile(source, target, lineDir, type, damage, knockback, player.whoAmI, 0f);
                }
                return false;
            }

            // 左键连段: 斩3 挥出鬼牙刀气
            if (comboStep == 2 && player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(source, player.Center, velocity.SafeNormalize(Vector2.UnitX) * 12.5f,
                    ModContent.ProjectileType<YingouKnifeBladeWave>(), (int)(damage * 0.9f), knockback * 0.6f,
                    player.whoAmI);
            }
            comboStep = (comboStep + 1) % 3;
            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 本体近战命中: 小幽魂爆 + 轻震
            WeaponVFX.AddScreenShake(target.Center, 1f);
            ACMWeaponBurst.Spawn(player.GetSource_ItemUse(Item), target.Center,
                ACMWeaponBurst.Soul, scale: 0.75f, owner: player.whoAmI);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            // 挥砍弧内冷青/幽紫火花 (节流)
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(hitbox.TopLeft(), hitbox.Width, hitbox.Height,
                    Main.rand.NextBool() ? DustID.IceTorch : DustID.PurpleTorch, 0f, 0f, 120, default, 1.3f);
                d.noGravity = true;
                d.velocity *= 0.6f;
            }
        }
    }

    /// <summary>鬼牙系冷光斩线绘制 (YingouKnifeArc.fx, 本武器专属)。</summary>
    internal static class YingouKnifeVFX
    {
        public static readonly Color ColdCore = new(210, 240, 255);
        public static readonly Color GhostViolet = new(120, 90, 200);

        /// <summary>
        /// 画一条完整波形斩线 (uProgress 0~1: 预警→爆宽→残光)。须在有活动批的阶段调用。
        /// shader 缺失时退化为 BeamGrad 双态。
        /// </summary>
        public static void DrawSlashLine(Vector2 worldStart, Vector2 worldEnd, float halfWidth,
            float progress, float intensity) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            Effect fx = WeaponVFX.GetEffect("YingouKnifeArc");
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null) {
                float snap = MathHelper.Clamp((progress - 0.38f) / 0.14f, 0f, 1f);
                ACMShaders.DrawBeam(worldStart, worldEnd, MathHelper.Lerp(2.5f, halfWidth, snap),
                    ColdCore with { A = 220 }, GhostViolet with { A = 120 },
                    intensity * (1f - MathHelper.Clamp((progress - 0.52f) / 0.48f, 0f, 1f)));
                return;
            }

            Vector2 a = worldStart - Main.screenPosition;
            Vector2 b = worldEnd - Main.screenPosition;
            if ((b - a).LengthSquared() < 1f)
                return;
            var verts = ACMUtils.BuildRibbonStrip([a, b], _ => halfWidth, _ => Color.White, 0f, 1);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(ColdCore.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue((GhostViolet with { A = 130 }).ToVector4());
            fx.Parameters["uColorWarn"]?.SetValue(TelegraphColors.Lethal.ToVector4());

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }

    /// <summary>
    /// 冥刃斩线控制器 (类名保留) — 预警 24f → 双冥刃 480px 对冲 → 交叉帧 240px 处决斩 (×1.5)
    /// → 残光。velocity=线方向 (不位移); ai[0]=起手延迟 (米字刀阵错拍)。
    /// </summary>
    internal class SaberHellFriendly : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float HalfLen = 480f;
        private const int WarnTime = 24;
        private const int FlightTime = 10;
        private const int AfterTime = 29;
        private const int ActiveLife = WarnTime + FlightTime + AfterTime; // 63 → 与着色器 0.38/0.52 分段对齐

        private int crossFlash; // 交叉爆闪帧倒计时 (确定性时序, 各端一致)

        private float Delay => Projectile.ai[0];
        private float Timer => Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 63 + 40; // delay 上限 16 + 缓冲
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 处决斩对每目标只结算一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY); // 只作方向存储
            Projectile.localAI[0]++;
            if (crossFlash > 0)
                crossFlash--;

            float t = Timer - Delay;
            if (t < 0)
                return;

            if (t == 1f)
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.8f }, Projectile.Center);

            if (t == WarnTime - 6f)
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.3f, Volume = 0.9f }, Projectile.Center); // 定拍提示

            if (t == WarnTime) {
                // 双冥刃自线两端对冲 (owner 端生成)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.3f, Volume = 1.1f }, Projectile.Center);
                if (Projectile.owner == Main.myPlayer) {
                    Vector2 dir = Projectile.velocity;
                    int bladeDamage = Projectile.damage; // 刃 1× 档; 控制器处决斩经 ModifyHitNPC 升 1.5×
                    for (int s = -1; s <= 1; s += 2) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                            Projectile.Center + dir * (HalfLen * s), dir * (-s * 24f),
                            ModContent.ProjectileType<YingouKnifePhantomBlade>(), bladeDamage, Projectile.knockBack,
                            Projectile.owner, Projectile.Center.X, Projectile.Center.Y);
                    }
                }
            }

            if (t == WarnTime + FlightTime) {
                // 交叉帧: 十字爆闪 + 处决窗开启
                crossFlash = 14;
                WeaponVFX.AddScreenShake(Projectile.Center, 3f);
                SoundEngine.PlaySound(SoundID.Item89 with { Pitch = 0.2f, Volume = 1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.4f, Volume = 0.7f }, Projectile.Center);
                if (Projectile.owner == Main.myPlayer) {
                    ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
                        ACMWeaponBurst.Fatal, scale: 1.3f, owner: Projectile.owner);
                }
            }

            if (t >= ActiveLife)
                Projectile.Kill();
        }

        public override bool? CanDamage() {
            float t = Timer - Delay;
            return t >= WarnTime + FlightTime && t < WarnTime + FlightTime + 2f; // 处决斩 2f 判定窗
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => VaultUtils.CircleIntersectsRectangle(Projectile.Center, 240f, targetHitbox);

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage *= 1.5f; // 处决斩
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float t = Timer - Delay;
            if (t < 0)
                return false;

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 start = Projectile.Center - dir * HalfLen;
            Vector2 end = Projectile.Center + dir * HalfLen;
            float progress = MathHelper.Clamp(t / ActiveLife, 0f, 1f);
            YingouKnifeVFX.DrawSlashLine(start, end, 46f, progress, 0.95f);

            // 交叉十字爆闪 (SlashBurst 双张 ±45°, 弹出即衰)
            if (crossFlash > 0 && ACMAsset.SlashBurst != null) {
                float ft = crossFlash / 14f;
                float pop = 1f - MathF.Pow(1f - ft, 3f);
                Texture2D tex = ACMAsset.SlashBurst;
                float baseRot = dir.ToRotation();
                Color c = YingouKnifeVFX.ColdCore * (ft * 0.95f);
                c.A = 0;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(tex, drawPos, null, c, baseRot + MathHelper.PiOver4,
                    tex.Size() * 0.5f, new Vector2(1.6f, 0.55f) * pop, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(tex, drawPos, null, c * 0.8f, baseRot - MathHelper.PiOver4,
                    tex.Size() * 0.5f, new Vector2(1.3f, 0.45f) * pop, SpriteEffects.None, 0f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 1.8f * ft, YingouKnifeVFX.ColdCore * (0.8f * ft));
            }
            return false;
        }
    }

    /// <summary>对冲冥刃 — 沿斩线 48px/f (含 extraUpdates) 突进, 过交叉点 240px 后减速溶散。ai=交叉点坐标。</summary>
    internal class YingouKnifePhantomBlade : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Yingous/YingouHand";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 单刃对每目标只结算一次
            Projectile.alpha = 60;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            Vector2 cross = new(Projectile.ai[0], Projectile.ai[1]);
            Vector2 fromCross = Projectile.Center - cross;
            bool passed = Vector2.Dot(fromCross, Projectile.velocity) > 0f && fromCross.Length() > 240f;
            if (passed) {
                Projectile.velocity *= 0.86f;
                Projectile.alpha += 11; // extraUpdates 下每游戏帧 +22
                if (Projectile.alpha >= 250)
                    Projectile.Kill();
            }

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    DustID.IceTorch, -Projectile.velocity * 0.06f, 130, default, 1.2f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.35f, 0.5f, 0.8f) * (1f - Projectile.alpha / 255f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 2f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Soul, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float visible = 1f - Projectile.alpha / 255f;

            // 双层冷光拖尾 (速度门控)
            float speedGate = MathHelper.Clamp((Projectile.velocity.Length() - 8f) / 16f, 0f, 1f);
            if (speedGate > 0.05f) {
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 16f,
                    outerColor: (YingouKnifeVFX.GhostViolet * speedGate) with { A = 140 },
                    innerColor: (YingouKnifeVFX.ColdCore * speedGate) with { A = 200 },
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2.4f);
            }

            // 残影 (速度门控) + 主体冷色调
            Color tint = Color.Lerp(lightColor, YingouKnifeVFX.ColdCore, 0.65f) * visible;
            float sengs = 0.35f * speedGate;
            for (int i = 0; i < Projectile.oldPos.Length; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color c = tint * sengs;
                c.A = 0;
                Main.spriteBatch.Draw(tex, pos, null, c, Projectile.rotation, tex.Size() / 2f,
                    Projectile.scale, SpriteEffects.None, 0f);
                sengs *= 0.75f;
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, tint,
                Projectile.rotation, tex.Size() / 2f, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>鬼牙刀气 — 斩3 挥出的短程弧形刃波 (~380px 射程)。</summary>
    internal class YingouKnifeBladeWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.ai[0]++;
            Projectile.velocity *= 0.97f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.IceTorch, -Projectile.velocity * 0.08f, 140, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            WeaponVFX.AddScreenShake(target.Center, 1f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Soul, scale: 0.7f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || ACMAsset.SlashBurst == null)
                return false;
            Texture2D tex = ACMAsset.SlashBurst;

            float grow = MathHelper.Clamp(Projectile.ai[0] / 8f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            float scale = MathHelper.Lerp(0.8f, 1.25f, grow);

            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 14f * fade,
                outerColor: YingouKnifeVFX.GhostViolet with { A = 120 },
                innerColor: YingouKnifeVFX.ColdCore with { A = 180 },
                uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color edge = YingouKnifeVFX.GhostViolet * (0.7f * fade);
            edge.A = 0;
            Color core = YingouKnifeVFX.ColdCore * (0.9f * fade);
            core.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, edge, Projectile.rotation,
                tex.Size() * 0.5f, new Vector2(1.15f, 0.8f) * scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, core, Projectile.rotation,
                tex.Size() * 0.5f, new Vector2(0.95f, 0.55f) * scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
