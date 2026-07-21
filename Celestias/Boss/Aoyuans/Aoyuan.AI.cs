using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    internal partial class Aoyuan
    {
        #region AI主循环 — 状态机架构

        public override bool PreAI() {
            globalTime += 1f / 60f;

            // 激活天空背景
            if (!VaultUtils.isServer && AoyuanSky.name != null) {
                if (!SkyManager.Instance[AoyuanSky.name].IsActive())
                    SkyManager.Instance.Activate(AoyuanSky.name, NPC.Center);
            }

            // 目标与脱战
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            if ((!player.active || player.dead) && CurrentState != AoyuanState.DeathAnim) {
                despawn = true;
            }
            if (despawn) {
                if (!VaultUtils.isServer && AoyuanSky.name != null)
                    SkyManager.Instance.Deactivate(AoyuanSky.name);
                NPC.velocity.Y -= 0.4f;
                NPC.dontTakeDamage = true;
                NPC.ai[3]++;
                if (NPC.ai[3] >= 300)
                    NPC.active = false;
                return false;
            }

            // 蠕虫身体链生成（首次）
            SpawnWormBody();

            // 显隐: 入场未现身/入镜隐没时保持全透明
            if (BodyHidden) {
                NPC.alpha = 255;
            }
            else if (NPC.alpha > 0) {
                NPC.alpha -= 25;
                if (NPC.alpha < 0) NPC.alpha = 0;
            }

            // 张嘴动画帧推进
            UpdateMouthAnimation();

            // 踉跄易伤窗倒计时
            if (staggerTimer > 0)
                staggerTimer--;

            // === 阶段转换检查 ===
            if (IsPhase2 && !DidPhase2Transition
                && CurrentState != AoyuanState.PhaseTransition
                && CurrentState != AoyuanState.Intro
                && CurrentState != AoyuanState.DeathAnim) {
                EnterState(AoyuanState.PhaseTransition);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    AoyuanAttacks.ClearHostileProjectiles();
            }

            // === 状态机主循环 ===
            StateTimer++;
            switch (CurrentState) {
                case AoyuanState.Intro:
                    RunIntro(player);
                    break;
                case AoyuanState.Patrol:
                    RunPatrol(player);
                    break;
                case AoyuanState.Sheath:
                    RunSheath(player);
                    break;
                case AoyuanState.Attacking:
                    RunAttacking(player);
                    break;
                case AoyuanState.PhaseTransition:
                    RunPhaseTransition(player);
                    break;
                case AoyuanState.DeathAnim:
                    RunDeathAnim(player);
                    break;
            }

            // 蠕虫朝向与旋转
            NPC.rotation = (float)Math.Atan2(NPC.velocity.Y, NPC.velocity.X) + 1.57f;
            NPC.spriteDirection = NPC.velocity.X > 0 ? -1 : 1;

            // 伤害窗口与视觉严格对齐: 突刺帧满额, 静滞/巡逻只留 45% 的"擦身"伤害
            BladeActive = NPC.velocity.Length() > 30f && CurrentState == AoyuanState.Attacking;
            bool noContact = BodyHidden || CurrentState == AoyuanState.Intro
                || CurrentState == AoyuanState.PhaseTransition || CurrentState == AoyuanState.DeathAnim;
            NPC.damage = noContact ? 0 : (int)(contactDamageBase * (BladeActive ? 1.35f : 0.45f));

            // 冰霜光照
            float lightMul = IsPhase2 ? 1.5f : 1f;
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.6f, 0.9f) * glowIntensity * lightMul);

            // 屏幕演出标量（纯本地视觉）
            UpdateScreenFx(player);

            if ((NPC.velocity.X > 0 && NPC.oldVelocity.X < 0 || NPC.velocity.X < 0 && NPC.oldVelocity.X > 0 ||
                 NPC.velocity.Y > 0 && NPC.oldVelocity.Y < 0 || NPC.velocity.Y < 0 && NPC.oldVelocity.Y > 0) && !NPC.justHit)
                NPC.netUpdate = true;

            return false;
        }

        /// <summary>统一状态切换: 清计时器 + netUpdate</summary>
        private void EnterState(AoyuanState state) {
            CurrentState = state;
            StateTimer = 0;
            ParamB = 0;
            NPC.netUpdate = true;
        }

        #endregion

        #region 屏幕演出标量

        /// <summary>
        /// 每帧平滑推进屏幕演出标量并发布给 <see cref="AoyuanFrostScreenSystem"/>。
        /// 设计契约: 二阶段常驻轻度氛围底色; 昂贵的 AoyuanCrystalline 棱镜后处理(单一全屏名额)
        /// 只在签名时刻(绝对零度/破境/死亡/出剑瞬间)拉起。红色只留给真正致命的伤害源。
        /// </summary>
        private void UpdateScreenFx(Player player) {
            if (Main.dedServ)
                return;

            bool azCharging = CurrentState == AoyuanState.Attacking
                && (AoyuanAttackType)(int)NPC.ai[2] == AoyuanAttackType.AbsoluteZero
                && WeakPointsExposed;
            float azProgress = azCharging ? MathHelper.Clamp((StateTimer - 30f) / 190f, 0f, 1f) : 0f;

            // 蓄力末段渐增震屏(处决级预警, charge³ 曲线)
            if (azCharging)
                ACMUtils.AddScreenShake(azProgress * azProgress * azProgress * 4f);

            // —— ElementalScreenTint 氛围底色: 二阶段常驻, 蓄力/冻爆加浓 ——
            float tintTarget = IsPhase2 ? 0.45f : 0.08f;
            if (azCharging)
                tintTarget = Math.Max(tintTarget, 0.35f + azProgress * 0.4f);
            tintTarget = Math.Max(tintTarget, freezeBloom * 0.9f);
            frostTint = MathHelper.Lerp(frostTint, tintTarget, 0.04f);

            // —— 冻爆泛光逐帧衰减 ——
            if (freezeBloom > 0f)
                freezeBloom = Math.Max(0f, freezeBloom - 0.025f);

            // —— ArenaRunic 霜冻法阵地纹: 绝对零度蓄力向心收口预警 ——
            float runicTarget = azCharging ? 0.35f + azProgress * 0.5f : 0f;
            arenaRunic = MathHelper.Lerp(arenaRunic, runicTarget, 0.07f);

            // —— AoyuanCrystalline 棱镜标量 ——
            float crystalTarget = 0f;
            float stillTarget = 0f;
            float frostEdgeTarget = IsPhase2 ? 0.12f : 0f;

            switch (CurrentState) {
                case AoyuanState.Intro:
                    // 入场: 天幕结霜自边缘漫入
                    frostEdgeTarget = Math.Max(frostEdgeTarget, MathHelper.Clamp(StateTimer / 120f, 0f, 1f) * 0.4f);
                    crystalTarget = StateTimer > IntroMirrorTime ? 0.12f : 0f;
                    break;
                case AoyuanState.PhaseTransition:
                    // 时滞破境: 去饱和 + 全屏棱面化
                    stillTarget = StateTimer < 95f ? MathHelper.Clamp((StateTimer - 20f) / 70f, 0f, 1f) * 0.85f : 0f;
                    crystalTarget = 0.35f;
                    break;
                case AoyuanState.DeathAnim:
                    stillTarget = MathHelper.Clamp((StateTimer - 40f) / 180f, 0f, 1f);
                    crystalTarget = 0.25f + MathHelper.Clamp(StateTimer / 250f, 0f, 1f) * 0.4f;
                    break;
            }
            if (azCharging)
                crystalTarget = Math.Max(crystalTarget, 0.15f + azProgress * 0.45f);
            crystalTarget = Math.Max(crystalTarget, freezeBloom * 0.7f);
            // 出剑瞬间的短促棱面脉冲
            crystalTarget = Math.Max(crystalTarget, slashFlash * 0.30f);

            crystalFx = MathHelper.Lerp(crystalFx, crystalTarget, crystalTarget > crystalFx ? 0.12f : 0.05f);
            stillFx = MathHelper.Lerp(stillFx, stillTarget, stillTarget > stillFx ? 0.10f : 0.06f);
            frostEdge = MathHelper.Lerp(frostEdge, frostEdgeTarget, 0.04f);

            // 冲击帧自然衰减（由死亡碎裂一次性置 1）
            if (flashFx > 0f)
                flashFx = Math.Max(0f, flashFx - 0.09f);

            // 预警线视觉衰减
            if (telegraphAlpha > 0f && CurrentState != AoyuanState.Attacking && CurrentState != AoyuanState.Intro)
                telegraphAlpha = Math.Max(0f, telegraphAlpha - 0.1f);
            if (telegraphLock > 0f)
                telegraphLock = Math.Max(0f, telegraphLock - 0.05f);
            if (slashFlash > 0f)
                slashFlash = Math.Max(0f, slashFlash - 0.08f);

            AoyuanFrostScreenSystem.Publish(NPC.Center, frostTint, freezeBloom, arenaRunic, globalTime);
        }

        #endregion

        #region 张嘴动画

        private void UpdateMouthAnimation() {
            if (fireAttack) {
                attackCounter++;
                if (attackCounter > 8) {
                    attackFrame++;
                    attackCounter = 0;
                }
                if (attackFrame >= HeadFrameCount)
                    attackFrame = HeadFrameCount - 1;
            }
            else {
                attackFrame = 0;
                attackCounter = 0;
            }
        }

        private void OpenMouth() {
            fireAttack = true;
            attackFrame = 0;
            attackCounter = 0;
        }

        private void CloseMouth() {
            fireAttack = false;
            attackFrame = 0;
            attackCounter = 0;
        }

        #endregion

        #region 蠕虫身体生成

        private void SpawnWormBody() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (NPC.ai[0] != 0) return;

            NPC.realLife = NPC.whoAmI;
            int latestNPC = NPC.whoAmI;
            for (int i = 0; i < BodyFrameSequence.Length; ++i) {
                latestNPC = NPC.NewNPC(NPC.GetSource_FromAI(),
                    (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<AoyuanBody>(), NPC.whoAmI, 0, latestNPC);
                Main.npc[latestNPC].realLife = NPC.whoAmI;
                Main.npc[latestNPC].ai[3] = NPC.whoAmI;
                Main.npc[latestNPC].ai[2] = BodyFrameSequence[i];
                Main.npc[latestNPC].ai[0] = i; // 段序号（尾段最大, 死亡晶化顺序用）
                Main.npc[latestNPC].netUpdate = true;
            }
            NPC.ai[0] = 1;

            // 出生即进入入场演出: 隐身盘蜷
            BodyHidden = true;
            NPC.dontTakeDamage = true;
            EnterState(AoyuanState.Intro);
        }

        #endregion

        #region 运动学原语

        /// <summary>
        /// 盘蜷: 头部追踪绕锚点旋转的目标点, 蛇身自然盘成"剑鞘"环。
        /// coilAngle 各端本地积分（纯位置修饰, 关键帧由 netUpdate 校正）。
        /// </summary>
        private void CoilAround(Vector2 anchor, float radius, float angSpeed, float snap = 0.5f) {
            coilAngle += angSpeed * orbitDir;
            Vector2 target = anchor + coilAngle.ToRotationVector2() * radius;
            Vector2 toTarget = target - NPC.Center;
            float dist = toTarget.Length();
            Vector2 desired = toTarget.SafeNormalize(Vector2.UnitY) * Math.Clamp(dist * 0.30f, 4f, 30f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, snap);
        }

        /// <summary>向指定点平滑移动（速度差追赶）</summary>
        private void GlideTo(Vector2 target, float speed, float accel) {
            Vector2 dir = target - NPC.Center;
            float length = dir.Length();
            if (length < 8f) return;
            dir = dir.SafeNormalize(Vector2.UnitY) * speed;
            if (NPC.velocity.X < dir.X) NPC.velocity.X += accel;
            else if (NPC.velocity.X > dir.X) NPC.velocity.X -= accel;
            if (NPC.velocity.Y < dir.Y) NPC.velocity.Y += accel;
            else if (NPC.velocity.Y > dir.Y) NPC.velocity.Y -= accel;
        }

        /// <summary>
        /// 突刺沿途铺设冰封航迹（服务器）: 每 spacing px 一段。
        /// 剑痕朝向经 ai2 传递（多人下 rotation 不同步, ai 同步）。
        /// </summary>
        private void EmitThrustWake(float spacing, int crystallizeDelay, int lifeAfter) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (Vector2.Distance(NPC.Center, lastWakePos) < spacing)
                return;
            lastWakePos = NPC.Center;
            Terraria.Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<AoyuanPermafrostTrail>(),
                (int)(contactDamageBase * 0.28f), 0f, Main.myPlayer,
                ai0: crystallizeDelay, ai1: lifeAfter, ai2: NPC.velocity.ToRotation());
        }

        #endregion

        #region 状态：巡逻（收剑巡游）

        private void RunPatrol(Player player) {
            // 绕玩家的克制盘旋: 锚点在玩家周围缓慢公转 + 正弦游摆
            orbitAngle += 0.017f * orbitDir;
            float orbitR = 520f + MathF.Sin(globalTime * 0.9f) * 55f;
            Vector2 anchor = player.Center + orbitAngle.ToRotationVector2() * orbitR;

            float speed = IsPhase2 ? 16f : 13f;
            float accel = IsPhase2 ? 0.30f : 0.24f;

            // 距离栓绳: 玩家逃远则强力收拢, 防脱屏绕圈
            float dist = Vector2.Distance(NPC.Center, player.Center);
            if (dist > 1100f) {
                speed *= 1.6f;
                accel *= 1.6f;
                anchor = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.UnitY) * 600f;
            }

            GlideTo(anchor, speed, accel);

            // 呼吸寒气（轻, 不铺屏）
            if (!VaultUtils.isServer && Main.rand.NextBool(9))
                AoyuanHelper.CreateFrostTrail(NPC.Center, NPC.velocity, 0.7f);

            if (StateTimer >= patrolDuration) {
                ChooseNextAttack(player);
            }
        }

        #endregion

        #region 状态：收剑连接拍

        /// <summary>攻击后的段落句号: 25f 直线缓滑 + 收剑冰铃, 让玩家的眼睛翻页</summary>
        private void RunSheath(Player player) {
            NPC.velocity *= 0.955f;

            if (StateTimer == 6 && !VaultUtils.isServer)
                AoyuanHelper.PlayChime(NPC.Center, -0.55f, 0.6f);

            if (StateTimer >= SheathDuration) {
                EnterState(AoyuanState.Patrol);
                orbitDir = Main.rand.NextBool() ? 1 : -1;
                int min = IsPhase2 ? PatrolMinP2 : PatrolMin;
                int max = IsPhase2 ? PatrolMaxP2 : PatrolMax;
                if (IsDesperation) { min = min * 3 / 4; max = max * 3 / 4; }
                patrolDuration = Main.rand.Next(min, max);
            }
        }

        /// <summary>攻击结束统一走收剑拍</summary>
        private void FinishAttack() {
            CloseMouth();
            WeakPointsExposed = false;
            EnterState(AoyuanState.Sheath);
        }

        #endregion

        #region 状态：攻击执行

        private void RunAttacking(Player player) {
            AoyuanAttackType currentAttack = (AoyuanAttackType)(int)NPC.ai[2];

            bool finished = currentAttack switch {
                AoyuanAttackType.InstantThrust => AttackInstantThrust(player),
                AoyuanAttackType.MirrorArray => AttackMirrorArray(player),
                AoyuanAttackType.ColdWave => AttackColdWave(player),
                AoyuanAttackType.FreezeTrap => AttackFreezeTrap(player),
                AoyuanAttackType.FrostBlades => AttackFrostBlades(player),
                AoyuanAttackType.AbsoluteZero => AttackAbsoluteZero(player),
                AoyuanAttackType.MirrorRealm => AttackMirrorRealm(player),
                _ => true
            };

            // 保底出口: 任何攻击超时强制收剑（状态机不允许死路）
            if (StateTimer > 900)
                finished = true;

            if (finished) {
                BodyHidden = false;
                NPC.dontTakeDamage = false;
                FinishAttack();
            }
        }

        #endregion

        #region 攻击选择 — 洗牌袋

        /// <summary>
        /// 洗牌袋 + 防重: 每袋 = 当前阶段全招式池洗牌; 袋首与上一招相同则后移;
        /// 绝对零度每袋至多一次且不排袋首。服务器权威。
        /// </summary>
        private void ChooseNextAttack(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            if (attackBag.Count == 0)
                RefillAttackBag();

            AoyuanAttackType chosen = attackBag[0];
            attackBag.RemoveAt(0);
            lastAttack = chosen;

            NPC.ai[2] = (float)chosen;
            ParamA = 0;
            EnterState(AoyuanState.Attacking);
        }

        private void RefillAttackBag() {
            attackBag.Clear();
            attackBag.Add(AoyuanAttackType.InstantThrust);
            attackBag.Add(AoyuanAttackType.MirrorArray);
            attackBag.Add(AoyuanAttackType.ColdWave);
            attackBag.Add(AoyuanAttackType.FreezeTrap);
            attackBag.Add(AoyuanAttackType.FrostBlades);
            if (IsPhase2) {
                attackBag.Add(AoyuanAttackType.AbsoluteZero);
                attackBag.Add(AoyuanAttackType.MirrorRealm);
            }

            // Fisher-Yates 洗牌
            for (int i = attackBag.Count - 1; i > 0; i--) {
                int j = Main.rand.Next(i + 1);
                (attackBag[i], attackBag[j]) = (attackBag[j], attackBag[i]);
            }

            // 防重: 袋首 == 上一招 → 与中位交换
            if (attackBag.Count > 1 && attackBag[0] == lastAttack)
                (attackBag[0], attackBag[attackBag.Count / 2]) = (attackBag[attackBag.Count / 2], attackBag[0]);

            // 大招不做开袋第一印象（换阶段刚结束时给玩家喘息）
            if (attackBag.Count > 1 && attackBag[0] == AoyuanAttackType.AbsoluteZero)
                (attackBag[0], attackBag[1]) = (attackBag[1], attackBag[0]);
        }

        #endregion
    }
}
