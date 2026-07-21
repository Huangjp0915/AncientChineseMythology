using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Jiangcens
{
    // ============================================================
    //  将臣 V3 弹幕组（自 Jiangcen.cs 拆出并重做）
    //  预警语言契约: 红=致命伤害源, 尸暗红=尸气氛围, 雷青=雷电/非致命, 金=将令
    // ============================================================

    // ===== 预告标记：落点 / 尸坟 / 锚点 / 边界 / 安全缝（无伤害纯视觉）=====
    // ai[0]=样式(0落点环,1尸坟,2锚点,3边界,4安全缝立柱), ai[1]=寿命
    internal class JiangcenTelegraphMark : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 80;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0 && Projectile.ai[1] > 0) {
                Projectile.timeLeft = (int)Projectile.ai[1];
            }
            Projectile.localAI[0]++;
            if (!VaultUtils.isServer && Projectile.localAI[0] % 4 == 0) {
                int style = (int)Projectile.ai[0];
                if (style == 4) {
                    //安全缝: 翠色上升微光(邀请玩家进入, 而非警告)
                    int ds = Dust.NewDust(Projectile.Center + new Vector2(Main.rand.NextFloat(-40, 40), Main.rand.NextFloat(-300, 300)),
                        0, 0, DustID.GreenFairy, 0, -1.5f, 150, default, 1.1f);
                    Main.dust[ds].noGravity = true;
                    return;
                }
                bool warm = style == 0 || style == 1; //落点/尸坟=暖红粒子, 锚点/边界=雷青粒子
                int dustType = warm ? DustID.Shadowflame : DustID.Electric;
                Color col = warm ? Color.DarkRed : default;
                float r = style == 1 ? 18f : 34f;
                Vector2 off = Main.rand.NextVector2CircularEdge(r, r);
                int d = Dust.NewDust(Projectile.Center + off, 0, 0, dustType, 0, 0, 120, col, 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = off.SafeNormalize(Vector2.Zero) * 0.6f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int style = (int)Projectile.ai[0];
            float life = Math.Max(1f, Projectile.ai[1]);
            float prog = MathHelper.Clamp(Projectile.localAI[0] / life, 0, 1);
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Projectile.localAI[0] * 0.25f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //安全缝立柱: 翠色纵向软光带(万雷点将的"活路"标记)
            if (style == 4) {
                Color safe = TelegraphColors.Safe with { A = 0 };
                float sw = 90f / tex.Width;
                float sh = 1500f / tex.Height;
                Main.spriteBatch.Draw(tex, pos, null, safe * (0.22f + 0.12f * pulse), 0f,
                    tex.Size() / 2, new Vector2(sw, sh), SpriteEffects.None, 0);
                Main.spriteBatch.Draw(tex, pos, null, safe * (0.5f + 0.3f * pulse), 0f,
                    tex.Size() / 2, new Vector2(sw * 0.4f, sh), SpriteEffects.None, 0);
                return false;
            }

            //统一预警配色: 落点=致命红 / 尸坟=暖暗红 / 锚点=雷青 / 边界=低饱和脉动
            Color baseCol = style switch {
                0 => TelegraphColors.Lethal,
                1 => JiangcenVFX.CorpseRed,
                2 => TelegraphColors.Lightning,
                3 => new Color(110, 150, 200),
                _ => TelegraphColors.Lightning
            };
            baseCol.A = 0;

            //环形收束的预告圈
            float ringScale = MathHelper.Lerp(2.6f, 0.7f, prog) * (style == 3 ? 0.6f : 1f);
            Main.spriteBatch.Draw(tex, pos, null,
                baseCol * (0.5f + 0.5f * prog) * pulse, 0f, tex.Size() / 2, ringScale, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(tex, pos, null,
                baseCol * 0.5f, 0f, tex.Size() / 2, 1.1f * pulse, SpriteEffects.None, 0);

            //锁定式内核: 落点/锚点临近命中时收束变亮(可读的"就是这里")
            if (style == 0 || style == 2) {
                float lockT = prog * prog;
                Main.spriteBatch.Draw(tex, pos, null,
                    baseCol * (0.3f + 0.7f * lockT), 0f, tex.Size() / 2,
                    MathHelper.Lerp(0.9f, 0.35f, prog) * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    // ===== 落地震波弹：雷主题径向电弹（首 18 帧速度渐升 → 反 telefrag 阀门）=====
    // ai[0]=基础速度(px/f, 生成时刻的 velocity 长度会被覆写)
    internal class JiangcenShockBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 90;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];

            if (Projectile.ai[0] <= 0f) {
                Projectile.ai[0] = Math.Max(4f, Projectile.velocity.Length());
            }
            //首 18 帧 60%→100% 渐升(招式衔接的公平缓冲), 之后缓慢衰减
            float ramp = MathHelper.Lerp(0.6f, 1f, MathHelper.Clamp(t / 18f, 0f, 1f));
            float decay = t > 18f ? (float)Math.Pow(0.988, t - 18f) : 1f;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * Projectile.ai[0] * ramp * decay;
            Projectile.rotation += 0.3f;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 120, default, 1.2f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = ACMAsset.SoftGlow;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 op = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float f = 1f - i / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(tex, op, null, new Color(120, 180, 255, 0) * f * 0.5f, Projectile.rotation, tex.Size() / 2, Projectile.scale * f, SpriteEffects.None, 0);
            }
            if (glow != null) {
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    TelegraphColors.Lightning with { A = 0 } * 0.55f, 0f, glow.Size() / 2, 0.9f, SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, tex.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // ===== 尸手：从尸坟向上抓起的垂直命中柱（波浪时序: ai[0]=启动延迟）=====
    internal class JiangcenCorpseHand : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SlashBurst";

        private const int WarnTime = 26;
        private const int RiseTime = 26;
        private const int ActiveTime = 44;
        private float ColumnHeight => Projectile.ai[2] > 0 ? Projectile.ai[2] : 320f;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 320;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            //锚定于坟口，命中柱自下而上; ai[1]=坟口 Y(首帧记录)
            if (Projectile.ai[1] == 0) Projectile.ai[1] = Projectile.Center.Y;
            Projectile.height = (int)ColumnHeight;
            Projectile.Center = new Vector2(Projectile.Center.X, Projectile.ai[1] - ColumnHeight / 2f);

            //波浪时序: 延迟未到时只冒坟前尘
            if (Projectile.ai[0] > 0) {
                Projectile.ai[0]--;
                if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                    int ds = Dust.NewDust(new Vector2(Projectile.Center.X - 20, Projectile.ai[1] - 8), 40, 8, DustID.Shadowflame, 0, -0.6f, 140, Color.DarkRed, 1.1f);
                    Main.dust[ds].noGravity = true;
                }
                Projectile.timeLeft = WarnTime + RiseTime + ActiveTime + 10;
                return;
            }

            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == WarnTime) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = 0.2f }, Projectile.Center);
            }
            if (!VaultUtils.isServer) {
                if (Projectile.localAI[0] < WarnTime) {
                    int d = Dust.NewDust(new Vector2(Projectile.Center.X - 20, Projectile.ai[1] - 8), 40, 8, DustID.Shadowflame, 0, -1f, 120, Color.DarkRed, 1.5f);
                    Main.dust[d].noGravity = true;
                }
                else if (Main.rand.NextBool(2)) {
                    int d = Dust.NewDust(new Vector2(Projectile.Center.X - 24, Projectile.Center.Y), 48, (int)ColumnHeight, DustID.Shadowflame, 0, -3f, 120, Color.DarkRed, 1.8f);
                    Main.dust[d].noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target) {
            return Projectile.ai[0] <= 0 && Projectile.localAI[0] >= WarnTime
                && Projectile.localAI[0] < WarnTime + RiseTime + ActiveTime;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.ai[0] > 0)
                return false;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float t = Projectile.localAI[0];
            float rise = MathHelper.Clamp((t - WarnTime) / RiseTime, 0f, 1f);
            //抓出用 elastic 意味的过冲: 先冲到 1.08 再回落
            float riseCurve = rise < 1f ? MathF.Sin(rise * MathHelper.PiOver2) * (1f + 0.08f * MathF.Sin(rise * MathHelper.Pi)) : 1f;
            float warnAlpha = MathHelper.Clamp(t / WarnTime, 0f, 1f);
            //收招淡出
            float fade = MathHelper.Clamp((WarnTime + RiseTime + ActiveTime + 8 - t) / 8f, 0f, 1f);
            Vector2 bottom = new Vector2(Projectile.Center.X, Projectile.ai[1]) - Main.screenPosition;
            float scaleY = (ColumnHeight / tex.Height) * (t < WarnTime ? 0.25f : riseCurve);
            float scaleX = 64f / tex.Width;
            Color col = (t < WarnTime ? new Color(120, 10, 10) * warnAlpha * 0.5f : new Color(200, 30, 30) * fade);
            col.A = 0;
            Main.spriteBatch.Draw(tex, bottom, null, col, 0f, new Vector2(tex.Width / 2f, tex.Height), new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
            //爪芯: 更窄更亮的一条
            Main.spriteBatch.Draw(tex, bottom, null, col * 0.9f, 0f, new Vector2(tex.Width / 2f, tex.Height), new Vector2(scaleX * 0.45f, scaleY), SpriteEffects.None, 0);
            return false;
        }
    }

    // ===== 雷锤回旋投掷：飞出再回返，需躲两段 =====
    // ai[0]=Boss whoAmI, ai[1]=是否附电(P2 视觉)
    internal class JiangcenThrownHammer : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";

        private const int OutTime = 42;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 70;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.localAI[1]++;
            //转速随速度(重量: 快时狂转, 慢时滞转)
            Projectile.rotation += 0.18f + Projectile.velocity.Length() * 0.016f;

            if (Projectile.localAI[1] < OutTime) {
                Projectile.velocity *= 0.958f; //飞出减速(到顶点几乎悬停一拍)
            }
            else {
                //回返至 Boss, 越接近越快(收回的"猛")
                NPC boss = Main.npc[(int)Projectile.ai[0]];
                Vector2 dest = boss.Alives() ? boss.Center : Projectile.Center;
                Vector2 dir = (dest - Projectile.Center).SafeNormalize(Vector2.Zero);
                float returnT = MathHelper.Clamp((Projectile.localAI[1] - OutTime) / 40f, 0f, 1f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * (16f + 10f * returnT), 0.07f);
                if (boss.Alives() && Vector2.Distance(Projectile.Center, dest) < 74f) {
                    SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.2f }, Projectile.Center);
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric, 0, 0, 100, default, 1.5f);
                            Main.dust[d].noGravity = true;
                            Main.dust[d].velocity = Main.rand.NextVector2Circular(4, 4);
                        }
                    }
                    Projectile.Kill();
                }
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 120, default, 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.4f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float spd = Projectile.velocity.Length();
            //速度门控残影
            if (spd > 8f) {
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    Vector2 op = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                    float f = 1f - i / (float)Projectile.oldPos.Length;
                    Main.spriteBatch.Draw(tex, op, null, new Color(120, 170, 255, 0) * f * 0.4f, Projectile.rotation, tex.Size() / 2, Projectile.scale * f, SpriteEffects.None, 0);
                }
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, tex.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            //P2 附电
            if (Projectile.ai[1] > 0 && MythologyConfig.Trail != TrailQualityLevel.Off) {
                JiangcenVFX.DrawBodyArcs(Main.spriteBatch, Projectile.Center, 44f, 0.7f, Projectile.whoAmI);
            }
            return false;
        }
    }

    // ===== 镜像锤魂：镜像玩家走位后突袭 =====
    // ai[0]=类型(0点对称,1水平镜像), ai[1]=Boss whoAmI
    internal class JiangcenHammerGhost : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";

        private const int MirrorTime = 110;
        private const int StrikeTime = 46;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 70;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = MirrorTime + StrikeTime + 40;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            NPC boss = Main.npc[(int)Projectile.ai[1]];
            if (!boss.Alives() || boss.ModNPC is not Jiangcen jc) {
                Projectile.Kill();
                return;
            }
            Player target = Main.player[boss.target];
            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];
            Projectile.rotation += 0.2f;

            if (t < MirrorTime) {
                //镜像玩家相对场地中心的位置
                Vector2 mirror;
                if ((int)Projectile.ai[0] == 0) {
                    mirror = jc.ArenaCenter * 2f - target.Center; //点对称
                }
                else {
                    mirror = new Vector2(jc.ArenaCenter.X * 2f - target.Center.X, target.Center.Y); //水平镜像
                }
                Projectile.Center = Vector2.Lerp(Projectile.Center, mirror, 0.25f);
                Projectile.velocity = Vector2.Zero;
                if (!VaultUtils.isServer && t % 3 == 0) {
                    int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.Shadowflame, 0, 0, 120, Color.DarkRed, 1.4f);
                    Main.dust[d].noGravity = true;
                }
            }
            else if (t == MirrorTime) {
                Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * 27f;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 1.1f }, Projectile.Center);
            }
            else {
                Projectile.velocity *= 0.992f;
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0, 0, 120, Color.DarkRed, 1.6f);
                    Main.dust[d].noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target) {
            return Projectile.localAI[0] >= MirrorTime;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            bool striking = Projectile.localAI[0] >= MirrorTime;
            //与本体异色(紫红): 标明"这是你的影子", 强化与自己走位对抗的体验
            Color tint = striking ? new Color(225, 75, 205) : new Color(150, 60, 175) * 0.75f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 op = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float f = 1f - i / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(tex, op, null, tint * f * 0.4f, Projectile.rotation, tex.Size() / 2, Projectile.scale * f, SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, tint, Projectile.rotation, tex.Size() / 2, Projectile.scale, SpriteEffects.None, 0);

            //突袭起手的致命突进线(红=真正伤害源): 命中前可读"影子从哪扑来"
            float strikeT = Projectile.localAI[0] - MirrorTime;
            if (striking && strikeT < 16f && Projectile.velocity.LengthSquared() > 1f) {
                float fade = 1f - strikeT / 16f;
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dir * 520f, 6f + 6f * fade,
                    TelegraphColors.Lethal, new Color(150, 20, 90, 0), 0.85f * fade, 1.8f, 2.2f);
            }
            return false;
        }
    }

    // ===== 雷狱落雷柱：预警→静默→落雷, 伤害窗与视觉严格对齐 =====
    // ai[0]=启动延迟, ai[1]=模式(0标准46px / 1走廊110px / 2纯视觉雷矛无判定), ai[2]=柱高(0=默认1100)
    internal class JiangcenLightningStrike : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightningBranch";

        private const int WarnTime = 42;
        private const int SilentTime = 6;   //预警→落雷之间的静默一拍(充能语法)
        private const int ActiveTime = 22;

        private int Mode => (int)Projectile.ai[1];
        private float ColumnHeight => Projectile.ai[2] > 0 ? Projectile.ai[2] : 1100f;
        private float ColumnWidth => Mode == 1 ? 200f : 46f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 1100;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 600;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            //首帧按模式定尺寸(全端确定性)
            if (Projectile.localAI[1] == 0) {
                Projectile.localAI[1] = 1;
                Vector2 c = Projectile.Center;
                Projectile.width = (int)ColumnWidth;
                Projectile.height = (int)ColumnHeight;
                Projectile.Center = c;
            }

            if (Projectile.ai[0] > 0) {
                Projectile.ai[0]--;
                Projectile.timeLeft = WarnTime + SilentTime + ActiveTime + 8;
                return;
            }

            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];
            if (t == WarnTime + SilentTime) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = Mode == 1 ? 1.05f : 1.2f }, Projectile.Center);
                if (Mode != 2) {
                    ACMScreenShakeSystem.Add(Mode == 1 ? 5.5f : 7f);
                    JiangcenThunderPrisonSystem.Pulse(Projectile.Center, 0.5f, TelegraphColors.Lightning);
                }
                if (!VaultUtils.isServer) {
                    int n = Mode == 1 ? 14 : 22;
                    for (int i = 0; i < n; i++) {
                        int d = Dust.NewDust(new Vector2(Projectile.Center.X - ColumnWidth * 0.3f, Projectile.Center.Y - ColumnHeight / 2), (int)(ColumnWidth * 0.6f), (int)ColumnHeight, DustID.Electric, 0, 0, 100, default, 1.8f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = Main.rand.NextVector2Circular(2.4f, 2.4f);
                    }
                }
            }
        }

        public override bool CanHitPlayer(Player target) {
            if (Mode == 2)
                return false;
            return Projectile.ai[0] <= 0
                && Projectile.localAI[0] >= WarnTime + SilentTime
                && Projectile.localAI[0] < WarnTime + SilentTime + ActiveTime;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.ai[0] > 0)
                return false;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float t = Projectile.localAI[0];
            bool silent = t >= WarnTime && t < WarnTime + SilentTime;
            bool active = t >= WarnTime + SilentTime;
            float warnAlpha = MathHelper.Clamp(t / WarnTime, 0, 1);
            float flick = 0.6f + 0.4f * Main.rand.NextFloat();
            float scaleY = ColumnHeight / tex.Height;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (!active) {
                //预警: 渐亮细柱; 静默拍骤暗(inhale)
                float a = silent ? 0.12f : warnAlpha * 0.4f;
                Color wcol = new Color(80, 140, 255) * a;
                wcol.A = 0;
                float wx = (silent ? 0.22f : 0.35f) * ColumnWidth / tex.Width * (Mode == 1 ? 2.2f : 1f);
                Main.spriteBatch.Draw(tex, pos, null, wcol, 0f, new Vector2(tex.Width / 2f, tex.Height / 2f), new Vector2(wx, scaleY), SpriteEffects.None, 0);
                return false;
            }

            //落雷: 双层枝状 + 白热芯, 收招 8 帧淡出
            float fade = MathHelper.Clamp((WarnTime + SilentTime + ActiveTime + 8 - t) / 8f, 0f, 1f);
            Color col = new Color(180, 220, 255) * flick * fade;
            col.A = 0;
            float sx = ColumnWidth / tex.Width * (Mode == 1 ? 1.5f : 1f);
            Main.spriteBatch.Draw(tex, pos, null, col, 0f, new Vector2(tex.Width / 2f, tex.Height / 2f), new Vector2(sx, scaleY), SpriteEffects.None, 0);
            Main.spriteBatch.Draw(tex, pos, null, Color.White with { A = 0 } * 0.75f * flick * fade, 0f, new Vector2(tex.Width / 2f, tex.Height / 2f), new Vector2(sx * 0.4f, scaleY), SpriteEffects.None, 0);
            //走廊模式补第二根错位枝(更宽的"雷幕"感)
            if (Mode == 1) {
                Main.spriteBatch.Draw(tex, pos + new Vector2(ColumnWidth * 0.28f, 0), null, col * 0.6f, 0f, new Vector2(tex.Width / 2f, tex.Height / 2f), new Vector2(sx * 0.7f, scaleY), SpriteEffects.FlipHorizontally, 0);
            }
            return false;
        }
    }

    // ===== 雷狱链式闪电：锚点之间的线段命中（自带预告+错拍延迟）=====
    // 生成：position=中点, velocity=(B-A)；首帧存半向量于 ai[0],ai[1]; ai[2]=启动延迟
    internal class JiangcenChainArc : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightningBranch";

        private const int WarnTime = 36;
        private const int ActiveTime = 26;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 600;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Projectile.localAI[1] == 0) {
                Projectile.localAI[1] = 1;
                Vector2 half = Projectile.velocity * 0.5f;
                Vector2 mid = Projectile.Center;
                Projectile.ai[0] = half.X;
                Projectile.ai[1] = half.Y;
                Projectile.velocity = Vector2.Zero;
                //扩张 AABB 作为宽相位包围盒（精确判定见 Colliding）
                Projectile.width = (int)Math.Max(40, Math.Abs(half.X) * 2 + 40);
                Projectile.height = (int)Math.Max(40, Math.Abs(half.Y) * 2 + 40);
                Projectile.Center = mid;
                Projectile.netUpdate = true;
            }

            if (Projectile.ai[2] > 0) {
                Projectile.ai[2]--;
                Projectile.timeLeft = WarnTime + ActiveTime + 8;
                return;
            }

            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == WarnTime) {
                SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.2f }, Projectile.Center);
            }
            if (!VaultUtils.isServer && Projectile.localAI[0] >= WarnTime && Main.rand.NextBool(2)) {
                Vector2 half = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                Vector2 p = Vector2.Lerp(Projectile.Center - half, Projectile.Center + half, Main.rand.NextFloat());
                int d = Dust.NewDust(p, 0, 0, DustID.Electric, 0, 0, 100, default, 1.6f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(2, 2);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.ai[2] > 0 || Projectile.localAI[0] < WarnTime || Projectile.localAI[0] >= WarnTime + ActiveTime)
                return false;
            Vector2 half = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            Vector2 a = Projectile.Center - half;
            Vector2 b = Projectile.Center + half;
            Vector2 c = targetHitbox.Center.ToVector2();
            return Jiangcen.DistanceToSegment(c, a, b) < 30f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.ai[2] > 0)
                return false;
            Vector2 half = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            Vector2 aWorld = Projectile.Center - half;
            Vector2 bWorld = Projectile.Center + half;
            float t = Projectile.localAI[0];
            bool active = t >= WarnTime;
            float flick = 0.6f + 0.4f * Main.rand.NextFloat();

            if (!active) {
                //预警: 雷青细弧渐亮(非致命色, 电网尚未通电)
                float warnAlpha = MathHelper.Clamp(t / WarnTime, 0, 1);
                JiangcenVFX.DrawArcStandalone(aWorld, bWorld, 10f,
                    TelegraphColors.Lightning with { A = 200 }, JiangcenVFX.ArcBlue with { A = 90 },
                    0.35f * warnAlpha, Projectile.whoAmI * 3.7f, 0.16f, 8f);
                return false;
            }

            //激活: 全功率电弧(专属着色器, 折线跳变+白热芯), 收招淡出
            float fade = MathHelper.Clamp((WarnTime + ActiveTime + 8 - t) / 8f, 0f, 1f);
            JiangcenVFX.DrawArcStandalone(aWorld, bWorld, 26f + 8f * flick,
                Color.White with { A = 235 }, TelegraphColors.Lightning with { A = 120 },
                (0.6f + 0.4f * flick) * fade, Projectile.whoAmI * 3.7f, 0.34f, 12f);
            return false;
        }
    }

    // ===== 将令雷印（V3 新增）：跟随→锁定→落雷的"点名"机制, 取代旧版冻结玩家 =====
    // ai[0]=模式(0跟随本人 / 1跟随水平镜像点), ai[1]=Boss whoAmI
    internal class JiangcenSealMark : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        private const int FollowTime = 36;
        private const int LockTime = 26;
        private const int SilentTime = 6;
        private const int StrikeTime = 22;
        private const float ColumnHeight = 900f;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = FollowTime + LockTime + SilentTime + StrikeTime + 12;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            NPC boss = Main.npc[(int)Projectile.ai[1]];
            if (!boss.Alives() || boss.ModNPC is not Jiangcen jc) {
                Projectile.Kill();
                return;
            }
            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];

            if (t <= FollowTime) {
                //跟随点名目标(或其镜像)的脚下
                Player target = Main.player[boss.target];
                Vector2 anchor = target.Bottom + new Vector2(0, -8);
                if ((int)Projectile.ai[0] == 1) {
                    anchor.X = jc.ArenaCenter.X * 2f - anchor.X;
                }
                Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.5f);
                if (t == 1) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.9f }, Projectile.Center);
                }
            }
            else if (t == FollowTime + 1) {
                //锁定瞬间: 滴答声 + 印面定格
                SoundEngine.PlaySound(SoundID.Item56 with { Pitch = 0.4f, Volume = 1f }, Projectile.Center);
                Projectile.netUpdate = true;
            }
            else if (t == FollowTime + LockTime + SilentTime) {
                //落雷
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 1.15f }, Projectile.Center);
                ACMScreenShakeSystem.Add(6f);
                JiangcenThunderPrisonSystem.Pulse(Projectile.Center, 0.45f, TelegraphColors.Lightning);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 18; i++) {
                        int d = Dust.NewDust(new Vector2(Projectile.Center.X - 14, Projectile.Center.Y - ColumnHeight), 28, (int)ColumnHeight, DustID.Electric, 0, 0, 100, default, 1.7f);
                        Main.dust[d].noGravity = true;
                    }
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float t = Projectile.localAI[0];
            if (t < FollowTime + LockTime + SilentTime || t >= FollowTime + LockTime + SilentTime + StrikeTime)
                return false;
            //落雷柱: 印位向上的竖直窄柱
            Rectangle column = new((int)(Projectile.Center.X - 30), (int)(Projectile.Center.Y - ColumnHeight), 60, (int)ColumnHeight + 16);
            return column.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = TextureAssets.Projectile[Type].Value;
            Texture2D branch = ACMAsset.LightningBranch;
            float t = Projectile.localAI[0];
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float pulse = 0.7f + 0.3f * (float)Math.Sin(t * 0.3f);

            if (t <= FollowTime) {
                //跟随期: 军金印圈(非致命色 — 尚可甩脱)
                Color gold = JiangcenVFX.GeneralGold with { A = 0 };
                float a = MathHelper.Clamp(t / 12f, 0f, 1f);
                Main.spriteBatch.Draw(glow, pos, null, gold * 0.5f * a * pulse, 0f, glow.Size() / 2, 1.5f, SpriteEffects.None, 0);
                Main.spriteBatch.Draw(glow, pos, null, gold * 0.8f * a, 0f, glow.Size() / 2, 0.7f * pulse, SpriteEffects.None, 0);
                return false;
            }

            float lockT = MathHelper.Clamp((t - FollowTime) / LockTime, 0f, 1f);
            bool silent = t >= FollowTime + LockTime && t < FollowTime + LockTime + SilentTime;
            bool striking = t >= FollowTime + LockTime + SilentTime;

            if (!striking) {
                //锁定期: 金→致命红收束 + 渐亮预警细柱(告知竖直落雷)
                Color c = Color.Lerp(JiangcenVFX.GeneralGold, TelegraphColors.Lethal, lockT) with { A = 0 };
                float shrink = MathHelper.Lerp(1.5f, 0.55f, lockT);
                float alpha = silent ? 0.25f : 0.55f + 0.35f * lockT;
                Main.spriteBatch.Draw(glow, pos, null, c * alpha * pulse, 0f, glow.Size() / 2, shrink, SpriteEffects.None, 0);
                if (branch != null) {
                    Color wc = TelegraphColors.Lethal with { A = 0 } * (silent ? 0.15f : 0.30f * lockT);
                    Main.spriteBatch.Draw(branch, pos, null, wc, 0f, new Vector2(branch.Width / 2f, branch.Height),
                        new Vector2((silent ? 8f : 14f) / branch.Width, ColumnHeight / branch.Height), SpriteEffects.None, 0);
                }
                return false;
            }

            //落雷: 白热枝状柱 + 印面爆闪
            float fade = MathHelper.Clamp((FollowTime + LockTime + SilentTime + StrikeTime + 10 - t) / 10f, 0f, 1f);
            float flick = 0.6f + 0.4f * Main.rand.NextFloat();
            if (branch != null) {
                Color col = new Color(190, 225, 255) * flick * fade;
                col.A = 0;
                Main.spriteBatch.Draw(branch, pos, null, col, 0f, new Vector2(branch.Width / 2f, branch.Height),
                    new Vector2(60f / branch.Width, ColumnHeight / branch.Height), SpriteEffects.None, 0);
                Main.spriteBatch.Draw(branch, pos, null, Color.White with { A = 0 } * 0.8f * flick * fade, 0f,
                    new Vector2(branch.Width / 2f, branch.Height),
                    new Vector2(24f / branch.Width, ColumnHeight / branch.Height), SpriteEffects.None, 0);
            }
            Main.spriteBatch.Draw(glow, pos, null, TelegraphColors.Lightning with { A = 0 } * flick * fade, 0f, glow.Size() / 2, 1.8f * fade, SpriteEffects.None, 0);
            return false;
        }
    }
}
