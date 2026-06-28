using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 祖龙残魂(地表) V2 弹幕共享工具。地面探测 / 段链枚举。
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
    /// 残雷弹 — 蓄力齐射 (ThunderVolley) 的主题雷弹。替代旧版 RNG <c>ThrowThunderballs</c>:
    /// 头部聚能 1.5s 后沿**可读扇形**直线射出 (无随机偏转 / 无自残 / 无令人困惑的"可被打碎"判定)。
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
    /// 尾雷 (TailLightning) — 由龙身**段节**释放的纵向落雷。把蠕虫身体变成机制载体:
    /// 锚定某节身体, 锁定其下落点, 渐强**段节闪光 + 暗色导引线**预告, 然后转为青白致命雷柱 + 落点泛光。
    /// ai[0] = 锚定段节 whoAmI; ai[1] = 预告 tick。
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
            // 锚定段节 (用于绘制段节闪光); 段节消失则继续用锁定坐标
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
                // 导引线: 大部分时间青白(无害), 末段 0.3s 转红致命可读
                bool imminent = p > 0.78f;
                Color core = imminent ? TelegraphColors.Lethal : TelegraphColors.Lightning;
                ACMShaders.DrawBeam(top, bottom, MathHelper.Lerp(2f, 6f, p), core, core * 0.3f,
                    MathHelper.Lerp(0.2f, 0.85f, p), flowSpeed: 2.4f, flowScale: 3f);
            }
            else {
                float sp = MathHelper.Clamp((Age - Telegraph) / (float)StrikeTicks, 0f, 1f);
                float fade = 1f - sp;
                Color core = new(215, 240, 255);
                Color edge = TelegraphColors.Lightning * 0.7f;
                ACMShaders.DrawBeam(top, bottom, 9f + 22f * fade, core, edge, 0.6f + 0.4f * fade,
                    flowSpeed: 3.4f, flowScale: 2f, coreGlow: 1.5f);
                if (sp < 0.4f)
                    ACMShaders.DrawRadialBloomAt(bottom, 0.1f, fade * 0.8f, TelegraphColors.Lightning, rayCount: 8f);
            }
            return false;
        }
    }

    /// <summary>
    /// 雷巢 (ThunderNest) 静态雷球 — 三角阵纯视觉锚点 (链电由 <see cref="ArchosaurNestLink"/> 承载)。
    /// </summary>
    public class ArchosaurNestOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/ThunderCloud";

        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ArchosaurNestLink.TelegraphTicks + 60;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Age++;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, 0.45f, 0.6f, 0.95f);
            if (!Main.dedServ && Age % 3 == 0) {
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
            Main.spriteBatch.Draw(glow, pos, null, new Color(120, 190, 255, 0) * 0.9f,
                0f, glow.Size() * 0.5f, 0.85f * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, pos, null, Color.White with { A = 0 } * 0.8f,
                0f, glow.Size() * 0.5f, 0.4f * pulse, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 雷巢链电 (ThunderNest link) — 连接两颗静态雷球, 蓄力 3s 后若**中点节点未被打碎**则在两端间放出致命链电。
    /// 中点节点可被玩家弹幕击碎 (沿用 <see cref="ThunderOrb"/> 的扫描机制) → 该条链失效(安全)。
    /// 端点: <c>Center ± half</c>, half 由生成速度传入 (首帧固化进 ai[0]/ai[1])。
    /// </summary>
    public class ArchosaurNestLink : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public const int TelegraphTicks = 180; // 3s
        private const int StrikeTicks = 24;
        private const float NodeHealth = 1600f;

        private ref float Age => ref Projectile.localAI[0];
        private ref float NodeHP => ref Projectile.localAI[1];
        private bool Broken => Projectile.ai[2] >= 1f;
        private Vector2 Half => new(Projectile.ai[0], Projectile.ai[1]); // 半向量 (生成时经 ai 传入)
        private bool init;
        private bool brokenFxDone;
        private Vector2 mid;                 // 固定中点 (首帧锁定, 改 hitbox 不影响)
        private Vector2 EndA => mid - Half;
        private Vector2 EndB => mid + Half;
        private bool Striking => !Broken && Age >= TelegraphTicks;

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

            // 中点节点: 蓄力期可被玩家弹幕打碎 (服务器权威, Broken 经 ai[2] 同步)
            if (!Broken && !Striking && Main.netMode != NetmodeID.MultiplayerClient) {
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

            if (Age == TelegraphTicks && !Broken) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f }, mid);
                ACMUtils.AddScreenShake(5f);
            }

            if (!Main.dedServ) {
                Lighting.AddLight(Projectile.Center, Broken ? new Vector3(0.1f, 0.1f, 0.1f) : new Vector3(0.3f, 0.5f, 0.8f));
            }

            Age++;
            if (Broken && Age >= TelegraphTicks + 10)
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
                Color core = new(220, 245, 255);
                ACMShaders.DrawBeam(a, b, 7f + 16f * fade, core, TelegraphColors.Lightning * 0.7f,
                    0.6f + 0.4f * fade, flowSpeed: 4f, flowScale: 2f, coreGlow: 1.6f);
            }
            return false;
        }
    }

    /// <summary>
    /// 逆向雷 (ReverseLightning) — P2 替身被破时触发: 外环生成多道雷, 预告后**向内汇聚**(向心) → 玩家应向外躲。
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
}
