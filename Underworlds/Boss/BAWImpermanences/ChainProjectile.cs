using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑无常锁镰链 (V3: 真 Verlet 链节物理)。
    /// ai[0]=模式: 0 甩镰(通用直射回收) / 1 垂链帘 / 2 十字合拢锁 / 3 火签 / 4 地涌链柱。
    /// ai[1]=模式参数 (帘=相位索引, +100 为阴阳勾魂快降变体; 十字=方位角; 柱=预警帧数)。
    /// ai[2]=所属 NPC。伤害线段与预警严格对齐 (公平阀门)。
    /// </summary>
    public class ChainProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        private BAWVerletChain chain;
        private Vector2 anchor;
        private bool initialized;
        private float timer;          // 模式内部计时
        private float eruptHeight;    // mode4 当前柱高
        private float riseSpeed;      // mode4 上涌速度
        private bool stuck;           // mode3 已钉住
        private float fade = 1f;      // 收尾淡出

        private int Mode => (int)Projectile.ai[0];
        private float Param => Projectile.ai[1];
        private NPC Owner => Projectile.ai[2] >= 0 && Projectile.ai[2] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[2]] : null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                anchor = Projectile.Center;
                int nodes = Mode switch {
                    1 => 12,
                    2 => 14,
                    4 => 12,
                    _ => 9
                };
                chain = new BAWVerletChain(nodes, 26f, anchor);
                switch (Mode) {
                    case 2: Projectile.timeLeft = 132; break;
                    case 3:
                        Projectile.timeLeft = 220;
                        Projectile.tileCollide = true;
                        break;
                    case 4: Projectile.timeLeft = (int)Param + 108; break;
                    case 0: Projectile.timeLeft = 150; break;
                }
            }

            timer++;

            switch (Mode) {
                case 0: AI_ThrownSickle(); break;
                case 1: AI_Curtain(); break;
                case 2: AI_CrossLock(); break;
                case 3: AI_FireTally(); break;
                case 4: AI_Eruption(); break;
            }

            Lighting.AddLight(Projectile.Center, new Color(90, 70, 140).ToVector3() * 0.4f * fade);
        }

        /// <summary>mode0 甩镰: 直射 80f 后回收到主人。</summary>
        private void AI_ThrownSickle() {
            Projectile.rotation += 0.28f;
            NPC owner = Owner;
            Vector2 root = owner?.Center ?? anchor;

            if (timer > 80f && owner != null) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    (root - Projectile.Center).SafeNormalize(Vector2.Zero) * 22f, 0.08f);
                if (Projectile.Distance(root) < 60f)
                    Projectile.Kill();
            }
            else if (Projectile.velocity.Length() < 24f) {
                Projectile.velocity *= 1.03f;
            }

            chain.SegLen = MathF.Max(14f, Vector2.Distance(root, Projectile.Center) / (chain.Count - 1) * 1.06f);
            chain.Step(root, 4, Projectile.Center);
        }

        /// <summary>mode1 垂链帘: 锚点缓降 + 相位错开横摆; 阴阳变体自动避让安全缝。</summary>
        private void AI_Curtain() {
            bool fast = Param >= 100f;
            float phase = fast ? Param - 100f : Param;

            // 锚点: 缓降 + 正弦横摆 (相位错开 —— 帘幕永远有东西在动)
            anchor.Y += fast ? 1.15f : 0.55f;
            float sway = MathF.Sin(timer * 0.022f + phase * 1.1f) * 42f;
            Vector2 effAnchor = anchor + new Vector2(sway, 0f);

            bool retracting = Projectile.timeLeft < 34;
            chain.Gravity = retracting ? -0.5f : 0.5f;
            chain.Step(effAnchor);
            Projectile.Center = chain.Tail;
            Projectile.rotation = (chain.Pos[chain.Count - 1] - chain.Pos[chain.Count - 2]).ToRotation() - MathHelper.PiOver2;

            if (retracting)
                fade = Projectile.timeLeft / 34f;

            // 阴阳变体: 侵入安全缝前 150px 即熄灭 (缝 = 双使连线垂直平分线)
            if (fast && timer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC black = FindBoss(ModContent.NPCType<BlackImpermanence>());
                NPC white = FindBoss(ModContent.NPCType<WhiteImpermanence>());
                if (black != null && white != null) {
                    Vector2 mid = (black.Center + white.Center) * 0.5f;
                    Vector2 myNormal = (black.Center - white.Center).SafeNormalize(Vector2.UnitX);
                    if (Vector2.Dot(Projectile.Center - mid, myNormal) < 150f)
                        Projectile.Kill();
                }
            }
        }

        /// <summary>mode2 十字锁: 25f 显形红线 → 26px/f 合拢 → 定桎 40f → 消散。</summary>
        private void AI_CrossLock() {
            Vector2 dir = -Param.ToRotationVector2(); // 指向合拢中心
            Vector2 center = anchor + dir * 640f;

            if (timer <= 25f) {
                // 显形读条: 原地悬停
                Projectile.velocity = Vector2.Zero;
                Projectile.Center = anchor;
            }
            else {
                float dist = Vector2.Distance(Projectile.Center, center);
                if (dist > 52f) {
                    Projectile.velocity = dir * 26f;
                }
                else {
                    Projectile.velocity = Vector2.Zero; // 定桎
                }
            }

            if (Projectile.timeLeft < 18)
                fade = Projectile.timeLeft / 18f;

            Projectile.rotation = dir.ToRotation() + MathHelper.PiOver4;
            chain.SegLen = MathF.Max(12f, Vector2.Distance(anchor, Projectile.Center) / (chain.Count - 1) * 1.04f);
            chain.Step(anchor, 4, Projectile.Center);
        }

        /// <summary>mode3 火签: 抛物钉地 → 25f 落点预警 → 原地唤起地涌链柱。</summary>
        private void AI_FireTally() {
            if (!stuck) {
                Projectile.velocity.Y += 0.34f;
                Projectile.rotation += 0.32f * Projectile.direction;
                anchor = Projectile.Center;

                // 签体火光
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = -Projectile.velocity * 0.1f;
                }

                if (timer > 60f)
                    Stick();
            }
            else {
                Projectile.velocity = Vector2.Zero;

                // 落点红圈预警 (收缩)
                if (!Main.dedServ && timer % 3 == 0) {
                    float k = MathHelper.Clamp((timer - stuckTime) / 25f, 0f, 1f);
                    float r = MathHelper.Lerp(70f, 16f, k);
                    for (int i = 0; i < 3; i++) {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        var d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * r, DustID.Torch);
                        d.noGravity = true;
                        d.scale = 1.2f;
                        d.velocity = Vector2.Zero;
                    }
                }

                if (timer - stuckTime >= 25f) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        var p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center,
                            Vector2.Zero, ModContent.ProjectileType<ChainProjectile>(),
                            Projectile.damage, 0f, -1, 4f, 6f, Projectile.ai[2]);
                        p.netUpdate = true;
                    }
                    Projectile.Kill();
                }
            }
        }

        private float stuckTime;

        private void Stick() {
            if (stuck)
                return;
            stuck = true;
            stuckTime = timer;
            Projectile.tileCollide = false;
            Projectile.velocity = Vector2.Zero;
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.4f }, Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Mode == 3) {
                Stick();
                return false;
            }
            return true;
        }

        /// <summary>mode4 地涌链柱: Param 帧落点预警 → 链头上涌 (~420px) → 回收。</summary>
        private void AI_Eruption() {
            float delay = MathF.Max(0f, Param);
            Projectile.velocity = Vector2.Zero;

            if (timer <= delay) {
                // 落点预警
                Projectile.Center = anchor;
                if (!Main.dedServ && timer % 3 == 0) {
                    var d = Dust.NewDustPerfect(anchor + new Vector2(Main.rand.NextFloat(-26f, 26f), 6f), DustID.Torch);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = new Vector2(0f, -2f);
                }
                if (timer == delay && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.5f, Volume = 1f }, anchor);
                    for (int i = 0; i < 10; i++) {
                        var d = Dust.NewDustPerfect(anchor, DustID.Shadowflame);
                        d.noGravity = true;
                        d.scale = 1.8f;
                        d.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(4f, 10f));
                    }
                }
                riseSpeed = 18f;
            }
            else if (timer <= delay + 70f) {
                // 上涌: 首帧最快, 指数放缓 (0.96/f)
                eruptHeight = MathF.Min(440f, eruptHeight + riseSpeed);
                riseSpeed *= 0.96f;
            }
            else {
                // 回收
                eruptHeight = MathF.Max(0f, eruptHeight - 15f);
                fade = MathHelper.Clamp(eruptHeight / 200f, 0f, 1f);
                if (eruptHeight <= 1f)
                    Projectile.Kill();
            }

            Vector2 head = anchor - new Vector2(0f, eruptHeight);
            Projectile.Center = head;
            Projectile.rotation = -MathHelper.PiOver2 + MathHelper.PiOver4;
            chain.SegLen = MathF.Max(10f, eruptHeight / (chain.Count - 1) * 1.05f);
            chain.Step(anchor, 4, head);
        }

        #region 判定 (伤害窗口与预警严格对齐)

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            switch (Mode) {
                case 2: {
                    if (timer <= 25f || fade < 0.85f)
                        return false; // 显形读条与消散期无伤害
                    return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        anchor, Projectile.Center, 22f, ref _);
                }
                case 4: {
                    float delay = MathF.Max(0f, Param);
                    if (timer <= delay || eruptHeight < 24f || timer > delay + 74f)
                        return false; // 预警期与回收期无伤害
                    return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        anchor, anchor - new Vector2(0f, eruptHeight), 24f, ref _);
                }
                case 1:
                    return fade > 0.6f ? null : false; // 仅链头镰 (默认 AABB)
                default:
                    return null;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyChainBound(Mode == 2 ? 120 : 90);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.3f }, Projectile.Center);
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                }
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // mode2 显形红线预警 (与合拢路径一致)
            if (Mode == 2 && timer <= 25f) {
                float k = timer / 25f;
                Vector2 dir = -Param.ToRotationVector2();
                Color c = Color.Lerp(BAWFX.YinColor, TelegraphColors.Lethal, k);
                ACMShaders.DrawBeam(anchor, anchor + dir * 600f, 7f + k * 6f, c, c * 0.4f, 0.3f + k * 0.5f);
            }

            // 链体 (Verlet 节点)
            if (chain != null && (Mode != 4 || eruptHeight > 2f)) {
                Color chainCol = new Color(52, 48, 66) * fade;
                Color glowCol = BlackWhiteBlend(new Color(120, 90, 200));
                BAWHelper.DrawVerletChain(sb, chain, chainCol, glowCol * fade, 0.85f, fade);
            }

            // 链头镰刀
            bool drawSickle = Mode != 4 || eruptHeight > 2f;
            if (drawSickle) {
                BAWHelper.DrawSickleWithTrail(sb, Projectile.Center, Projectile.rotation,
                    Color.White * fade, Projectile.scale, Mode == 0 ? Projectile.oldPos : null, Projectile.oldRot);
            }

            return false;
        }

        /// <summary>孤使黑: 链光泛白 (黑白双色化)。</summary>
        private Color BlackWhiteBlend(Color baseCol) {
            if (Owner?.ModNPC is BAWImpermanenceBase b && b.Unleashed)
                return Color.Lerp(baseCol, Color.White, 0.5f);
            return baseCol;
        }

        #endregion

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.5f, Volume = 0.7f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 pos = chain != null
                    ? Vector2.Lerp(chain.Pos[0], chain.Tail, Main.rand.NextFloat())
                    : Projectile.Center;
                var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Main.rand.NextVector2Circular(5f, 5f);
            }
        }

        private static NPC FindBoss(int type) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n != null && n.active && n.type == type)
                    return n;
            }
            return null;
        }
    }

    /// <summary>
    /// 黑无常绞镰 (V3): B1 冲刺期随体旋转的 Verlet 链镰 ×2。
    /// 伤害仅在主人冲刺爆发帧 (速度 >16) 激活 —— 伤害窗口与残影视觉严格对齐。
    /// ai[0]=所属 NPC, ai[1]=相位。
    /// </summary>
    public class ChainSweepProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        private BAWVerletChain chain;
        private float timer;

        private NPC Owner => Projectile.ai[0] >= 0 && Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.netImportant = true;
        }

        public override void AI() {
            NPC owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            timer++;
            chain ??= new BAWVerletChain(7, 24f, owner.Center);

            // 随体公转: 半径随时间展开
            float angle = Projectile.ai[1] + timer * 0.16f;
            float radius = MathF.Min(165f, 50f + timer * 3.5f);
            Projectile.Center = owner.Center + angle.ToRotationVector2() * radius;
            Projectile.rotation = angle + MathHelper.Pi * 0.75f;

            chain.SegLen = MathF.Max(12f, radius / (chain.Count - 1) * 1.08f);
            chain.Step(owner.Center, 4, Projectile.Center);

            // 高速时的离心火花
            if (!Main.dedServ && owner.velocity.Length() > 16f && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 6f;
            }

            Lighting.AddLight(Projectile.Center, new Color(100, 80, 130).ToVector3() * 0.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC owner = Owner;
            if (owner == null || owner.velocity.Length() <= 16f)
                return false; // 仅冲刺爆发帧有伤害
            return null;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyChainBound(60);
            SoundEngine.PlaySound(SoundID.NPCHit2, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Owner == null)
                return false;
            SpriteBatch sb = Main.spriteBatch;

            bool hot = Owner.velocity.Length() > 16f;
            Color glow = hot ? new Color(160, 110, 240) : new Color(90, 80, 130);
            BAWHelper.DrawVerletChain(sb, chain, new Color(52, 48, 66), glow, 0.7f);
            BAWHelper.DrawSickleWithTrail(sb, Projectile.Center, Projectile.rotation,
                hot ? Color.White : Color.White * 0.75f, Projectile.scale * 1.1f, null, null);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 8; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5f, 5f);
            }
        }
    }

    /// <summary>
    /// 黑无常拘魂锁 (V3): 追踪抓取, 命中后短促牵拉 (仅本地玩家施力, 限幅 ≤1.3px/f, 55f 即断)。
    /// ai[0]=所属 NPC, ai[1]=已抓玩家 whoAmI+1 (0=未抓)。
    /// </summary>
    public class ChainPullProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        private BAWVerletChain chain;
        private float latchTimer;
        private float spin;

        private NPC Owner => Projectile.ai[0] >= 0 && Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;

        private Player Latched => Projectile.ai[1] >= 1f && Projectile.ai[1] <= Main.maxPlayers
            ? Main.player[(int)Projectile.ai[1] - 1] : null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.netImportant = true;
        }

        public override void AI() {
            NPC owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            chain ??= new BAWVerletChain(10, 26f, owner.Center);
            Player latched = Latched;

            if (latched == null) {
                // 追踪最近玩家 (温和转向, 可甩)
                Player closest = null;
                float best = 900f;
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p != null && p.active && !p.dead) {
                        float dist = p.Distance(Projectile.Center);
                        if (dist < best) { best = dist; closest = p; }
                    }
                }
                if (closest != null) {
                    Vector2 to = (closest.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * 17f, 0.05f);
                }

                spin += 0.26f;
                Projectile.rotation = spin;

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.1f;
                    d.velocity = -Projectile.velocity * 0.12f;
                }
            }
            else if (latched.active && !latched.dead) {
                // 牵拉: 位置钉在玩家身上, 只对本地玩家施力 (客户端权威, 限幅)
                latchTimer++;
                Projectile.Center = latched.Center;
                Projectile.rotation = (owner.Center - latched.Center).ToRotation() + MathHelper.Pi;

                if (latched.whoAmI == Main.myPlayer) {
                    Vector2 pull = (owner.Center - latched.Center).SafeNormalize(Vector2.Zero);
                    latched.velocity += pull * MathF.Min(1.3f, latchTimer * 0.08f);
                }
                latched.GetModPlayer<BAWPlayer>().ApplyChainBound(6);

                // 沿链流动的粒子
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 pos = Vector2.Lerp(latched.Center, owner.Center, Main.rand.NextFloat());
                    var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 0.9f;
                    d.velocity = (owner.Center - latched.Center).SafeNormalize(Vector2.Zero) * 6f;
                }

                // 短促即断: 55f 或已被拉近
                if (latchTimer > 55f || latched.Distance(owner.Center) < 320f)
                    Projectile.Kill();
            }
            else {
                Projectile.Kill();
            }

            chain.SegLen = MathF.Max(14f, Vector2.Distance(owner.Center, Projectile.Center) / (chain.Count - 1) * 1.05f);
            chain.Step(owner.Center, 4, Projectile.Center);
            Lighting.AddLight(Projectile.Center, new Color(100, 80, 140).ToVector3() * 0.5f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.ai[1] < 1f) {
                Projectile.ai[1] = target.whoAmI + 1;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
                target.GetModPlayer<BAWPlayer>().ApplyChainBound(120);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.5f, Volume = 1.2f }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 14; i++) {
                        var d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame);
                        d.noGravity = true;
                        d.scale = 1.5f;
                        d.velocity = Main.rand.NextVector2CircularEdge(7f, 7f);
                    }
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 已抓住后不再重复判伤
            return Latched != null ? false : null;
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = Owner;
            if (owner == null)
                return false;
            SpriteBatch sb = Main.spriteBatch;

            bool latched = Latched != null;
            Color glow = latched ? new Color(200, 100, 150) : new Color(130, 110, 180);
            BAWHelper.DrawVerletChain(sb, chain, latched ? new Color(80, 46, 60) : new Color(52, 48, 66), glow, 0.8f);
            BAWHelper.DrawSickleWithTrail(sb, Projectile.Center, Projectile.rotation,
                Color.White, latched ? 1.25f : 1.05f, latched ? null : Projectile.oldPos, Projectile.oldRot);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(6f, 6f);
            }
        }
    }

    /// <summary>
    /// 勾魂链 (V3): C2 协同技 —— 连接黑白二使的黑白渐变 Verlet 链。
    /// 60f 松垂结链 (无伤害, 白→红渐变读条) → 绷直致命 (线段判定, 双使恒速公转) → 崩断收场。
    /// ai[0]=黑无常, ai[1]=白无常。
    /// </summary>
    public class SoulChainProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "Sickle";

        private BAWVerletChain chain;
        private float timer;
        private bool snapped;

        private NPC BlackImp => Projectile.ai[0] >= 0 && Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;
        private NPC WhiteImp => Projectile.ai[1] >= 0 && Projectile.ai[1] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[1]] : null;

        private const float FormTime = 60f;   // 结链读条
        private const float SnapTime = 340f;  // 崩断时刻

        private bool Taut => timer > FormTime && !snapped;

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 380;
            Projectile.netImportant = true;
        }

        public override void AI() {
            NPC black = BlackImp;
            NPC white = WhiteImp;
            if (black == null || white == null || !black.active || !white.active) {
                Projectile.Kill();
                return;
            }

            timer++;
            chain ??= new BAWVerletChain(16, 40f, black.Center);
            Projectile.Center = (black.Center + white.Center) * 0.5f;

            float dist = Vector2.Distance(black.Center, white.Center);
            if (!snapped) {
                // 松垂 → 绷直: 节距从冗余收紧到贴线
                float slack = timer < FormTime ? MathHelper.Lerp(1.45f, 1.02f, timer / FormTime) : 1.005f;
                chain.SegLen = dist / (chain.Count - 1) * slack;
                chain.Gravity = timer < FormTime ? 0.6f : 0.12f;
                chain.Step(black.Center, 5, white.Center);
            }
            else {
                // 崩断: 双段回收, 只跑自由端
                chain.Gravity = 0.7f;
                chain.Step(black.Center);
            }

            if (timer == FormTime + 1f) {
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.6f, Volume = 1.2f }, Projectile.Center);
                ACMScreenShakeSystem.Add(5f);
            }

            // 崩断前 18f 收缩闪烁预告
            if (!snapped && timer >= SnapTime) {
                snapped = true;
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = 0.2f }, Projectile.Center);
                ACMScreenShakeSystem.Add(6f);
                // 崩断冲量: 波沿链传播
                for (int i = 1; i < chain.Count - 1; i++)
                    chain.ApplyImpulse(i, Main.rand.NextVector2Circular(9f, 9f));
                if (!Main.dedServ) {
                    for (int i = 0; i < 22; i++) {
                        Vector2 pos = Vector2.Lerp(black.Center, white.Center, Main.rand.NextFloat());
                        var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Shadowflame : DustID.SpectreStaff);
                        d.noGravity = true;
                        d.scale = 1.6f;
                        d.velocity = Main.rand.NextVector2Circular(8f, 8f);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, new Color(180, 150, 200).ToVector3() * 0.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Taut)
                return false;
            NPC black = BlackImp;
            NPC white = WhiteImp;
            if (black == null || white == null)
                return false;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                black.Center, white.Center, 24f, ref _);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplySoulLock(120);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.3f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC black = BlackImp;
            NPC white = WhiteImp;
            if (black == null || white == null || chain == null)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D chainTex = BAWHelper.ChainTexture;
            if (chainTex == null)
                return false;

            // 崩断预告: 末 18f 高频闪烁
            float warnFlash = !snapped && timer > SnapTime - 18f && (int)timer % 4 < 2 ? 1.6f : 1f;
            float readK = MathHelper.Clamp(timer / FormTime, 0f, 1f);
            float alpha = snapped ? MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f) : 1f;

            Vector2 origin = new(chainTex.Width * 0.5f, 0f);
            for (int i = 0; i < chain.Count - 1; i++) {
                Vector2 a = chain.Pos[i];
                Vector2 b = chain.Pos[i + 1];
                Vector2 seg = b - a;
                float len = seg.Length();
                if (len < 0.5f)
                    continue;

                // 黑白渐变: 黑端幽紫 → 白端暖白; 读条期整体向致命红渐近
                float k = i / (float)(chain.Count - 1);
                Color body = Color.Lerp(new Color(58, 50, 80), new Color(225, 228, 240), k);
                Color glow = Color.Lerp(BAWFX.YinColor, BAWFX.YangColor, k);
                if (Taut)
                    glow = Color.Lerp(glow, TelegraphColors.Lethal, 0.55f);
                else
                    glow = Color.Lerp(glow, TelegraphColors.Lethal, readK * 0.4f);
                glow.A = 0;

                float rot = seg.ToRotation() - MathHelper.PiOver2;
                Vector2 scale = new(0.9f * warnFlash, len / chainTex.Height);
                sb.Draw(chainTex, a - Main.screenPosition, null, glow * (0.4f * alpha * warnFlash), rot, origin, scale * 1.5f, SpriteEffects.None, 0);
                sb.Draw(chainTex, a - Main.screenPosition, null, body * alpha, rot, origin, scale, SpriteEffects.None, 0);
            }

            // 双镰沿链滑行 (绷直期)
            if (Taut) {
                var sickleTex = BAWHelper.SickleTexture;
                if (sickleTex != null) {
                    for (int i = 0; i < 2; i++) {
                        float slide = 0.5f + MathF.Sin(timer * 0.03f + i * MathHelper.Pi) * 0.36f;
                        Vector2 pos = Vector2.Lerp(black.Center, white.Center, slide);
                        Color c = i == 0 ? new Color(110, 100, 140) : new Color(230, 230, 245);
                        sb.Draw(sickleTex, pos - Main.screenPosition, null, c,
                            timer * 0.2f + i * MathHelper.Pi, sickleTex.Size() * 0.5f, 1.15f, SpriteEffects.None, 0);
                    }
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f }, Projectile.Center);
            for (int i = 0; i < 20; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(60f, 60f),
                    Main.rand.NextBool() ? DustID.Shadowflame : DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2CircularEdge(9f, 9f);
            }
        }
    }
}
