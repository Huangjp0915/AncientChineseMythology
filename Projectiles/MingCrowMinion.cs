using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 冥鸦随从 (冥鸦法杖召唤物, 可多只成群)。
    /// 攻击循环: 目标上方错拍环伺(前摇) → 俯冲穿透(爆发, 仅此段有判定) → 拉起收招 → 再环伺。
    /// 每第 3 次为「缠绕俯冲」: 轨迹螺旋缠绕, 伤害 ×1.4, 命中鸦羽爆散。
    /// 多只冥鸦以 minionPos 相位错开 (待机盘旋与进攻轮换此起彼伏)。
    /// </summary>
    public class MingCrowMinion : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/MingCrowMinion/MingCrowMinion_Fly";

        private const int FramesPerAnim = 5;
        private const float TeleportThreshold = 1200f;

        // —— 状态机 (ai 同步字段) ——
        private const int StCircle = 0, StDive = 1, StRecover = 2;
        private ref float State => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float DiveCount => ref Projectile.ai[2];

        private const int DiveMax = 16;
        private const int RecoverTime = 14;

        /// <summary>当前/下一次俯冲是否为缠绕俯冲 (每第 3 次)。</summary>
        private bool SpiralDive => DiveCount > 0f && ((int)DiveCount % 3) == 0;
        private bool SpiralNext => (((int)DiveCount + 1) % 3) == 0;

        // —— 贴图静态缓存 (客户端惰性一次, 修复旧版 SetDefaults 每次生成即时加载) ——
        private static Texture2D texFly, texAttack;

        private static void EnsureTextures() {
            if (texFly != null)
                return;
            static Texture2D Load(string name) =>
                ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/MingCrowMinion/" + name, AssetRequestMode.ImmediateLoad).Value;
            texFly = Load("MingCrowMinion_Fly");
            texAttack = Load("MingCrowMinion_Attack");
        }

        public override void SetStaticDefaults() {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            Main.projFrames[Type] = FramesPerAnim;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.minionSlots = 1f;
            Projectile.aiStyle = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.netImportant = true;
        }

        public override bool? CanCutTiles() => false;

        // 伤害窗口 = 俯冲段 (与视觉冲刺严格对齐; 删除旧版 StrikeNPC 直击的多端多重伤害 bug)
        public override bool? CanDamage() => State == StDive ? null : false;

        public override void OnSpawn(IEntitySource source) {
            // 鼠标点显形冥火烟 (纯视觉)
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    Main.rand.NextVector2Circular(2.2f, 2.2f), 120, new Color(80, 130, 210), 1.2f);
                d.noGravity = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (SpiralDive)
                modifiers.FinalDamage *= 1.4f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool spiral = SpiralDive;
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Shadow, scale: spiral ? 1.15f : 0.8f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, spiral ? 2.5f : 1.5f);

            // 鸦羽爆散 (缠绕俯冲加倍)
            int feathers = spiral ? 16 : 8;
            for (int i = 0; i < feathers; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4.5f, 4.5f) * Main.rand.NextFloat(0.5f, 1f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Smoke, vel, 150, new Color(28, 32, 52), 1.35f);
                d.noGravity = true;
                if (i % 2 == 0) {
                    Dust b = Dust.NewDustPerfect(target.Center, DustID.IceTorch, vel * 1.2f, 120, default, 1.1f);
                    b.noGravity = true;
                }
            }
            if (spiral && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = 0.4f }, target.Center);
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Projectile.localAI[1]++;   // 显形计时 (纯视觉)

            //--------- 存活检查 ----------
            if (player.dead || !player.active) {
                player.ClearBuff(ModContent.BuffType<Buffs.MingCrowMinionBuff>());
                Projectile.Kill();
                return;
            }
            // Buff 由 MingCrowMinionBuff.Update 自维持 (鸦在则续), 此处只续命弹幕
            if (player.HasBuff(ModContent.BuffType<Buffs.MingCrowMinionBuff>()))
                Projectile.timeLeft = 2;

            // 离玩家太远: 瞬移回归 (owner 端写位置并同步)
            if (Main.myPlayer == player.whoAmI &&
                Vector2.Distance(player.Center, Projectile.Center) > TeleportThreshold && State != StDive) {
                Projectile.position = player.Center - new Vector2(0f, 48f) - Projectile.Size * 0.5f;
                Projectile.velocity *= 0.1f;
                Projectile.netUpdate = true;
            }

            NPC target = FindTarget(player);
            float phase = Projectile.minionPos * 2.4f;   // 群鸦相位错拍 (修复旧版全员重叠)

            switch ((int)State) {
                case StCircle: {
                    if (target == null) {
                        // 待机: 绕玩家头顶椭圆盘旋 (相位错开)
                        float a = Main.GameUpdateCount * 0.03f + phase;
                        Vector2 orbit = player.Center + new Vector2(MathF.Cos(a) * 72f, -48f + MathF.Sin(a) * 24f);
                        SoftChase(orbit, 9f, 0.10f);
                        Timer = 0f;
                        break;
                    }

                    // 环伺: 在目标上方弧线盘旋蓄势 (群鸦轮流进入俯冲)
                    Timer++;
                    float hoverA = Main.GameUpdateCount * 0.09f + phase;
                    Vector2 hover = target.Center + new Vector2(MathF.Cos(hoverA) * 116f, -124f - MathF.Sin(hoverA * 0.7f) * 22f);
                    SoftChase(hover, 13f, 0.12f);

                    float waitTime = 22f + (Projectile.minionPos % 3) * 8f; // 错拍出击
                    if (Timer >= waitTime && Vector2.Distance(Projectile.Center, target.Center) < 320f) {
                        // 俯冲: 一帧 set 速度 + 提前量 (launch is a set)
                        DiveCount++;
                        Vector2 aim = target.Center + target.velocity * 7f - Projectile.Center;
                        Projectile.velocity = aim.SafeNormalize(Vector2.UnitY) * 21f;
                        State = StDive;
                        Timer = 0f;
                        Projectile.netUpdate = true;
                        if (!Main.dedServ)
                            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.35f, Pitch = -0.35f + Main.rand.NextFloat(0.15f) }, Projectile.Center);
                    }
                    break;
                }

                case StDive: {
                    Timer++;
                    // 俯冲复利加速
                    if (Projectile.velocity.Length() < 30f)
                        Projectile.velocity *= 1.035f;

                    // 缠绕俯冲: 垂直于冲线的螺旋摆动 (视觉+判定同体)
                    if (SpiralDive) {
                        Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        Projectile.position += perp * MathF.Sin(Timer * 0.55f) * 7f;
                    }

                    bool passed = target != null &&
                        Vector2.Dot(target.Center - Projectile.Center, Projectile.velocity) < 0f;
                    if (Timer >= DiveMax || passed || target == null) {
                        // 拉起收招: 刹车 + 向上弧线
                        Projectile.velocity = Projectile.velocity * 0.4f + new Vector2(0f, -6f);
                        State = StRecover;
                        Timer = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;
                }

                case StRecover:
                    Timer++;
                    Projectile.velocity *= 0.92f;
                    if (Timer >= RecoverTime) {
                        State = StCircle;
                        Timer = 0f;
                    }
                    break;
            }

            //--- 卡墙救援: 计满 60 帧瞬移到玩家身旁 ---
            if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] > 60 && Main.myPlayer == player.whoAmI) {
                    Projectile.position = player.Center - Projectile.Size * 0.5f;
                    Projectile.velocity *= 0f;
                    Projectile.localAI[0] = 0;
                    Projectile.netUpdate = true;
                }
            }
            else {
                Projectile.localAI[0] = 0;
            }

            //朝向 + 俯冲侧倾 + 扑翼帧速 ∝ 速度
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? -1 : 1;
            float bankTarget = State == StDive ? Projectile.velocity.X * 0.022f : 0f;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, bankTarget, 0.2f);
            int frameTicks = Projectile.velocity.Length() > 8f ? 3 : 6;
            if (++Projectile.frameCounter >= frameTicks) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % FramesPerAnim;
            }
        }

        private NPC FindTarget(Player player) {
            if (player.HasMinionAttackTargetNPC) {
                NPC locked = Main.npc[player.MinionAttackTargetNPC];
                if (locked.CanBeChasedBy(this) && Vector2.Distance(locked.Center, Projectile.Center) < 1500f)
                    return locked;
            }
            int best = -1;
            float nearest = 700f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(this))
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < nearest) {
                    best = i;
                    nearest = dist;
                }
            }
            return best >= 0 ? Main.npc[best] : null;
        }

        /// <summary>软追踪目标点 (lerp 速度 + 上限, 保留灵体漂浮感)。</summary>
        private void SoftChase(Vector2 goal, float speed, float inertia) {
            Vector2 want = (goal - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, inertia);
        }

        //---------- 自绘 ----------
        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            EnsureTextures();
            if (texFly == null || texAttack == null)
                return false;

            bool attacking = State == StDive;
            Texture2D tex = attacking ? texAttack : texFly;
            int frameH = tex.Height / FramesPerAnim;
            Rectangle src = new(0, Projectile.frame * frameH, tex.Width, frameH);
            Vector2 origin = new(tex.Width * 0.5f, frameH * 0.5f);
            SpriteEffects fxFlip = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 出生显形: 前 18 帧噪声溶解凝实 (幽蓝灼烧边, 与系列召唤语言统一)
            if (Projectile.localAI[1] < 18f) {
                float mat = Projectile.localAI[1] / 18f;
                WeaponVFX.ApplyDissolveBurn(tex, Projectile.Center, src, Color.White,
                    Projectile.rotation, origin, 1f, threshold: 1f - mat, intensity: 1f,
                    edgeColor: new Color(120, 180, 255), edgeWidth: 0.12f, effects: fxFlip);
                return false;
            }

            // 俯冲: 幽蓝双层 ribbon 残影 (缠绕俯冲更亮)
            if (attacking) {
                float glow = SpiralDive ? 1.25f : 1f;
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f * glow,
                    outerColor: new Color(40, 70, 140, 150), innerColor: new Color(155, 215, 255, 200),
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.4f * glow, new Color(100, 160, 255));
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, lightColor,
                Projectile.rotation, origin, 1f, fxFlip, 0);
            return false;
        }
    }
}
