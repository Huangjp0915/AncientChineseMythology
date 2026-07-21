using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 黑熊幼灵 (黑熊法杖召唤物)。
    /// 形象复用黑熊精 Boss 精灵图 (只读贴图资源, 0.22 缩放幽灵化着色 + 金冠亮点)。
    /// 攻击循环: 蓄势后仰(前摇) → 黑风猛扑(爆发, 仅此段有接触判定) → 落掌震击(BlackBearStaffProj2 AoE)
    /// → 反冲收招。每第 4 次落掌为「金冠怒击」: 前摇更长 + 金光定格, 震击 ×1.5。
    /// </summary>
    public class BlackBearStaffProj1 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/idle_344";

        // —— 状态机 (ai 同步字段) ——
        private const int StIdle = 0, StWindup = 1, StPounce = 2, StRecover = 3;
        private ref float State => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float SlamCount => ref Projectile.ai[2];

        private const int WindupTime = 18;
        private const int FuryExtra = 8;      // 怒击追加前摇 (金光定格)
        private const int PounceMax = 16;
        private const int RecoverTime = 22;
        private const float SpriteScale = 0.22f;

        /// <summary>下一次落掌是否为金冠怒击 (每第 4 掌)。</summary>
        private bool FuryNext => ((int)SlamCount % 4) == 3;
        private int CurWindup => WindupTime + (FuryNext ? FuryExtra : 0);

        private int _materializeTimer;        // 显形计时 (纯视觉)

        // —— 精灵图静态缓存 (与 Boss 共享 Asset 缓存, 客户端惰性一次) ——
        private static Texture2D texIdle, texRun;
        private static readonly SoundStyle PounceSound = new("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_1");
        private static readonly SoundStyle FuryRoar = new("AncientChineseMythology/Sounds/BlackBear/BlackBear_Roar");

        private static void EnsureTextures() {
            if (texIdle != null)
                return;
            static Texture2D Load(string name) =>
                ModContent.Request<Texture2D>("AncientChineseMythology/Textures/NPCs/Boss/BlackBear/" + name, AssetRequestMode.ImmediateLoad).Value;
            texIdle = Load("idle_344");   // 332x1376, 4 帧
            texRun = Load("run_332");     // 304x1992, 6 帧
        }

        public override void SetStaticDefaults() {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 38;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.light = 0.4f;
            Projectile.minion = true;
            Projectile.minionSlots = 0.5f;
            Projectile.aiStyle = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.netImportant = true;
        }

        public override bool? CanCutTiles() => false;

        // 只有猛扑段有接触判定 (伤害窗口与视觉冲刺严格对齐)
        public override bool? CanDamage() => State == StPounce ? null : false;

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (_materializeTimer < 20)
                _materializeTimer++;

            if (!player.active || player.dead) {
                Projectile.Kill();
                return;
            }
            if (player.HasBuff<Buffs.BuffsBlackBearStaff>())
                Projectile.timeLeft = 2;

            // 离主人太远: 强制回归 (公平阀)
            if (Vector2.Distance(player.Center, Projectile.Center) > 2000f && State != StIdle) {
                State = StIdle;
                Timer = 0f;
            }

            NPC target = FindTarget(player);

            switch ((int)State) {
                case StIdle:
                    IdleFollow(player);
                    if (target != null) {
                        State = StWindup;
                        Timer = 0f;
                        Projectile.netUpdate = true;
                        if (FuryNext && !Main.dedServ)
                            SoundEngine.PlaySound(FuryRoar with { Volume = 0.32f, Pitch = 0.15f }, Projectile.Center);
                    }
                    break;

                case StWindup: {
                    Timer++;
                    if (target == null) {
                        State = StIdle; Timer = 0f;
                        break;
                    }
                    // 蓄势后仰: 末段 pow(4) 突然向后吸 (late-snap reel-back)
                    float t = Timer / CurWindup;
                    Vector2 away = (Projectile.Center - target.Center).SafeNormalize(-Vector2.UnitY);
                    Projectile.velocity = Projectile.velocity * 0.80f + away * MathF.Pow(t, 4f) * 5f;
                    Projectile.spriteDirection = target.Center.X > Projectile.Center.X ? 1 : -1;

                    // 黑风汇聚预警 (客户端纯视觉)
                    if (!Main.dedServ && Main.rand.NextBool(FuryNext ? 1 : 2)) {
                        Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(46f, 46f);
                        Dust d = Dust.NewDustPerfect(from, DustID.Smoke, (Projectile.Center - from) * 0.09f, 150,
                            new Color(45, 45, 65), 1.25f);
                        d.noGravity = true;
                        if (FuryNext && Main.rand.NextBool(3)) {
                            Dust g = Dust.NewDustPerfect(from, DustID.GoldCoin, (Projectile.Center - from) * 0.10f, 100, default, 0.9f);
                            g.noGravity = true;
                        }
                    }

                    if (Timer >= CurWindup) {
                        // 猛扑: 一帧 set 速度 (launch is a set) + 提前量
                        Vector2 aim = target.Center + target.velocity * 6f - Projectile.Center;
                        Projectile.velocity = aim.SafeNormalize(Vector2.UnitX * Projectile.spriteDirection) * 21f;
                        State = StPounce;
                        Timer = 0f;
                        Projectile.netUpdate = true;
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(PounceSound with { Volume = 0.30f, Pitch = 0.25f }, Projectile.Center);
                            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = -0.2f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
                        }
                    }
                    break;
                }

                case StPounce: {
                    Timer++;
                    // 冲刺复利加速, 近乎直线 (straight reads fast)
                    if (Projectile.velocity.Length() < 30f)
                        Projectile.velocity *= 1.025f;
                    Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;

                    bool passed = target != null &&
                        Vector2.Dot(target.Center - Projectile.Center, Projectile.velocity) < 0f;
                    if (Timer >= PounceMax || passed)
                        DoSlam(Projectile.Center);
                    break;
                }

                case StRecover:
                    Timer++;
                    Projectile.velocity *= 0.90f;
                    if (Timer >= RecoverTime) {
                        State = StIdle;
                        Timer = 0f;
                    }
                    break;
            }
        }

        private NPC FindTarget(Player player) {
            // 右键锁敌优先 (修复旧版判断反转导致锁敌失效的 bug)
            if (player.HasMinionAttackTargetNPC) {
                NPC locked = Main.npc[player.MinionAttackTargetNPC];
                if (locked.CanBeChasedBy(this) && Vector2.Distance(locked.Center, Projectile.Center) < 2000f)
                    return locked;
            }
            int idx = Projectile.FindTargetWithLineOfSight(900f);
            return idx >= 0 ? Main.npc[idx] : null;
        }

        private void IdleFollow(Player player) {
            // 弹簧跟随玩家侧后方 + 呼吸浮动 (软追踪, 落后半拍的灵体感)
            Vector2 anchor = player.MountedCenter + new Vector2(-player.direction * 46f,
                -46f + MathF.Sin(Main.GameUpdateCount * 0.045f + Projectile.whoAmI * 1.3f) * 5f);
            Vector2 toAnchor = anchor - Projectile.Center;

            if (toAnchor.Length() > 1400f && Main.myPlayer == Projectile.owner) {
                Projectile.position = anchor - Projectile.Size * 0.5f;
                Projectile.velocity *= 0.1f;
                Projectile.netUpdate = true;
                return;
            }

            Projectile.velocity = (Projectile.velocity + toAnchor * 0.055f) * 0.86f;
            if (Projectile.velocity.Length() > 14f)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 14f;
            Projectile.spriteDirection = player.direction;
        }

        /// <summary>落掌: 生成熊掌震击 AoE (owner 端), 反冲收招。</summary>
        private void DoSlam(Vector2 pos) {
            bool fury = FuryNext;
            SlamCount++;
            if (Main.myPlayer == Projectile.owner) {
                int dmg = (int)(Projectile.damage * (fury ? 1.5f : 1f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<BlackBearStaffProj2>(), dmg, 6f, Projectile.owner, fury ? 1f : 0f);
            }
            Projectile.velocity *= -0.25f; // 落掌反冲 (recoil on emission)
            State = StRecover;
            Timer = 0f;
            Projectile.netUpdate = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 扑中敌人: 立即在敌人身上落掌 (提前引爆), 金辉命中反馈
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 0.8f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 2f);
            if (State == StPounce)
                DoSlam(target.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            EnsureTextures();
            if (texIdle == null || texRun == null)
                return false;

            // 帧选择: 待机/收招 = idle 4 帧慢放; 前摇/猛扑 = run 6 帧快放
            bool running = State == StWindup || State == StPounce;
            Texture2D tex = running ? texRun : texIdle;
            int frameH = running ? 332 : 344;
            int totalFrames = running ? 6 : 4;
            int frameTicks = State == StPounce ? 4 : running ? 6 : 12;
            int frame = (int)(Main.GameUpdateCount / (uint)frameTicks) % totalFrames;
            Rectangle src = new(0, frame * frameH, tex.Width, frameH);
            Vector2 origin = new(tex.Width / 2f, frameH / 2f);
            // 与 Boss 绘制同约定: 原图朝右, spriteDirection=1 时不翻转
            SpriteEffects fxFlip = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float rot = State == StPounce ? Projectile.velocity.X * 0.012f : 0f;

            // 召唤显形: 前 20 帧噪声溶解凝实 (金冠灼烧边)
            float materialize = MathHelper.Clamp(_materializeTimer / 20f, 0f, 1f);
            if (materialize < 1f) {
                WeaponVFX.ApplyDissolveBurn(tex, Projectile.Center, src, new Color(150, 155, 190),
                    rot, origin, SpriteScale, threshold: 1f - materialize, intensity: 1f,
                    edgeColor: new Color(255, 205, 110), edgeWidth: 0.12f, effects: fxFlip);
                return false;
            }

            // 猛扑拖尾: 黑风外层 + 金芯
            if (State == StPounce)
                WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
                    outerColor: new Color(28, 28, 44, 170), innerColor: new Color(255, 215, 120, 185),
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

            // 灵体底光 (暗金柔光)
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.42f, new Color(120, 95, 40) * 0.5f);

            // 本体: 幽灵化着色 (暗蓝灰半透)
            Color spirit = Color.Lerp(lightColor, new Color(110, 120, 170), 0.55f) * 0.92f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, spirit,
                rot, origin, SpriteScale, fxFlip, 0);

            // 金冠亮点 (怒击前摇时增辉充能)
            float furyCharge = State == StWindup && FuryNext ? MathHelper.Clamp(Timer / CurWindup, 0f, 1f) : 0f;
            float glint = 0.35f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f) + furyCharge * 0.8f;
            Vector2 crownPos = Projectile.Center + new Vector2(Projectile.spriteDirection * 9f, -frameH * SpriteScale * 0.34f);
            Color crown = new Color(255, 215, 110) * glint;
            crown.A = 0;
            Main.EntitySpriteDraw(ACMAsset.SoftGlow, crownPos - Main.screenPosition, null, crown,
                0f, ACMAsset.SoftGlow.Size() * 0.5f, 0.28f + furyCharge * 0.22f, SpriteEffects.None, 0);

            return false;
        }
    }
}
