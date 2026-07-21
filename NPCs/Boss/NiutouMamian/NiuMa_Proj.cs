using AncientChineseMythology.Underworlds;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    /// <summary>
    /// 入场演出载体「鬼门开」: 地面鬼门法阵展开 → 双魂柱升起 → 门破, 牛头马面踏出。
    /// 视觉全程本地 (法阵经 NiuMaScreenSystem 连续槽), NPC 生成仅服务器。
    /// </summary>
    public class SpoawnProj : ModProjectile
    {
        public static void CreatNPC(Vector2 Position) {
            Projectile.NewProjectileDirect(null, Position, new Vector2(0, -8), ModContent.ProjectileType<SpoawnProj>(), 0, 0);
        }
        public override string Texture => NiuMaHelper.NothingTex_Path;

        private const int GateOpenStart = 50;   // 门开始撕开
        private const int BurstFrame = 140;     // 门破 + 生成双吏
        private const int EndFrame = 175;

        public override void SetDefaults() {
            Projectile.aiStyle = -1;
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = EndFrame + 10;
            Projectile.netImportant = true;
        }
        public override bool? CanDamage() => false;

        public override void AI() {
            float t = ++Projectile.ai[0];
            Projectile.velocity *= 0.94f;

            float open = MathHelper.Clamp((t - GateOpenStart) / (BurstFrame - GateOpenStart - 10), 0f, 1f);
            float inten = MathHelper.Clamp(t / 40f, 0f, 1f);
            if (t > BurstFrame + 8)
                inten = MathHelper.Clamp(1f - (t - BurstFrame - 8) / 24f, 0f, 1f);

            if (!Main.dedServ) {
                NiuMaScreenSystem.Publish(Projectile.Center, 0.22f * inten);
                NiuMaScreenSystem.PublishGate(Projectile.Center, 300f + 60f * open, open, inten);

                // 轻镜头: 引向鬼门 (仅本地玩家, 变焦克制)
                var scr = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
                scr.SetScreenPos(Projectile.Center + new Vector2(0, -60));
                scr.SetZoom(1.22f);

                // 收束魂火: 密度 ∝ sqrt(open), 门破前 28% 静默 (先聚气后屏息)
                if (open < 0.72f && Main.rand.NextFloat() < 0.25f + 0.6f * MathF.Sqrt(MathHelper.Max(open, 0.05f))) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + a.ToRotationVector2() * Main.rand.NextFloat(220f, 460f);
                    var d = Dust.NewDustPerfect(pos, ModContent.DustType<Dust_1>());
                    d.color = Main.rand.NextBool() ? NiuMaHelper.GhostViolet : NiuMaHelper.GhostCore;
                    d.color.A = 255;
                    d.scale *= 1.8f;
                    d.velocity = (Projectile.Center - pos).NormalizeVector() * NiuMaHelper.Rand_Float(3f, 6.5f);
                }

                // 渐强轰鸣震屏 (t² 曲线)
                if (t > GateOpenStart && t < BurstFrame)
                    ACMScreenShakeSystem.Add(open * open * 3.5f);
            }

            if (t == GateOpenStart)
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.7f, Volume = 0.9f }, Projectile.Center);

            if (t == (int)BurstFrame) {
                // —— 门破: 双吏踏出 ——
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.9f }, Projectile.Center);
                ACMScreenShakeSystem.Add(9f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 34; i++) {
                        var d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<Dust_1>());
                        d.color = i % 2 == 0 ? NiuMaHelper.EmberRed : NiuMaHelper.GhostViolet;
                        d.color.A = 255;
                        d.scale *= 2.4f;
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(3, 11)).RotatedByRandom(8);
                    }
                }
                if (Main.netMode != NetmodeID.MultiplayerClient && Projectile.localAI[0] == 0f) {
                    Projectile.localAI[0] = 1f;
                    NPC.NewNPC(Projectile.GetSource_FromThis(), (int)Projectile.Center.X - 250, (int)Projectile.Center.Y, ModContent.NPCType<NiuTou>());
                    NPC.NewNPC(Projectile.GetSource_FromThis(), (int)Projectile.Center.X + 250, (int)Projectile.Center.Y, ModContent.NPCType<MaMian>());
                }
            }

            if (t >= EndFrame)
                Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            // 兜底: 演出载体被提前销毁 (掉线/清弹) 时仍保证双吏生成
            if (Main.netMode != NetmodeID.MultiplayerClient && Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                NPC.NewNPC(Projectile.GetSource_FromThis(), (int)Projectile.Center.X - 250, (int)Projectile.Center.Y, ModContent.NPCType<NiuTou>());
                NPC.NewNPC(Projectile.GetSource_FromThis(), (int)Projectile.Center.X + 250, (int)Projectile.Center.Y, ModContent.NPCType<MaMian>());
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float t = Projectile.ai[0];
            float open = MathHelper.Clamp((t - GateOpenStart) / (BurstFrame - GateOpenStart - 10), 0f, 1f);
            if (open <= 0.02f || t > BurstFrame + 14)
                return false;

            // 门内双魂柱: 左熔红 (牛头位), 右幽紫 (马面位), 随开门升起
            float rise = ACMUtils.QuadOut(open);
            Vector2 baseL = Projectile.Center + new Vector2(-250, 60);
            Vector2 baseR = Projectile.Center + new Vector2(250, 60);
            ACMShaders.DrawBeam(baseL, baseL - new Vector2(0, 90f + 560f * rise), 26f * open,
                NiuMaHelper.EmberRed, new Color(90, 20, 20), 0.85f * open, 2.2f, 2.4f);
            ACMShaders.DrawBeam(baseR, baseR - new Vector2(0, 90f + 560f * rise), 26f * open,
                NiuMaHelper.GhostViolet, new Color(45, 25, 90), 0.85f * open, 2.2f, 2.4f);
            return false;
        }
    }

    /// <summary>牛头冲锋沿途的驻留血焰刻痕 (短命拉伸光束, 致命红)。ai[1]=尺寸。</summary>
    public class Proj_756_Adjust : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void AI() {
            const int fadeIn = 12;
            const int fadeOutStart = 26;
            const int killAt = 36;
            Projectile.ai[0] += 1f;
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            if (Projectile.ai[0] < fadeIn) {
                Projectile.Opacity += 0.1f;
                Projectile.scale = Projectile.Opacity * Projectile.ai[1];
            }
            if (Projectile.ai[0] >= fadeOutStart)
                Projectile.Opacity -= 0.14f;
            if (Projectile.ai[0] >= killAt)
                Projectile.Kill();
        }
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.Opacity = 0;
            Projectile.timeLeft = 60;
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var t = ACMAsset.LightShot; // 64x64, 朝向右侧
            if (t == null)
                return false;
            var sb = Main.spriteBatch;
            var origin = new Vector2(0, t.Height * 0.5f);
            float len = 200f * Projectile.scale / t.Width;
            var glow = new Color(180, 30, 30) { A = 0 };
            var core = new Color(255, 120, 110) { A = 0 };
            sb.Draw(t, Projectile.Center - Main.screenPosition, null, glow * Projectile.Opacity, Projectile.rotation, origin, new Vector2(len, Projectile.scale) * Projectile.Opacity, SpriteEffects.None, 0f);
            sb.Draw(t, Projectile.Center - Main.screenPosition, null, core * Projectile.Opacity * 0.8f, Projectile.rotation, origin, new Vector2(len, Projectile.scale * 0.45f) * Projectile.Opacity, SpriteEffects.None, 0f);
            return false;
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center,
                Projectile.Center + Projectile.velocity.SafeNormalize(-Vector2.UnitY) * 200f * Projectile.scale, 22f * Projectile.scale, ref p);
        }
    }

    /// <summary>拘魂锁链命中标记: 短促牵引 (0.66s), 被拽向牛头但保留操作权 (公平阀门: 不定身)。</summary>
    public class ChainProj_Buff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            int who = -1;
            NPC niu = NiuMaHelper.FindBoss(ModContent.NPCType<NiuTou>(), ref who);
            if (niu == null)
                return;
            if (player.Distance(niu.Center) < 200f)
                return;
            Vector2 pull = (niu.Center - player.Center).NormalizeVector() * 1.3f;
            player.velocity = player.velocity * 0.88f + pull;
        }
    }

    /// <summary>
    /// 拘魂锁链: 直线射出 (致命红) → 命中挂牵引标记后回收 (幽紫, 非致命)。
    /// ai[2]=所属 NPC; ai[1]: 0=射出 1=回收。
    /// </summary>
    public class ChainProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        private NPC Owner {
            get {
                int who = (int)Projectile.ai[2];
                return (who >= 0 && who < Main.maxNPCs) ? Main.npc[who] : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2100;
        }

        public override void AI() {
            NPC owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }
            Projectile.ai[0]++;

            if (Projectile.ai[1] == 0) {
                // 射出: 定速直线; 超程或超时转回收
                if (Projectile.ai[0] > 55 || Vector2.Distance(owner.Center, Projectile.Center) > 1350f)
                    Projectile.ai[1] = 1;
            }
            else {
                // 回收: 动态朝所属者收拢
                Projectile.velocity = (owner.Center - Projectile.Center).NormalizeVector() * 34f;
                if (Vector2.Distance(owner.Center, Projectile.Center) < 110f)
                    Projectile.Kill();
            }

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.velocity = Projectile.velocity * 0.1f;
            }
        }

        public override bool? CanDamage() => Projectile.ai[1] == 0;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC owner = Owner;
            if (owner == null || !owner.active)
                return false;
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), owner.Center, Projectile.Center, 12f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<ChainProj_Buff_1>(), 40);
            UnderworldField.AddSoulErosion(target, 1);
            ACMScreenShakeSystem.Add(5f);
            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.4f }, target.Center);
            Projectile.ai[1] = 1;
            Projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = Owner;
            if (Main.dedServ || owner == null || !owner.active)
                return false;

            bool lethal = Projectile.ai[1] == 0;
            Color beamCore = lethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            Color beamEdge = lethal ? new Color(110, 20, 28) : new Color(60, 30, 110);
            ACMShaders.DrawBeam(owner.Center, Projectile.Center, lethal ? 9f : 6f, beamCore, beamEdge, lethal ? 1f : 0.7f);

            // 实体锁链 + 链头
            var sb = Main.spriteBatch;
            var t = TextureAssets.Chains[0].Value;
            Vector2 dirv = Projectile.Center - owner.Center;
            float dist = dirv.Length();
            float rot = dirv.ToRotation() - MathHelper.PiOver2;
            var rec = new Rectangle(0, 0, t.Width, (int)(dist * 0.95f));
            sb.Draw(t, owner.Center - Main.screenPosition, rec, Color.DarkGray, rot, new Vector2(t.Width * 0.5f, 0), 1f, SpriteEffects.None, 0);
            var t2 = TextureAssets.Projectile[234].Value;
            sb.Draw(t2, Projectile.Center - Main.screenPosition, null, Color.DarkGray, rot, t2.Size() * 0.5f, 1.1f, SpriteEffects.None, 0);
            var glow = ACMAsset.SoftGlow;
            var gcol = (lethal ? NiuMaHelper.EmberRed : NiuMaHelper.GhostViolet) with { A = 0 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, gcol * 0.9f, 0, glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>凝魂之眼命中印记: 魂目侵蚀, 小幅破防 (5s), 不再腰斩生命/输出。</summary>
    public class EyeProj_Buff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            player.statDefense -= 10;
        }
    }

    /// <summary>凝魂之眼: 缓速追魂鬼目 (上限 8.5px/f, 恒可甩开), 命中挂魂蚀+黑暗+破防。</summary>
    public class EyeProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 330;
            Projectile.penetrate = 1;
        }
        private Player player => Main.player[Projectile.owner];
        public override void AI() {
            Vector2 want = (player.Center - Projectile.Center).NormalizeVector() * 8.5f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.03f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.25f;
                d.scale = 1.3f;
            }
            Lighting.AddLight(Projectile.Center, NiuMaHelper.GhostViolet.ToVector3() * 0.45f);
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.Kill();
            UnderworldField.AddSoulErosion(target, 2);
            target.AddBuff(BuffID.Darkness, 240);
            target.AddBuff(ModContent.BuffType<EyeProj_Buff_1>(), 300);
        }
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var shot = ACMAsset.LightShot;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
            var halo = TelegraphColors.NetherViolet with { A = 0 };
            var pupil = new Color(220, 120, 255) { A = 0 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, halo * 0.85f, 0, glow.Size() * .5f, 0.95f * pulse, default, 0);
            sb.Draw(shot, Projectile.Center - Main.screenPosition, null, pupil * 0.9f, Projectile.velocity.ToRotation(), shot.Size() * .5f, 0.55f * pulse, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, (NiuMaHelper.EmberRed with { A = 0 }) * 0.65f, 0, glow.Size() * .5f, 0.32f * pulse, default, 0);
            return false;
        }
    }

    /// <summary>
    /// 魂火弹: 马面/令符的基础弹。ai[1]: 0=缓转向追踪 (上限 11px/f) 1=直线。
    /// 命中只挂 1 层魂蚀 (废除 29 连 debuff)。
    /// </summary>
    public class DarkGreenProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
            Projectile.penetrate = 1;
        }
        private Player player => Main.player[Projectile.owner];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void AI() {
            if (Projectile.ai[1] == 0) {
                if (Projectile.velocity.Length() < 11f)
                    Projectile.velocity *= 1.03f;
                float ro = (player.Center - Projectile.Center).ToRotation();
                float cur = Projectile.velocity.ToRotation();
                float toRo = Math.Clamp(MathHelper.WrapAngle(cur.AngleLerp(ro, 0.02f) - cur), -0.02f, 0.02f);
                Projectile.velocity = Projectile.velocity.RotatedBy(toRo);
            }

            if (!Main.dedServ) {
                var ty = ModContent.DustType<Dust_2>();
                for (int i = 0; i < 2; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, ty);
                    d.color = Color.YellowGreen;
                    d.alpha /= 6;
                    d.scale *= 2.6f;
                    d.velocity = Projectile.velocity.RotatedByRandom(.3) * .5f;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.16f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            var ty = ModContent.DustType<Dust_1>();
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, ty);
                d.color = Color.YellowGreen;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
            }
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Projectile.Kill();
            UnderworldField.AddSoulErosion(target, 1);
        }
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var o = glow.Size() * .5f;
            float ro = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            var edge = TelegraphColors.NetherViolet with { A = 0 };
            var core = TelegraphColors.GhostGreen with { A = 0 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, edge * 0.75f, ro, o, 0.85f, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.95f, ro, o, 0.5f, default, 0);
            return false;
        }
    }

    /// <summary>忘川水域减速 (25%): 有边界、可出域即解 (废除 60% 版常驻)。</summary>
    public class DeclineSpeedBuff_1 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            player.Center -= player.velocity * 0.22f;
        }
    }
    /// <summary>忘川水域·怒 (38%): 仅 P3 狂怒水域使用。</summary>
    public class DeclineSpeedBuff_2 : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            player.Center -= player.velocity * 0.38f;
        }
    }

    /// <summary>
    /// 爆裂魂核: 缓飘至玩家上方 → 定悬蓄能 (收束粒子, 末段静默) → 爆成一圈直线魂火。
    /// 本体永不接触伤害; 危险全部来自可读的爆裂环。
    /// </summary>
    public class DarkGreenBoomProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        private const int DriftEnd = 70;
        private const int BoomAt = 150;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = BoomAt + 10;
            Projectile.penetrate = -1;
        }
        private Player player => Main.player[Projectile.owner];
        public override bool? CanDamage() => false;

        public override void AI() {
            float t = ++Projectile.ai[0];
            float charge = MathHelper.Clamp((t - DriftEnd) / (BoomAt - DriftEnd), 0f, 1f);

            if (t < DriftEnd) {
                Vector2 want = (player.Center + new Vector2(0, -280) - Projectile.Center).NormalizeVector() * 6f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.04f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            // 收束粒子 ∝ sqrt(charge), 72% 后静默 (屏息节拍)
            if (!Main.dedServ && charge > 0f && charge < 0.72f &&
                Main.rand.NextFloat() < 0.3f + 0.6f * MathF.Sqrt(charge)) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * Main.rand.NextFloat(70f, 170f);
                var d = Dust.NewDustPerfect(pos, DustID.CursedTorch);
                d.noGravity = true;
                d.velocity = (Projectile.Center - pos).NormalizeVector() * 4f;
            }

            if (t == BoomAt) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.9f }, Projectile.Center);
                ACMScreenShakeSystem.Add(5f);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 v = (MathHelper.TwoPi / 10f * i).ToRotationVector2() * 6.8f;
                        var p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, v,
                            ModContent.ProjectileType<DarkGreenProj>(), Projectile.damage, 1f, Projectile.owner);
                        p.ai[1] = 1f;
                        p.netUpdate = true;
                    }
                }
                if (!Main.dedServ) {
                    for (int i = 0; i < 24; i++) {
                        var d = Dust.NewDustPerfect(Projectile.Center, DustID.FireworkFountain_Green);
                        d.noGravity = true;
                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 8)).RotatedByRandom(8);
                    }
                }
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float t = Projectile.ai[0];
            float charge = MathHelper.Clamp((t - DriftEnd) / (BoomAt - DriftEnd), 0f, 1f);
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var o = glow.Size() * .5f;
            float pulse = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f);
            // 核: 体积随蓄能三次方涨 (小→骤大)
            float coreScale = 0.9f + 1.6f * charge * charge * charge;
            var edge = new Color(90, 45, 150) { A = 0 };
            var core = TelegraphColors.GhostGreen with { A = 0 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, edge * 0.8f, 0, o, (1.2f + coreScale * 0.6f) * pulse, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.95f, 0, o, coreScale * pulse, default, 0);
            // 末段预警: 即将爆裂时红环一闪
            if (charge > 0.72f) {
                var warn = TelegraphColors.Lethal with { A = 0 };
                float flick = 0.35f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f);
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null, warn * flick, 0, o, 3.0f, default, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 黄泉慢魂球: 合体技「黄泉车道」的帘幕填充弹。极慢 (由生成端限速), 恒可读。
    /// ai[0]=速度上限 (0 则 5.5)。
    /// </summary>
    public class SoulOrbProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 340;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            float max = Projectile.ai[0] > 0f ? Projectile.ai[0] : 5.5f;
            if (Projectile.velocity.Length() < max)
                Projectile.velocity *= 1.012f;
            Projectile.rotation += 0.04f;

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<Dust_2>());
                d.color = TelegraphColors.GhostGreen;
                d.alpha /= 5;
                d.scale *= 2.4f;
                d.velocity = Projectile.velocity * 0.2f;
            }
            Lighting.AddLight(Projectile.Center, 0.15f, 0.32f, 0.2f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2);
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<Dust_1>());
                d.color = TelegraphColors.GhostGreen;
                d.color.A = 255;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(1.5f, 4f)).RotatedByRandom(8);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var sb = Main.spriteBatch;
            var glow = ACMAsset.SoftGlow;
            var o = glow.Size() * .5f;
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI);
            var edge = TelegraphColors.NetherViolet with { A = 0 };
            var core = TelegraphColors.GhostGreen with { A = 0 };
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, edge * 0.7f, 0, o, 1.3f * pulse, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, core * 0.95f, 0, o, 0.7f * pulse, default, 0);
            return false;
        }
    }

    /// <summary>
    /// 燃角链锤: 牛头炮台岗掷出的弧线锤 (链连牛头), 落地爆出左右两道贴地魂火波。
    /// ai[2]=所属 NPC。
    /// </summary>
    public class NiuMaChainMace : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        private NPC Owner {
            get {
                int who = (int)Projectile.ai[2];
                return (who >= 0 && who < Main.maxNPCs) ? Main.npc[who] : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 160;
            Projectile.penetrate = -1;
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void AI() {
            Projectile.ai[0]++;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.34f, 19f);
            Projectile.rotation += 0.32f * Math.Sign(Projectile.velocity.X == 0 ? 1 : Projectile.velocity.X);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<Dust_1>());
                d.color = NiuMaHelper.EmberRed;
                d.color.A = 255;
                d.scale *= 1.6f;
                d.velocity = Projectile.velocity * 0.12f;
            }

            // 落地 (碰实体物块) 或超时 → 冲击
            if (Projectile.ai[0] > 8 && (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height) || Projectile.ai[0] >= 150))
                Impact();
        }

        private void Impact() {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1f }, Projectile.Center);
            ACMScreenShakeSystem.Add(8f);
            if (!Main.dedServ) {
                NiuMaScreenSystem.AddGateMark(Projectile.Center, 190f, 44);
                for (int i = 0; i < 26; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<Dust_1>());
                    d.color = i % 3 == 0 ? NiuMaHelper.EmberCore : NiuMaHelper.EmberRed;
                    d.color.A = 255;
                    d.scale *= 2.2f;
                    d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 9)).RotatedByRandom(8);
                }
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    var p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<NiuMaGroundFlame>(), (int)(Projectile.damage * 1.1f), 1f, Projectile.owner);
                    p.ai[0] = dir;
                    p.netUpdate = true;
                }
            }
            Projectile.Kill();
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = Owner;
            if (Main.dedServ)
                return false;
            var sb = Main.spriteBatch;
            // 链: 牛头 → 锤
            if (owner != null && owner.active) {
                var t = TextureAssets.Chains[0].Value;
                Vector2 dirv = Projectile.Center - owner.Center;
                float rot = dirv.ToRotation() - MathHelper.PiOver2;
                var rec = new Rectangle(0, 0, t.Width, (int)dirv.Length());
                sb.Draw(t, owner.Center - Main.screenPosition, rec, Color.DarkGray, rot, new Vector2(t.Width * 0.5f, 0), 1f, SpriteEffects.None, 0);
            }
            // 锤头: 链球贴图 + 熔红辉光
            var mace = TextureAssets.Projectile[234].Value;
            var glow = ACMAsset.SoftGlow;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, (NiuMaHelper.EmberRed with { A = 0 }) * 0.9f, 0, glow.Size() * 0.5f, 1.4f, default, 0);
            sb.Draw(mace, Projectile.Center - Main.screenPosition, null, Color.Lerp(Color.DarkGray, NiuMaHelper.EmberRed, 0.4f), Projectile.rotation, mace.Size() * 0.5f, 2.1f, default, 0);
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, (NiuMaHelper.EmberCore with { A = 0 }) * 0.7f, 0, glow.Size() * 0.5f, 0.6f, default, 0);
            return false;
        }
    }

    /// <summary>
    /// 贴地魂火波: 沿地表横移的火墙 (高 92px 可跳过)。ai[0]=方向; 前 12 帧无伤淡入 (公平阀门)。
    /// </summary>
    public class NiuMaGroundFlame : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 92;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 130;
            Projectile.penetrate = -1;
        }

        public override bool? CanDamage() => Projectile.ai[1] >= 12;

        public override void AI() {
            Projectile.ai[1]++;
            Projectile.velocity = new Vector2(Projectile.ai[0] * 9f, 0f);

            // 贴地: 向下找最近实体面, 底边吸附
            float groundY = FindGroundY(Projectile.Center.X, Projectile.Center.Y - 200f);
            if (groundY > 0)
                Projectile.Bottom = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Bottom.Y, groundY, 0.5f));

            if (!Main.dedServ) {
                float rise = Math.Min(Projectile.ai[1] / 12f, 1f);
                for (int i = 0; i < 2; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(NiuMaHelper.Rand_Float(-20, 20), 0), ModContent.DustType<Dust_2>());
                    d.color = Main.rand.NextBool() ? NiuMaHelper.EmberRed : NiuMaHelper.EmberCore;
                    d.alpha /= 4;
                    d.scale *= 2.2f * rise;
                    d.velocity = new Vector2(Projectile.ai[0] * 1.5f, -NiuMaHelper.Rand_Float(2f, 5f));
                }
            }
            Lighting.AddLight(Projectile.Center, NiuMaHelper.EmberRed.ToVector3() * 0.5f);
        }

        private static float FindGroundY(float worldX, float searchStartY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = Math.Max((int)(searchStartY / 16f), 10);
            for (int tileY = startTileY; tileY < Math.Min(startTileY + 46, Main.maxTilesY - 10); tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && WorldGen.SolidTile(tileX, tileY))
                    return tileY * 16f;
            }
            return -1f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            var sb = Main.spriteBatch;
            var shot = ACMAsset.LightShot;
            float rise = Math.Min(Projectile.ai[1] / 12f, 1f);
            float fade = Math.Min(Projectile.timeLeft / 20f, 1f) * rise;
            float wobble = 1f + 0.12f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.whoAmI * 2f);
            Vector2 basePos = Projectile.Bottom - Main.screenPosition;
            var edge = NiuMaHelper.EmberRed with { A = 0 };
            var core = NiuMaHelper.EmberCore with { A = 0 };
            // 竖向火舌: 宽晕 + 亮芯 (LightShot 旋转 90° 拉伸)
            sb.Draw(shot, basePos, null, edge * (0.85f * fade), -MathHelper.PiOver2, new Vector2(0, shot.Height * 0.5f),
                new Vector2(Projectile.height / 64f * 1.25f * wobble, 1.5f), default, 0);
            sb.Draw(shot, basePos, null, core * (0.9f * fade), -MathHelper.PiOver2, new Vector2(0, shot.Height * 0.5f),
                new Vector2(Projectile.height / 64f * wobble, 0.62f), default, 0);
            return false;
        }
    }

    /// <summary>
    /// 拘魂令符: 马面缠斗岗沿弧线布下的符牌。显形悬停 → 锁定瞄线 (幽紫→红) → 齐发直线魂火。
    /// ai[0]=计时; ai[1]=开火帧 (生成端错开)。本体无接触伤害。
    /// </summary>
    public class NiuMaWritProj : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        private Vector2 lockedAim;
        private bool aimLocked;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 200;
            Projectile.penetrate = -1;
        }
        public override bool? CanDamage() => false;

        private Player player => Main.player[Projectile.owner];

        public override void AI() {
            float t = ++Projectile.ai[0];
            float fireAt = Projectile.ai[1] <= 0 ? 70f : Projectile.ai[1];
            Projectile.velocity *= 0.86f;

            // 锁定: 开火前 10 帧定格瞄线 (之后不再追踪 → 位移即可躲)
            if (!aimLocked && t >= fireAt - 10f) {
                aimLocked = true;
                lockedAim = (player.Center - Projectile.Center).NormalizeVector(Vector2.UnitX);
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }

            if (t == (int)fireAt) {
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.7f }, Projectile.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 aim = (player.Center - Projectile.Center).NormalizeVector(Vector2.UnitX);
                    if (aimLocked)
                        aim = lockedAim;
                    var p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, aim * 13f,
                        ModContent.ProjectileType<DarkGreenProj>(), Projectile.damage, 1f, Projectile.owner);
                    p.ai[1] = 1f;
                    p.netUpdate = true;
                }
                Projectile.velocity -= (aimLocked ? lockedAim : Vector2.UnitX) * 4f; // 后坐
            }

            if (t > fireAt + 14f)
                Projectile.Kill();

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14, 18), DustID.PurpleTorch);
                d.noGravity = true;
                d.velocity = new Vector2(0, -1.2f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(1, 3)).RotatedByRandom(8);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float t = Projectile.ai[0];
            float fireAt = Projectile.ai[1] <= 0 ? 70f : Projectile.ai[1];
            float grow = ACMUtils.BackOut(Math.Min(t / 14f, 1f));

            var sb = Main.spriteBatch;
            var shot = ACMAsset.LightShot;
            var glow = ACMAsset.SoftGlow;

            // 瞄线: 幽紫渐强, 锁定后转红
            if (t > 16f && t < fireAt + 4f) {
                Vector2 aim = aimLocked ? lockedAim : (player.Center - Projectile.Center).NormalizeVector(Vector2.UnitX);
                bool lethal = aimLocked;
                Color c = lethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                float inten = lethal ? 0.9f : 0.4f * Math.Min((t - 16f) / 30f, 1f);
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + aim * 1500f, lethal ? 7f : 4f, c, new Color(60, 30, 110), inten);
            }

            // 符牌: 竖长光牌 + 辉光
            float bob = 3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.4f + Projectile.whoAmI);
            Vector2 pos = Projectile.Center - Main.screenPosition + new Vector2(0, bob);
            var vio = NiuMaHelper.GhostViolet with { A = 0 };
            var grn = NiuMaHelper.GhostCore with { A = 0 };
            sb.Draw(glow, pos, null, vio * 0.85f * grow, 0, glow.Size() * 0.5f, 0.9f * grow, default, 0);
            sb.Draw(shot, pos, null, vio * 0.95f * grow, MathHelper.PiOver2, shot.Size() * 0.5f, new Vector2(0.85f, 0.32f) * grow, default, 0);
            sb.Draw(shot, pos, null, grn * 0.8f * grow, MathHelper.PiOver2, shot.Size() * 0.5f, new Vector2(0.55f, 0.16f) * grow, default, 0);
            return false;
        }
    }

    /// <summary>
    /// 勾魂锁命链: 合体技 C2 的旋转锁链 (连接两吏)。结链期无害 (幽紫), 绷紧后致命 (红),
    /// 中点 165px 为安全命门 (翠玉法印) — 可从中央穿过。两吏状态退出即自毁。
    /// ai[0]=牛头 whoAmI; ai[1]=马面 whoAmI。
    /// </summary>
    public class NiuMaLinkChain : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public const float SafeGap = 165f;

        private NPC Niu => Get((int)Projectile.ai[0]);
        private NPC Ma => Get((int)Projectile.ai[1]);
        private static NPC Get(int who) => (who >= 0 && who < Main.maxNPCs) ? Main.npc[who] : null;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 420;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        private bool Lethal {
            get {
                NPC niu = Niu;
                return niu != null && niu.active && niu.ai[0] == NiuMaBoss.StLink && niu.ai[1] >= 110f && niu.ai[1] < 382f;
            }
        }

        public override void AI() {
            NPC niu = Niu;
            NPC ma = Ma;
            if (niu == null || !niu.active || ma == null || !ma.active || niu.ai[0] != NiuMaBoss.StLink) {
                Projectile.Kill();
                return;
            }
            // 382f = 断链节拍: 链体炸裂收场, 两吏反冲退场
            if (niu.ai[1] >= 382f) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = (niu.Center + ma.Center) * 0.5f;

            if (!Main.dedServ) {
                // 中点法印 (安全命门, 翠玉)
                NiuMaScreenSystem.PublishGate(Projectile.Center, SafeGap, 0.6f, Lethal ? 0.85f : 0.45f, jade: true);
                if (Lethal && Main.rand.NextBool(2)) {
                    float f = Main.rand.NextFloat();
                    Vector2 pos = Vector2.Lerp(niu.Center, ma.Center, f);
                    if (Vector2.Distance(pos, Projectile.Center) > SafeGap) {
                        var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                        d.noGravity = true;
                        d.velocity = (ma.Center - niu.Center).NormalizeVector().RotatedBy(MathHelper.PiOver2) * NiuMaHelper.Rand_Float(-2, 2);
                    }
                }
            }
        }

        public override bool? CanDamage() => Lethal;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC niu = Niu;
            NPC ma = Ma;
            if (niu == null || ma == null || !niu.active || !ma.active)
                return false;
            // 中点安全命门内不判定
            Vector2 mid = (niu.Center + ma.Center) * 0.5f;
            if (Vector2.Distance(targetHitbox.Center.ToVector2(), mid) < SafeGap)
                return false;
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), niu.Center, ma.Center, 16f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            UnderworldField.AddSoulErosion(target, 2);
            ACMScreenShakeSystem.Add(4f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.5f, Volume = 0.9f }, Projectile.Center);
            for (int i = 0; i < 18; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(120, 120), DustID.Shadowflame);
                d.noGravity = true;
                d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC niu = Niu;
            NPC ma = Ma;
            if (Main.dedServ || niu == null || ma == null || !niu.active || !ma.active)
                return false;

            bool lethal = Lethal;
            float form = MathHelper.Clamp((niu.ai[1] - 50f) / 60f, 0f, 1f);
            Color core = lethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            Color edge = lethal ? new Color(120, 20, 30) : new Color(55, 30, 105);
            Vector2 mid = (niu.Center + ma.Center) * 0.5f;
            Vector2 dir = (ma.Center - niu.Center).NormalizeVector(Vector2.UnitX);

            // 两段光链 (避开中点命门)
            ACMShaders.DrawBeam(niu.Center, mid - dir * SafeGap, lethal ? 13f : 7f, core, edge, (lethal ? 1f : 0.65f) * form);
            ACMShaders.DrawBeam(mid + dir * SafeGap, ma.Center, lethal ? 13f : 7f, core, edge, (lethal ? 1f : 0.65f) * form);

            // 实体链节
            var sb = Main.spriteBatch;
            var t = TextureAssets.Chains[0].Value;
            float total = Vector2.Distance(niu.Center, ma.Center);
            float rot = dir.ToRotation() - MathHelper.PiOver2;
            float segLen = total * 0.5f - SafeGap;
            if (segLen > 20f) {
                var rec = new Rectangle(0, 0, t.Width, (int)segLen);
                sb.Draw(t, niu.Center - Main.screenPosition, rec, Color.DarkGray * form, rot, new Vector2(t.Width * 0.5f, 0), 1f, SpriteEffects.None, 0);
                sb.Draw(t, (mid + dir * SafeGap) - Main.screenPosition, rec, Color.DarkGray * form, rot, new Vector2(t.Width * 0.5f, 0), 1f, SpriteEffects.None, 0);
            }

            // 中点命门标识: 翠玉光晕 (安全色)
            var glow = ACMAsset.SoftGlow;
            var safeCol = TelegraphColors.Safe with { A = 0 };
            float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f);
            sb.Draw(glow, mid - Main.screenPosition, null, safeCol * (0.55f * form * pulse), 0, glow.Size() * 0.5f, SafeGap / 26f, default, 0);
            return false;
        }
    }

    /// <summary>
    /// 忘川水域: 马面炮台岗布下的驻留领域 (不随马面移动)。域内 25% 减速 + 周期魂蚀,
    /// 域顶降下三列可读魂火雨。半径包络: 展开 40f → 驻留 → 收拢。ai[1]=1 时为 P3 狂怒版。
    /// </summary>
    public class NiuMaTideField : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        private const float MaxRadius = 480f;
        private const int HoldEnd = 330;
        private const int KillAt = 365;
        private static readonly int[] ColumnTimes = [70, 160, 250];

        private readonly float[] columnX = new float[3];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = KillAt;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
        }
        public override bool? CanDamage() => false;

        private float Envelope {
            get {
                float t = Projectile.ai[0];
                if (t < 40f)
                    return ACMUtils.QuadOut(t / 40f);
                if (t > HoldEnd)
                    return MathHelper.Clamp(1f - (t - HoldEnd) / 30f, 0f, 1f);
                return 1f;
            }
        }

        public override void AI() {
            float t = ++Projectile.ai[0];
            bool rage = Projectile.ai[1] == 1f;
            float radius = MaxRadius * Envelope;

            if (t == 1f)
                SoundEngine.PlaySound(SoundID.Item84 with { Pitch = -0.6f, Volume = 0.8f }, Projectile.Center);

            // 域内减速 + 周期魂蚀 (服务器权威; buff 会自动同步)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                foreach (var p in Main.ActivePlayers) {
                    if (p.dead || p.Distance(Projectile.Center) > radius)
                        continue;
                    p.AddBuff(rage ? ModContent.BuffType<DeclineSpeedBuff_2>() : ModContent.BuffType<DeclineSpeedBuff_1>(), 6);
                    if (t % 45 == 0)
                        UnderworldField.AddSoulErosion(p, 1);
                }
            }

            // 三列魂火雨: 列位在触发帧取玩家 X (锁定, 不追身)
            for (int i = 0; i < 3; i++) {
                if ((int)t == ColumnTimes[i]) {
                    Player tgt = Main.player[Projectile.owner];
                    columnX[i] = MathHelper.Clamp(tgt.Center.X, Projectile.Center.X - radius + 90f, Projectile.Center.X + radius - 90f);
                }
                int firstBolt = ColumnTimes[i] + 38;
                if (t >= firstBolt && t <= firstBolt + 72 && (t - firstBolt) % 24 == 0 && columnX[i] != 0) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        var p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(),
                            new Vector2(columnX[i], Projectile.Center.Y - radius), new Vector2(0, rage ? 8.5f : 7.2f),
                            ModContent.ProjectileType<DarkGreenProj>(), Projectile.damage, 1f, Projectile.owner);
                        p.ai[1] = 1f;
                        p.timeLeft = (int)(radius * 2f / (rage ? 8.5f : 7.2f)) + 20;
                        p.netUpdate = true;
                    }
                    if ((t - firstBolt) == 0)
                        SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.6f, Pitch = -0.3f }, new Vector2(columnX[i], Projectile.Center.Y));
                }
            }

            // 边界魂火粒子
            if (!Main.dedServ && radius > 60f) {
                for (int i = 0; i < 3; i++) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    var d = Dust.NewDustPerfect(Projectile.Center + a.ToRotationVector2() * radius, DustID.CorruptTorch);
                    d.noGravity = true;
                    d.velocity = a.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float t = Projectile.ai[0];
            float radius = MaxRadius * Envelope;
            float inten = Envelope * 0.55f;
            if (inten <= 0.03f)
                return false;

            // 水域地纹 (ArenaRunic 法阵模式, 幽紫鬼绿)
            Effect fx = ACMShaders.ArenaRunic;
            if (fx != null) {
                ACMShaders.WorldDecalParams(Projectile.Center, radius, out Vector2 uv, out float radiusFrac, out float aspect);
                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(inten);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(TelegraphColors.GhostGreen.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(12f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
            }

            // 列雨预告线: 触发后 38f 内幽紫渐强, 末 10f 转红
            for (int i = 0; i < 3; i++) {
                float since = t - ColumnTimes[i];
                if (columnX[i] == 0 || since < 0 || since > 110)
                    continue;
                bool lethal = since >= 28;
                Color c = lethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                float lineInt = lethal ? 0.75f : 0.35f * (since / 28f);
                Vector2 top = new(columnX[i], Projectile.Center.Y - radius);
                Vector2 bottom = new(columnX[i], Projectile.Center.Y + radius);
                ACMShaders.DrawBeam(top, bottom, lethal ? 6f : 3.5f, c, new Color(50, 25, 95), lineInt);
            }
            return false;
        }
    }

    /// <summary>
    /// 同伴复生反制圈 —— 复活演出期间在「阵亡者尸位」生成的鬼门法阵 (翠玉配色)。
    /// 玩家站入圈内 → 正在引魂的同伴暂时<b>可被伤害</b>; 离开 → 恢复无敌。
    /// 打断成功 (引魂者被击杀) 即双吏同亡 — 高风险高回报的反制窗口。
    /// ai[0] = 引魂同伴 whoAmI。逻辑服务器权威, 绘制本地。
    /// </summary>
    public class NiuMaRevivalCircle : ModProjectile
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;

        public const float WorldRadius = 190f;

        private NPC Channeler {
            get {
                int who = (int)Projectile.ai[0];
                return (who >= 0 && who < Main.maxNPCs) ? Main.npc[who] : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 250;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
        }

        private bool AnyPlayerInside() {
            foreach (var p in Main.ActivePlayers) {
                if (!p.dead && p.Distance(Projectile.Center) < WorldRadius)
                    return true;
            }
            return false;
        }

        public override void AI() {
            NPC ch = Channeler;
            if (ch == null || !ch.active) {
                Projectile.Kill();
                return;
            }
            // 引魂者已被打断进入死亡演出 → 圈立即消散 (不再干涉其无敌位)
            if (ch.ai[0] == NiuMaBoss.StDeath) {
                Projectile.Kill();
                return;
            }

            bool playerInside = AnyPlayerInside();
            if (Main.netMode != NetmodeID.MultiplayerClient)
                ch.dontTakeDamage = !playerInside; // 站圈内则引魂者可被打断

            if (!Main.dedServ) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + a.ToRotationVector2() * WorldRadius * Main.rand.NextFloat(0.7f, 1f);
                var d = Dust.NewDustPerfect(pos, playerInside ? DustID.GreenFairy : DustID.GreenTorch);
                d.noGravity = true;
                d.velocity = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 1.6f;
                d.scale = playerInside ? 1.5f : 1.1f;
                Lighting.AddLight(Projectile.Center, 0.1f, 0.4f, 0.22f);
            }
        }

        public override void OnKill(int timeLeft) {
            NPC ch = Channeler;
            // 死亡演出的无敌不在此解除 (由演出自持)
            if (ch != null && ch.active && ch.ai[0] != NiuMaBoss.StDeath && Main.netMode != NetmodeID.MultiplayerClient)
                ch.dontTakeDamage = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            bool playerInside = AnyPlayerInside();
            float life = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float intensity = (playerInside ? 0.95f : 0.6f) * life;

            // 鬼门法阵 (翠玉安全配色): 站入者点亮法印
            Effect fx = NiuMaHelper.NetherGate;
            if (fx != null) {
                NiuMaHelper.SetGateParams(fx, Projectile.Center, WorldRadius,
                    TelegraphColors.Safe, TelegraphColors.GhostGreen,
                    playerInside ? 0.75f : 0.25f, 0f, intensity);
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
            }

            // 引魂链: 尸位 → 引魂者 (读障: 打谁一目了然)
            NPC ch = Channeler;
            if (ch != null && ch.active) {
                Color c = playerInside ? TelegraphColors.Safe : TelegraphColors.GhostGreen;
                ACMShaders.DrawBeam(Projectile.Center, ch.Center, 7f, c, new Color(40, 90, 60), playerInside ? 0.9f : 0.5f, 1.8f);
            }
            return false;
        }
    }

    /// <summary>勾魂标记 (马面控场机制): 纯可视/计时载体, 实际逻辑在 <see cref="NiuMaPlayer"/>。</summary>
    public class SoulHookBuff : ModBuff
    {
        public override string Texture => NiuMaHelper.NothingTex_Path;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }
}
