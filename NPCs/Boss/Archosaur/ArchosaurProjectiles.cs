using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 祖龙残魂(地表) V3 弹幕共享工具。地面探测。
    /// </summary>
    internal static class ArchosaurFX
    {
        /// <summary>从 <paramref name="from"/> 向下扫描第一块实体砖, 返回世界 Y; 找不到返回 from.Y + cap。</summary>
        public static float FindGroundY(Vector2 from, float cap = 1500f) {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int maxY = startY + (int)(cap / 16f);
            for (int y = startY; y < maxY; y++) {
                if (!WorldGen.InWorld(x, y, 5))
                    break;
                Tile t = Main.tile[x, y];
                if (t != null && t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                    return y * 16f;
            }
            return from.Y + cap;
        }
    }

    /// <summary>
    /// 残雷弹 — 蓄力齐射 (ThunderVolley) 的主题雷弹: 可读扇形直射, 无随机偏转 / 无自残。
    /// </summary>
    public class ArchosaurStormOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        public override void SetStaticDefaults() => Main.projFrames[Type] = 4;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.aiStyle = -1;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 260;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.velocity *= 1.012f;
            if (Projectile.velocity.Length() > 22f)
                Projectile.velocity = Vector2.Normalize(Projectile.velocity) * 22f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 4;
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.55f, 0.95f);
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Electric, 0f, 0f, 120, default, 1.1f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            int frameH = tex.Height / 4;
            Rectangle src = new(0, Projectile.frame * frameH, tex.Width, frameH);
            Vector2 origin = src.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Main.spriteBatch.Draw(glow, pos, null, new Color(120, 180, 255, 0) * 0.7f,
                    0f, glow.Size() * 0.5f, 0.5f, SpriteEffects.None, 0f);
            }
            Main.spriteBatch.Draw(tex, pos, src, Color.White, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => Projectile.Kill();

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f }, Projectile.Center);
            if (Main.dedServ)
                return;
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Electric, 0f, 0f, 80, default, 1.2f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 尾雷 (TailStorm / 雷渊螺旋 / 贯天次生柱共用) — 纵向落雷柱。
    /// 锚定段节或固定点, 暗色导引线渐强 (末段转红) → 程序化闪电雷柱贯落。
    /// ai[0] = 锚定段节 whoAmI (-1 = 以生成位置为顶端); ai[1] = 预告 tick。
    /// </summary>
    public class ArchosaurTailBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private const int StrikeTicks = 16;

        private int Anchor => (int)Projectile.ai[0];
        private int Telegraph => Math.Max(16, (int)Projectile.ai[1]);
        private ref float Age => ref Projectile.localAI[0];
        private ref float LockX => ref Projectile.localAI[1];
        private ref float GroundY => ref Projectile.localAI[2];
        private float TopY;

        private bool Striking => Age >= Telegraph;

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 锚定段节 (用于绘制段节闪光); 段节消失或 Anchor=-1 则用锁定坐标
            Vector2 anchorPos;
            if (Anchor >= 0 && Anchor < Main.maxNPCs && Main.npc[Anchor].active)
                anchorPos = Main.npc[Anchor].Center;
            else
                anchorPos = new Vector2(LockX == 0 ? Projectile.Center.X : LockX, Projectile.Center.Y);

            if (LockX == 0f) {
                LockX = anchorPos.X;
                TopY = anchorPos.Y;
                GroundY = ArchosaurFX.FindGroundY(new Vector2(LockX, anchorPos.Y));
            }
            else if (TopY == 0f) {
                TopY = anchorPos.Y; // 重建瞬态 TopY (非同步字段)
            }

            float topY = Math.Min(anchorPos.Y, TopY == 0 ? anchorPos.Y : TopY);
            float botY = GroundY;
            Projectile.Center = new Vector2(LockX, (topY + botY) * 0.5f);
            Projectile.height = Math.Max(16, (int)(botY - topY));
            Projectile.hostile = Striking;

            if (Age == Telegraph) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.6f, Pitch = Main.rand.NextFloat(-0.1f, 0.2f) }, Projectile.Center);
                ACMUtils.AddScreenShake(4.5f);
                if (!Main.dedServ) {
                    // 落点扬尘 (触地因果)
                    for (int i = 0; i < 9; i++) {
                        Dust d = Dust.NewDustPerfect(new Vector2(LockX + Main.rand.NextFloat(-14f, 14f), botY - 4f),
                            DustID.Electric, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -1f)), 60, default, 1.4f);
                        d.noGravity = true;
                    }
                }
            }

            if (!Main.dedServ) {
                Lighting.AddLight(new Vector2(LockX, botY), Striking ? new Vector3(0.4f, 0.7f, 0.95f) : new Vector3(0.2f, 0.3f, 0.45f));
                if (Striking && Age % 2 == 0) {
                    Dust d = Dust.NewDustDirect(new Vector2(LockX - 3, topY + Main.rand.NextFloat(botY - topY)), 6, 6,
                        DustID.Electric, Main.rand.NextFloat(-2, 2), 0, 60, default, 1.3f);
                    d.noGravity = true;
                }
            }

            Age++;
            if (Age >= Telegraph + StrikeTicks)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float topY = TopY == 0 ? Projectile.Center.Y - Projectile.height * 0.5f : TopY;
            Vector2 top = new(LockX, topY);
            Vector2 bottom = new(LockX, GroundY);
            float seed = (Projectile.whoAmI * 0.0731f) % 1f;

            if (!Striking) {
                float p = MathHelper.Clamp(Age / Telegraph, 0f, 1f);
                // 段节闪光 (锚点处, 渐强)
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null && Anchor >= 0 && Anchor < Main.maxNPCs && Main.npc[Anchor].active) {
                    Vector2 ap = Main.npc[Anchor].Center - Main.screenPosition;
                    Color flash = Color.Lerp(TelegraphColors.Lightning, Color.White, p) with { A = 0 };
                    Main.spriteBatch.Draw(glow, ap, null, flash * (0.4f + 0.6f * p),
                        0f, glow.Size() * 0.5f, 0.4f + 0.7f * p, SpriteEffects.None, 0f);
                }
                // 导引线: 大部分时间青白(无害), 末 ~0.3s 转红致命可读
                bool imminent = p > 0.55f;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                ACMShaders.DrawBeam(top, bottom, MathHelper.Lerp(2f, 6f, p), core, core * 0.3f,
                    MathHelper.Lerp(0.2f, 0.85f, p), flowSpeed: 2.4f, flowScale: 3f);
            }
            else {
                float sp = MathHelper.Clamp((Age - Telegraph) / (float)StrikeTicks, 0f, 1f);
                float fade = 1f - sp;
                // 程序化闪电主柱 + 残余细芯
                ArchosaurVFX.DrawLightningStrip(top, bottom, 26f + 34f * fade,
                    ArchosaurVFX.BoltCore, TelegraphColors.Lightning, 0.55f + 0.45f * fade, seed,
                    jagAmp: 0.5f, flicker: 0.35f);
                if (sp < 0.4f)
                    ACMShaders.DrawRadialBloomAt(bottom, 0.1f, fade * 0.8f, TelegraphColors.Lightning, rayCount: 8f);
            }
            return false;
        }
    }

    /// <summary>
    /// 雷巢 (ThunderNest) 雷球 — 从龙口吐出、弧线飞往阵位的视觉锚点 (链电由 <see cref="ArchosaurNestLink"/> 承载)。
    /// ai[0]/ai[1] = 阵位世界坐标; ai[2] = 0 真球 / 1 幻影假球 (灰蓝, 雷击时刻化烟消散)。
    /// </summary>
    public class ArchosaurNestOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderCloud";

        /// <summary>飞行入位时长。</summary>
        public const int FlightTicks = 26;

        private ref float Age => ref Projectile.localAI[0];
        private Vector2 Slot => new(Projectile.ai[0], Projectile.ai[1]);
        private bool Fake => Projectile.ai[2] >= 1f;
        private Vector2 spawnPos;
        private bool init;

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FlightTicks + ArchosaurNestLink.TelegraphTicks + 60;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (!init) {
                init = true;
                spawnPos = Projectile.Center;
            }
            Age++;
            Projectile.velocity = Vector2.Zero;

            if (Age <= FlightTicks) {
                // 弧线入位: SmoothStep 位移 + 垂直于路径的弧形鼓起
                float t = MathHelper.SmoothStep(0f, 1f, Age / (float)FlightTicks);
                Vector2 path = Slot - spawnPos;
                Vector2 norm = new Vector2(-path.Y, path.X).SafeNormalize(Vector2.UnitY);
                Projectile.Center = spawnPos + path * t + norm * MathF.Sin(t * MathHelper.Pi) * 70f;
            }
            else {
                Projectile.Center = Slot;
            }

            // 幻影假球: 雷击时刻化烟消散 (真球活到链电结束)
            if (Fake && Age >= FlightTicks + ArchosaurNestLink.TelegraphTicks) {
                if (!Main.dedServ) {
                    for (int i = 0; i < 12; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                            Main.rand.NextVector2Circular(2.5f, 2.5f), 140, ArchosaurVFX.PhantomBlue, 1.3f);
                        d.noGravity = true;
                    }
                }
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, Fake ? new Vector3(0.2f, 0.25f, 0.4f) : new Vector3(0.45f, 0.6f, 0.95f));
            if (!Main.dedServ && Age % 3 == 0 && !Fake) {
                Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(12), 24, 24,
                    DustID.Electric, 0f, 0f, 100, default, 1.1f);
                d.noGravity = true;
                d.velocity = (Projectile.Center - d.position) * 0.05f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return false;
            float pulse = 0.85f + 0.15f * MathF.Sin(Age * 0.18f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            if (!Fake) {
                Main.spriteBatch.Draw(glow, pos, null, new Color(120, 190, 255, 0) * 0.9f,
                    0f, glow.Size() * 0.5f, 0.85f * pulse, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(glow, pos, null, Color.White with { A = 0 } * 0.8f,
                    0f, glow.Size() * 0.5f, 0.4f * pulse, SpriteEffects.None, 0f);
            }
            else {
                // 幻影假球: 灰蓝半透 + 确定性闪烁, 无亮白芯 (读法: 无芯=假)
                float flicker = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.whoAmI * 1.7f);
                Main.spriteBatch.Draw(glow, pos, null, (ArchosaurVFX.PhantomBlue with { A = 0 }) * (0.55f * flicker),
                    0f, glow.Size() * 0.5f, 0.8f * pulse, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 雷巢链电 — 连接两颗雷球, 蓄力后若<b>中点节点未被打碎</b>则放出致命链电。
    /// 端点: <c>Center ± half</c> (半向量经 ai[0]/ai[1] 传入并首帧固化)。
    /// ai[2] 模式: 0=真链(可破节点, 会转红, 会雷击) / 1=已破(安全) / 2=幻影假链(灰蓝, 永不转红, 永不伤害)。
    /// 读法语言: 有亮白可破节点的 = 真; 灰蓝无节点的 = 假。
    /// </summary>
    public class ArchosaurNestLink : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public const int TelegraphTicks = 180; // 3s
        private const int StrikeTicks = 24;
        private const float NodeHealth = 1600f;

        private ref float Age => ref Projectile.localAI[0];
        private ref float NodeHP => ref Projectile.localAI[1];
        private bool Broken => (int)Projectile.ai[2] == 1;
        private bool Fake => (int)Projectile.ai[2] == 2;
        private Vector2 Half => new(Projectile.ai[0], Projectile.ai[1]); // 半向量 (生成时经 ai 传入)
        private bool init;
        private bool brokenFxDone;
        private Vector2 mid;                 // 固定中点 (首帧锁定, 改 hitbox 不影响)
        private Vector2 EndA => mid - Half;
        private Vector2 EndB => mid + Half;
        private bool Striking => (int)Projectile.ai[2] == 0 && Age >= TelegraphTicks;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphTicks + StrikeTicks + 30;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 首帧: 锁定中点 + 初始化节点血量 (半向量经 ai 传入, 无需从 velocity 推断)
            if (!init) {
                init = true;
                mid = Projectile.Center;
                Projectile.velocity = Vector2.Zero;
                NodeHP = NodeHealth;
            }

            // 致命链电期的包围盒(供 Colliding 宽相; 用固定 mid 计算端点, 改 hitbox 不移动端点)
            if (Striking) {
                Vector2 min = Vector2.Min(EndA, EndB);
                Vector2 max = Vector2.Max(EndA, EndB);
                Projectile.position = min - new Vector2(20);
                Projectile.width = (int)(max.X - min.X) + 40;
                Projectile.height = (int)(max.Y - min.Y) + 40;
                Projectile.hostile = true;
            }
            else {
                Projectile.hostile = false;
            }

            // 中点节点: 真链蓄力期可被玩家弹幕打碎 (服务器权威, 模式经 ai[2] 同步)
            if ((int)Projectile.ai[2] == 0 && !Striking && Main.netMode != NetmodeID.MultiplayerClient) {
                Rectangle node = new((int)mid.X - 16, (int)mid.Y - 16, 32, 32);
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || !p.friendly || p.damage <= 0)
                        continue;
                    if (p.Hitbox.Intersects(node)) {
                        NodeHP -= p.damage;
                        if (!Main.dedServ)
                            CombatText.NewText(node, CombatText.DamagedFriendly, p.damage, dramatic: true);
                        if (p.penetrate > 0) {
                            p.penetrate--;
                            if (p.penetrate == 0)
                                p.Kill();
                        }
                        if (NodeHP <= 0) {
                            Projectile.ai[2] = 1f; // Broken
                            Projectile.netUpdate = true;
                            break;
                        }
                    }
                }
            }

            // 击破特效 (各端在观测到 Broken 同步后各自播放一次)
            if (Broken && !brokenFxDone) {
                brokenFxDone = true;
                BreakFx();
            }

            if (Age == TelegraphTicks) {
                if (Striking) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f }, mid);
                    ACMUtils.AddScreenShake(5f);
                }
                else if (Fake && !Main.dedServ) {
                    // 假链在雷击时刻静默化烟 (无雷声 = 又一层读法回响)
                    for (int i = 0; i < 10; i++) {
                        Dust d = Dust.NewDustPerfect(Vector2.Lerp(EndA, EndB, Main.rand.NextFloat()),
                            DustID.Smoke, Main.rand.NextVector2Circular(1.5f, 1.5f), 150, ArchosaurVFX.PhantomBlue, 1.1f);
                        d.noGravity = true;
                    }
                }
            }

            if (!Main.dedServ)
                Lighting.AddLight(Projectile.Center, Broken ? new Vector3(0.1f, 0.1f, 0.1f) : new Vector3(0.3f, 0.5f, 0.8f));

            Age++;
            if ((Broken || Fake) && Age >= TelegraphTicks + 10)
                Projectile.Kill();
            if (Age >= TelegraphTicks + StrikeTicks)
                Projectile.Kill();
        }

        private void BreakFx() {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f }, mid);
            for (int i = 0; i < 16; i++) {
                Dust d = Dust.NewDustPerfect(mid + Main.rand.NextVector2Circular(8, 8), DustID.Electric, Main.rand.NextVector2Circular(4, 4), 60, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Striking)
                return false;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), EndA, EndB, 18f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 a = EndA;
            Vector2 b = EndB;

            if (Broken)
                return false;

            if (Fake) {
                // 幻影假链: 灰蓝细线, 永不转红, 无中点节点
                float p = MathHelper.Clamp(Age / (float)TelegraphTicks, 0f, 1f);
                float flicker = 0.6f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 2.3f);
                ACMShaders.DrawBeam(a, b, MathHelper.Lerp(1.2f, 3.4f, p),
                    ArchosaurVFX.PhantomBlue, ArchosaurVFX.PhantomBlue * 0.25f,
                    MathHelper.Lerp(0.14f, 0.5f, p) * flicker, flowSpeed: 1.6f, flowScale: 4f);
                return false;
            }

            if (!Striking) {
                float p = MathHelper.Clamp(Age / (float)TelegraphTicks, 0f, 1f);
                // 张力线: 青白(无害), 末 0.25s 转红致命
                bool imminent = p > 0.82f;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                ACMShaders.DrawBeam(a, b, MathHelper.Lerp(1.5f, 5f, p), core, core * 0.3f,
                    MathHelper.Lerp(0.18f, 0.8f, p), flowSpeed: 2f, flowScale: 3.5f);

                // 中点可破节点: 用青白 SoftGlow + 受损泛红提示"打我"
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    float hpFrac = MathHelper.Clamp(NodeHP / NodeHealth, 0f, 1f);
                    float pulse = 0.8f + 0.2f * MathF.Sin(Age * 0.3f);
                    Color nodeCol = Color.Lerp(TelegraphColors.Lethal, TelegraphColors.Lightning, hpFrac) with { A = 0 };
                    Vector2 c = mid - Main.screenPosition;
                    Main.spriteBatch.Draw(glow, c, null, nodeCol * 0.9f, 0f, glow.Size() * 0.5f, 0.6f * pulse, SpriteEffects.None, 0f);
                }
            }
            else {
                float sp = MathHelper.Clamp((Age - TelegraphTicks) / (float)StrikeTicks, 0f, 1f);
                float fade = 1f - sp;
                // 程序化闪电链
                ArchosaurVFX.DrawLightningStrip(a, b, 18f + 22f * fade,
                    ArchosaurVFX.BoltCore, TelegraphColors.Lightning, 0.6f + 0.4f * fade,
                    (Projectile.whoAmI * 0.113f) % 1f, jagAmp: 0.42f, flicker: 0.3f);
            }
            return false;
        }
    }

    /// <summary>
    /// 逆向雷 (ReverseLightning) — P2 幻影被破时触发: 外环生成多道雷, 预告后<b>向内汇聚</b>(向心) → 玩家应向外躲。
    /// ai[0]/ai[1] = 汇聚中心世界坐标; ai[2] = 预告 tick。
    /// </summary>
    public class ArchosaurReverseBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderOrb";

        public override void SetStaticDefaults() => Main.projFrames[Type] = 4;

        private Vector2 Center => new(Projectile.ai[0], Projectile.ai[1]);
        private int Telegraph => Math.Max(20, (int)Projectile.ai[2]);
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (Age < Telegraph) {
                Projectile.velocity *= 0.9f; // 预告期悬停
                Projectile.hostile = false;
            }
            else {
                Projectile.hostile = true;
                Vector2 toCenter = (Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toCenter * 17f, 0.14f);
                // 抵达中心则消散
                if (Vector2.Distance(Projectile.Center, Center) < 40f)
                    Projectile.Kill();
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 4;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.3f, 0.6f);
            Age++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            if (Age < Telegraph) {
                // 预告: 从当前位置指向中心的红色致命导引线 (告诉玩家"会向内冲")
                float p = MathHelper.Clamp(Age / (float)Telegraph, 0f, 1f);
                Vector2 dir = (Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Vector2 end = Projectile.Center + dir * MathHelper.Lerp(40f, 220f, p);
                ACMShaders.DrawBeam(Projectile.Center, end, MathHelper.Lerp(1.5f, 4.5f, p),
                    TelegraphColors.Lethal, TelegraphColors.Lethal * 0.3f, MathHelper.Lerp(0.25f, 0.85f, p),
                    flowSpeed: 2.5f, flowScale: 3f);
            }

            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            int frameH = tex.Height / 4;
            Rectangle src = new(0, Projectile.frame * frameH, tex.Width, frameH);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null)
                Main.spriteBatch.Draw(glow, pos, null, new Color(150, 120, 230, 0) * 0.7f,
                    0f, glow.Size() * 0.5f, 0.5f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pos, src, new Color(200, 180, 255), Projectile.rotation,
                src.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => Projectile.Kill();
    }

    /// <summary>
    /// 贯天雷柱标记 (HeavenPierce) — P3 处决级华彩的地面 X 标记。
    /// 滞后跟踪目标玩家 → 锁定 (变粗变亮, 不再移动) → 同帧亮出两侧次生雷柱预警线。
    /// 本体无伤害, 全部伤害由龙体贯下与次生 <see cref="ArchosaurTailBolt"/> 承载。
    /// ai[0] = 跟踪时长 tick; ai[1] = 目标玩家 whoAmI; ai[2] = 0 跟踪中 / 1 已锁定 (服务器权威)。
    /// </summary>
    public class ArchosaurPierceMark : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        /// <summary>次生雷柱相对锁定点的横向偏移。</summary>
        public const float SidePillarOffset = 260f;

        private ref float Age => ref Projectile.localAI[0];
        private ref float GroundYA => ref Projectile.localAI[1];
        private ref float GroundYB => ref Projectile.localAI[2];
        private bool Locked => Projectile.ai[2] >= 1f;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Age++;
            Projectile.velocity = Vector2.Zero;

            if (!Locked) {
                // 滞后跟踪 (跑离即甩脱); 各端跟踪, 锁定帧由服务器 netUpdate 钉住权威位置
                int t = (int)Projectile.ai[1];
                if (t >= 0 && t < Main.maxPlayers && Main.player[t].active && !Main.player[t].dead) {
                    Vector2 goal = Main.player[t].Center;
                    goal.Y = ArchosaurFX.FindGroundY(goal) - 8f;
                    Projectile.Center = Vector2.Lerp(Projectile.Center, goal, 0.075f);
                }
                if (Age >= Math.Max(10f, Projectile.ai[0]) && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.ai[2] = 1f;
                    Projectile.netUpdate = true;
                }
                GroundYA = GroundYB = 0f;
            }
            else if (GroundYA == 0f) {
                // 锁定帧: 固化两侧次生柱的地面高度 (纯视觉预警线用)
                GroundYA = ArchosaurFX.FindGroundY(Projectile.Center + new Vector2(-SidePillarOffset, -600f));
                GroundYB = ArchosaurFX.FindGroundY(Projectile.Center + new Vector2(SidePillarOffset, -600f));
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = -0.4f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float p = MathHelper.Clamp(Age / Math.Max(10f, Projectile.ai[0]), 0f, 1f);
            float baseIntensity = Locked ? 0.95f : MathHelper.Lerp(0.35f, 0.8f, p);
            float armLen = Locked ? 92f : 66f;
            float width = Locked ? 5f : 3f;
            float pulse = Locked ? (0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f)) : 1f;

            // X 标记 (两道斜杠)
            Vector2 c = Projectile.Center;
            for (int i = 0; i < 2; i++) {
                float rot = MathHelper.PiOver4 + i * MathHelper.PiOver2 + (Locked ? 0f : Age * 0.02f);
                Vector2 arm = rot.ToRotationVector2() * armLen;
                ACMShaders.DrawBeam(c - arm, c + arm, width, TelegraphColors.Lethal,
                    TelegraphColors.Lethal * 0.35f, baseIntensity * pulse, flowSpeed: 2f, flowScale: 2f);
            }

            // 锁定环
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Color ring = TelegraphColors.Lethal with { A = 0 };
                Main.spriteBatch.Draw(glow, c - Main.screenPosition, null, ring * (0.5f * baseIntensity * pulse),
                    0f, glow.Size() * 0.5f, Locked ? 1.5f : 1.0f, SpriteEffects.None, 0f);
            }

            // 锁定后: 两侧次生雷柱预警线 (提前 ~100f 画线, 落雷前公平可读)
            if (Locked && GroundYA != 0f) {
                for (int s = -1; s <= 1; s += 2) {
                    float x = c.X + s * SidePillarOffset;
                    float gy = s < 0 ? GroundYA : GroundYB;
                    Vector2 top = new(x, gy - 860f);
                    Vector2 bot = new(x, gy);
                    ACMShaders.DrawBeam(top, bot, 3.2f, TelegraphColors.Lethal, TelegraphColors.Lethal * 0.25f,
                        0.5f * pulse, flowSpeed: 2.8f, flowScale: 3f);
                }
            }
            return false;
        }
    }
}
