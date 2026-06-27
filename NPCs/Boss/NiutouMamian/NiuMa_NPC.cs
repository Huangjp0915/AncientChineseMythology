using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Underworlds;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    public class NiuMaPlayer : ModPlayer
    {
        private Vector2 ScrPos;
        private bool Start_SetScrPos = false;
        private float Timer_SetScrPos = 1;
        public void SetScreenPos(Vector2 ToVec) {
            ScrPos = Vector2.Lerp(ScrPos, ToVec - Main.ScreenSize.ToVector2() * .5f, .04f);
            Start_SetScrPos = true;
            Timer_SetScrPos = 0;
        }
        private int ShakeScale = 0, ShakeTime = 0;
        public void SetScreenShake(double _ShakeScale, double _ShakeTime) {
            ShakeScale = (int)_ShakeScale;
            ShakeTime = (int)_ShakeTime;
        }
        private float OldZoom;
        private float Target_SetZoom = 1, Timer_SetZoom = 1;
        private bool Start_SetZoom = false;
        public void SetZoom(float zoom) {
            Target_SetZoom = MathHelper.Lerp(Target_SetZoom, zoom, .02f);
            Start_SetZoom = true;
            Timer_SetZoom = 0;
        }

        public override void ModifyScreenPosition() {
            if (!Start_SetScrPos) {
                Timer_SetScrPos = 1;
                ScrPos = Main.screenPosition;
            }
            else {
                Main.screenPosition = ScrPos;
                if (Timer_SetScrPos < 0.9) {
                    Timer_SetScrPos = MathHelper.Lerp(Timer_SetScrPos, 1, .05f);
                    ScrPos = Vector2.Lerp(ScrPos, Player.Center - Main.ScreenSize.ToVector2() * .5f, Timer_SetScrPos * .1f);
                }
                else Start_SetScrPos = false;

            }

            if (ShakeTime > 0) {
                ShakeTime--;
                Main.screenPosition += new Vector2(ShakeScale).RotateRandom(8);
            }

            if (Start_SetZoom) {
                Main.GameZoomTarget = Target_SetZoom;

                if (Timer_SetZoom < .9f || Math.Abs(Main.GameZoomTarget - OldZoom) > .08) {
                    Timer_SetZoom = MathHelper.Lerp(Timer_SetZoom, 1, .05f);
                    Target_SetZoom = MathHelper.Lerp(Target_SetZoom, OldZoom, Timer_SetScrPos * .1f);

                }
                else {
                    Main.GameZoomTarget = OldZoom;
                    Start_SetZoom = false;
                }
            }
            else {
                Target_SetZoom = OldZoom = Main.GameZoomTarget;
            }
            base.ModifyScreenPosition();
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            var Niu_B = false;
            var Ma_B = false;
            foreach (var n in Main.npc) {
                if (Niu_B && Ma_B) break;
                if (n != null)
                    if (n.active) {
                        if (n.type == ModContent.NPCType<NiuTou>()) {
                            if (n.life < n.lifeMax * .5f) Niu_B = true;
                            continue;
                        }
                        else if (n.type == ModContent.NPCType<MaMian>()) {
                            if (n.life < n.lifeMax * .5f) Ma_B = true;
                            continue;
                        }
                    }
            }
            if (Niu_B && Ma_B) modifiers.FinalDamage *= 1.3f;
            base.ModifyHitByNPC(npc, ref modifiers);
        }
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            var Niu_B = false;
            var Ma_B = false;
            foreach (var n in Main.npc) {
                if (Niu_B && Ma_B) break;
                if (n != null)
                    if (n.active) {
                        if (n.type == ModContent.NPCType<NiuTou>()) {
                            if (n.life < n.lifeMax * .5f) Niu_B = true;
                            continue;
                        }
                        else if (n.type == ModContent.NPCType<MaMian>()) {
                            if (n.life < n.lifeMax * .5f) Ma_B = true;
                            continue;
                        }
                    }
            }
            if (Niu_B && Ma_B) modifiers.FinalDamage *= 1.3f;

            base.ModifyHitByProjectile(proj, ref modifiers);
        }

        // ================= 勾魂 Soul Hook (马面半血新机制) =================
        // 马面标记玩家 3s; 期间若玩家未攻击牛头打断 -> 到期被拉向马面 200px。
        // 标记 = 冥律 (UnderworldField.AddNetherDecree); 到期硬拽 = 轻魂蚀。
        public int SoulHookTimer;        // >0 = 标记倒计时 (180 起)
        public int SoulHookCaster = -1;  // 施放马面 whoAmI
        private int SoulHookPull;        // >0 = 正在被拽
        private Vector2 SoulHookPullTarget;

        public void ApplySoulHook(int casterWho) {
            if (SoulHookTimer > 0 || SoulHookPull > 0)
                return;
            SoulHookTimer = 180;
            SoulHookCaster = casterWho;
            Player.AddBuff(ModContent.BuffType<SoulHookBuff>(), 180);
            UnderworldField.AddNetherDecree(Player, 1); // 冥律: 灵魂牵引判定
            if (Player.whoAmI == Main.myPlayer)
                CombatText.NewText(Player.Hitbox, TelegraphColors.NetherViolet, "勾魂");
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 反制: 攻击牛头即可打断勾魂 (锁命角色分工)。
            if (SoulHookTimer > 0 && target.type == ModContent.NPCType<NiuTou>()) {
                SoulHookTimer = 0;
                SoulHookCaster = -1;
                Player.ClearBuff(ModContent.BuffType<SoulHookBuff>());
                if (Player.whoAmI == Main.myPlayer)
                    CombatText.NewText(Player.Hitbox, TelegraphColors.Safe, "勾魂已破");
            }
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void PostUpdate() {
            UpdateSoulHook();
        }

        private void UpdateSoulHook() {
            if (SoulHookPull > 0) {
                SoulHookPull--;
                Player.velocity *= 0.55f;
                Player.Center = Vector2.Lerp(Player.Center, SoulHookPullTarget, 0.22f);
                if (!Main.dedServ && Main.rand.NextBool()) {
                    var d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(22, 22), DustID.Shadowflame);
                    d.noGravity = true;
                    d.velocity = (SoulHookPullTarget - Player.Center).SafeNormalize(Vector2.Zero) * 4f;
                }
                return;
            }
            if (SoulHookTimer <= 0)
                return;

            SoulHookTimer--;
            // 预告: 玩家周身收束的幽紫符环 (越近到期越亮)。
            if (!Main.dedServ && SoulHookTimer % 3 == 0) {
                float frac = SoulHookTimer / 180f;
                float r = 38f + 54f * frac;
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Player.Center + a.ToRotationVector2() * r;
                var d = Dust.NewDustPerfect(pos, DustID.PurpleTorch);
                d.noGravity = true;
                d.velocity = (Player.Center - pos).SafeNormalize(Vector2.Zero) * 2.6f;
            }

            if (SoulHookTimer == 0) {
                // 未被打断 -> 拽向马面 (最多 200px)。
                Player.ClearBuff(ModContent.BuffType<SoulHookBuff>());
                NPC ma = (SoulHookCaster >= 0 && SoulHookCaster < Main.maxNPCs) ? Main.npc[SoulHookCaster] : null;
                if (ma != null && ma.active) {
                    Vector2 toMa = ma.Center - Player.Center;
                    float dist = toMa.Length();
                    Vector2 dir = toMa.SafeNormalize(Vector2.UnitX);
                    SoulHookPullTarget = Player.Center + dir * Math.Min(dist, 200f);
                    SoulHookPull = 14;
                    UnderworldField.AddSoulErosion(Player, 2); // 魂蚀
                    ACMScreenShakeSystem.Add(5f);
                    if (Player.whoAmI == Main.myPlayer)
                        CombatText.NewText(Player.Hitbox, TelegraphColors.Execution, "勾魂!");
                }
                SoulHookCaster = -1;
            }
        }
    }
    public class NiuTou : ModNPC
    {
        //声音资源引用
        private static readonly SoundStyle RoarSound = SoundID.Roar with { PitchVariance = .2f };
        private static readonly SoundStyle ChargeWindupSound = SoundID.ForceRoar with { Volume = .8f, PitchVariance = .3f };
        private static readonly SoundStyle ChainLaunchSound = SoundID.Item20 with { Volume = .7f };
        private static readonly SoundStyle EyeBlastSound = SoundID.Item74 with { Volume = 1f };
        private static readonly SoundStyle ComboDashSound = SoundID.DD2_EtherianPortalDryadTouch with { Volume = .9f };

        public override void SetDefaults() {
            NPC.width = 70;
            NPC.height = 70;
            NPC.lifeMax = 10000;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0;
            NPC.damage = 10;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.defense = 13;
            NPC.chaseable = false;
            NPC.rarity = 2;
            NPC.scale = 2.4f;

            base.SetDefaults();
        }
        public Player player => Main.player[NPC.target];
        public NiuMaPlayer ScreenPla => player?.GetModPlayer<NiuMaPlayer>();
        public int NPC_MaMian_Count = 0;
        private NPC NPC_MaMian => Main.npc[NPC_MaMian_Count];

        // —— 协同阶段「勾魂锁命连携」: 牛头压线 (车道冲锋), 供马面读取以填充慢球 ——
        public int SynergyPhase;      // 0=铺垫 1=预告 2=冲锋 3=收招
        public Vector2 LaneStart, LaneEnd;
        private int synergyLocal;     // 本轮车道内计时
        private float synergyLaneY;   // 本轮车道高度 (玩家进入时锁定)

        // —— 冲锋预告 (每招可读) ——
        private bool chargeTelegraph;
        private Vector2 chargeAim = Vector2.UnitX;

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            NiuMaLoot.AddBossLoot(npcLoot, ModContent.NPCType<MaMian>());
        }

        private float Draw_Alpha = 1;
        private bool Draw_Tail = false;
        public override bool PreDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            //强化视觉表现: 添加渐隐尾焰与发光
            var tex = TextureAssets.Npc[Type].Value;
            var rec = NPC.frame;
            var spe = NPC.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            if (Draw_Tail) {
                var Tailcol = Color.DarkRed * .5f;
                Tailcol.A = 0;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    sb.Draw(tex, NPC.oldPos[i] + rec.Size() * .5f * NPC.scale - scrPos, rec, Tailcol * Draw_Alpha, NPC.rotation, rec.Size() * .5f, NPC.scale * (1f - i / (float)NPC.oldPos.Length * .3f), spe, 0);
                }
            }
            sb.Draw(tex, NPC.Center - scrPos, rec, col * Draw_Alpha, NPC.rotation, rec.Size() * .5f, NPC.scale, spe, 0);
            //外发光
            var glowCol = Color.DarkRed; glowCol.A = 0;
            sb.Draw(tex, NPC.Center - scrPos, rec, glowCol * .4f * Draw_Alpha, NPC.rotation, rec.Size() * .5f, NPC.scale * 1.08f, spe, 0);

            if (!Main.dedServ)
                DrawTelegraphs();
            return false;
        }

        /// <summary>冲锋/连携车道的硬化 API 预告 (幽紫蓄势 → 命中前转纯红致命)。</summary>
        private void DrawTelegraphs() {
            // 普通冲锋瞄准线 (Ai_0 蓄势期)。
            if (chargeTelegraph) {
                Vector2 end = NPC.Center + chargeAim * 1600f;
                ACMShaders.DrawBeam(NPC.Center, end, 14f, TelegraphColors.NetherViolet, new Color(80, 20, 30), 0.55f, 1.4f, 2f, 2.2f);
            }
            // 连携车道: 预告(幽紫) → 末段/冲锋(致命红) + 冲锋命中泛光。
            if (SynergyPhase == 1 || SynergyPhase == 2) {
                bool lethal = SynergyPhase == 2 || synergyLocal >= 92;
                Color core = lethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
                float w = lethal ? 38f : 18f;
                ACMShaders.DrawBeam(LaneStart, LaneEnd, w, core, new Color(90, 20, 30), lethal ? 1f : 0.6f);
                if (SynergyPhase == 2)
                    ACMShaders.DrawRadialBloomAt(NPC.Center, 0.16f, 0.7f, TelegraphColors.Lethal, 8f, 2.6f);
            }
        }
        public void ReSet() {
            for (int i = 0; i <= 2; i++) {
                NPC.ai[i] = 0;
            }
        }
        private float NPCai(int c) {
            return NPC.ai[c];
        }
        public override bool PreAI() {
            Draw_Tail = false;
            chargeTelegraph = false;
            return base.PreAI();
        }
        private void Ai_0(float timeLeft, int dam) {
            //冲锋阶段+抛出血雾
            NPC.ai[0]++;
            var Proj_t = ModContent.ProjectileType<Proj_756_Adjust>();

            if (NPCai(0) < 50) {
                NPC.rotation = 0;
                if (NPCai(0) == 1) SoundEngine.PlaySound(ChargeWindupSound, NPC.Center);//预备声音
            }
            else if (NPCai(0) < 110) {
                Draw_Alpha = MathHelper.Lerp(Draw_Alpha, 0, .06f);
            }
            else if (NPCai(0) < 140) {
                if (NPCai(0) == 110) {
                    NPC.direction = NiuMaHelper.Rand_Int(-1, 1, 0);
                    NPC.Center = player.Center + new Vector2(600, 0) * -NPC.direction;
                    SoundEngine.PlaySound(RoarSound, NPC.Center);//出现轰鸣
                    ScreenPla?.SetZoom(1.8f);
                }
                Draw_Alpha = MathHelper.Lerp(Draw_Alpha, 1, .1f);
                // 冲锋瞄准预告 (每招可读): 蓄势期画瞄准线, 末段转红。
                if (NPCai(0) >= 116) {
                    chargeTelegraph = true;
                    chargeAim = (player.Center - NPC.Center).NormalizeVector(Vector2.UnitX);
                }
            }
            else if (NPCai(0) < 175) {
                Draw_Tail = true;
                if (NPCai(0) == 140) {
                    for (int i = 0; i < 5; i++) {
                        var d = Dust.NewDustPerfect(NPC.Center, ModContent.DustType<Dust_1>());
                        d.color = Color.DarkRed * .5f;
                        d.color.A = 255;
                        d.scale *= 2.6f;

                        d.velocity = new Vector2(NiuMaHelper.Rand_Float(2, 6)).RotatedByRandom(8);
                    }
                    NPC.velocity = (player.Center - NPC.Center).NormalizeVector() * 2;
                    ScreenPla?.SetScreenShake(5, 15);
                }
                if (NPCai(0) % 3 == 0 && NPC.velocity.Length() > 9) {
                    for (int i = 0; i < 3; i++) {
                        var vel = new Vector2(NPC.direction, -1).RotatedByRandom(1);
                        var pos = Vector2.Lerp(NPC.oldPos[4] + new Vector2(35) * NPC.scale, NPC.Center, NiuMaHelper.Rand_Float(1));
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), pos, vel, Proj_t, dam, 1);
                        p.ai[0] = -timeLeft * 60;
                        p.ai[1] = NiuMaHelper.Rand_Float(.5f, 1f);
                        p.friendly = false;
                        p.hostile = true;
                    }
                }
                if (NPC.velocity.Length() < 26) NPC.velocity *= 1.2f;
            }
            else if (NPCai(0) < 256) {
                if (NPCai(0) % 3 == 0 && NPC.velocity.Length() > 12) {
                    for (int i = 0; i < 3; i++) {
                        var vel = new Vector2(NPC.direction, -1).RotatedByRandom(1);
                        var pos = Vector2.Lerp(NPC.oldPos[4] + new Vector2(35) * NPC.scale, NPC.Center, NiuMaHelper.Rand_Float(1));
                        var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), pos, vel, Proj_t, dam, 1);
                        p.ai[0] = -timeLeft * 60;
                        p.ai[1] = NiuMaHelper.Rand_Float(.5f, 1f);
                        p.friendly = false;
                        p.hostile = true;
                    }
                }
                NPC.velocity *= .95f;
                if (NPCai(0) == 255)
                    NPC.direction *= -1;
            }
            else {
                ReSet();
                NPC.ai[3]++;
            }
            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * .06f, -.5f, .5f), .07f);

        }
        private Vector2 Ai1_ToCreatChainProj;
        private void Ai_1(int next, int ChainNum = 3) {
            //锁链牵引阶段
            NPC.ai[0]++;
            var dis = Vector2.Distance(NPC.Center, player.Center);
            var ForStep = ((ChainNum - 1) / 2);
            if (NPCai(0) < 70) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, (player.Center + new Vector2(-500 * NPC.direction, -100) - NPC.Center).NormalizeVector() * 10 * Math.Clamp(dis * .03f, 0, 1), .07f);
                if (NPCai(0) == 1) SoundEngine.PlaySound(ChargeWindupSound, NPC.Center);//蓄力提示
            }
            else {
                NPC.velocity *= .0f;
            }
            if (NPCai(0) < 60) {
                Ai1_ToCreatChainProj = (player.Center - NPC.Center).NormalizeVector() * 75;

                for (int j = -ForStep; j <= ForStep; j++) {
                    for (float i = 0; i < NPCai(0); i++) {
                        if (j == 0) {
                            var v = i / NPCai(0);
                            var pos = NPC.Center + (Ai1_ToCreatChainProj.NormalizeVector()).RotatedBy(j * .5f) * v * Math.Min(Math.Abs(j) * 1800 + (player.Center - NPC.Center).Length(), 1800);
                            Dust.NewDustPerfect(pos, DustID.CrimsonTorch).noGravity = true;
                        }
                        else {
                            for (float k = 0; k < 1; k += .3f) {
                                var v = i / NPCai(0);
                                v += k;
                                var pos = NPC.Center + (Ai1_ToCreatChainProj.NormalizeVector()).RotatedBy(j * .5f) * v * Math.Min(Math.Abs(j) * 1800 + (player.Center - NPC.Center).Length(), 1800);
                                Dust.NewDustPerfect(pos, DustID.CrimsonTorch).noGravity = true;

                            }
                        }
                    }
                }
            }
            if (NPCai(0) == 70) {
                var v = Ai1_ToCreatChainProj;
                for (int i = -ForStep; i <= ForStep; i++) {
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center, v.RotatedBy(i * .5f), ModContent.ProjectileType<ChainProj>(), 1, 0);
                    p.ai[2] = NPC.whoAmI;
                }
                SoundEngine.PlaySound(ChainLaunchSound, NPC.Center);//发射锁链音效
                ScreenPla?.SetScreenShake(6, 12);
            }
            if (NPCai(0) > 116) {
                ReSet();
                NPC.ai[3] = next;
            }

        }
        private Vector2 Ai3_ToVector;
        private void Ai_2() {
            //高空凝视蓄能阶段
            var ty = ModContent.DustType<Dust_1>();
            Ai3_ToVector = player.Center + new Vector2(0, -340);
            NPC.ai[0]++;

            if (NPCai(0) < 240) {
                var dis = (NPC.Center - Ai3_ToVector).Length();
                NPC.velocity = Vector2.Lerp(NPC.velocity, (Ai3_ToVector - NPC.Center).NormalizeVector() * 12 * Math.Clamp(dis * .03f, 0, 1), .07f);
            }
            else {
                ReSet();
                //若两者均进入半血则进入组合阶段 (牛头为指挥, 同步拉马面进协同)
                if (NPC.life < NPC.lifeMax * .5f && NPC_MaMian.active && NPC_MaMian.life < NPC_MaMian.lifeMax * .5f) {
                    NPC.ai[3] = 3;
                    SynergyPhase = 0;
                    synergyLocal = 0;
                    if (NPC_MaMian.ModNPC is MaMian mm) {
                        mm.ReSet();
                        NPC_MaMian.ai[3] = 3;
                    }
                }
                else NPC.ai[3] = 0;
            }
            if (NPCai(0) <= 155 && NPCai(0) >= 50) {
                if (NPCai(0) % 35 == 0) {

                    for (int i = 0; i < 20; i++) {
                        var _CreatPos = NPC.Center + new Vector2(130).RotatedByRandom(8);
                        var d = Dust.NewDustPerfect(_CreatPos, ty);
                        d.color = Color.DarkRed * .2f;
                        d.color.A = 255;
                        d.scale *= 2.6f;

                        d.velocity = (NPC.Center - d.position).NormalizeVector() * 8;

                    }
                }
            }
            if (NPCai(0) == 190) {
                for (int i = 0; i < 10; i++) {
                    var v = new Vector2(NiuMaHelper.Rand_Float(5, 8)).RotatedByRandom(8);
                    for (int j = 0; j < 4; j++) {
                        var d = Dust.NewDustPerfect(NPC.Center, ty);
                        d.color = Color.DarkRed;
                        d.color.A = 255;
                        d.scale *= 2;
                        d.alpha /= 3;
                        d.velocity = v;
                    }
                }
                SoundEngine.PlaySound(EyeBlastSound, NPC.Center);//眼束释放
                Projectile.NewProjectileDirect(NPC.GetSource_FromAI(), NPC.Center, new Vector2(6).RotatedByRandom(8), ModContent.ProjectileType<EyeProj>(), 1, 0, player.whoAmI);
                ScreenPla.SetScreenShake(8, 10);
            }
        }
        private const int SynergyCycle = 200;   // 单条车道的完整周期
        private const int SynergyCycles = 2;     // 协同阶段重复几次车道
        private void Ai_3() {
            //协同阶段「勾魂锁命连携」: 牛头压一条可读车道 (铺垫→预告→冲锋→收招),
            //马面在收招/铺垫的空档填充缓慢灵魂球 —— 学配合, 而非拼 DPS。
            NPC.ai[0]++;
            int total = (int)NPC.ai[0];
            int cycleIndex = total / SynergyCycle;
            synergyLocal = total % SynergyCycle;

            if (cycleIndex >= SynergyCycles) {
                ReSet();
                NPC.ai[3] = 0;        // 循环回常规 (马面读到牛头离开协同后自行退出)
                SynergyPhase = 0;
                NPC.damage = 10;
                ScreenPla?.SetZoom(1.4f);
                return;
            }

            bool chargeFromLeft = cycleIndex % 2 == 0;

            if (synergyLocal < 50) {
                // —— 铺垫: 锁定车道高度, 牛头移动到起点侧 ——
                SynergyPhase = 0;
                NPC.damage = 10;
                if (synergyLocal == 0) {
                    synergyLaneY = player.Center.Y;
                    SoundEngine.PlaySound(RoarSound, NPC.Center);
                    ScreenPla?.SetZoom(2.2f);
                }
                float laneHalf = 1500f;
                LaneStart = new Vector2(player.Center.X - laneHalf, synergyLaneY);
                LaneEnd = new Vector2(player.Center.X + laneHalf, synergyLaneY);
                Vector2 startPos = chargeFromLeft ? LaneStart : LaneEnd;
                startPos.X += chargeFromLeft ? 200f : -200f; // 留一点起跑距离
                NPC.velocity = Vector2.Lerp(NPC.velocity, (startPos - NPC.Center) * 0.08f, 0.1f);
                NPC.direction = chargeFromLeft ? 1 : -1;
            }
            else if (synergyLocal < 110) {
                // —— 预告: 牛头蓄势, 车道亮起 (幽紫→末段红, 见 DrawTelegraphs) ——
                SynergyPhase = 1;
                NPC.damage = 10;
                NPC.velocity *= 0.85f;
                if (synergyLocal == 92)
                    SoundEngine.PlaySound(ChargeWindupSound, NPC.Center);
            }
            else if (synergyLocal < 140) {
                // —— 冲锋: 沿车道横扫 (致命接触) ——
                SynergyPhase = 2;
                NPC.damage = 160;
                Draw_Tail = true;
                if (synergyLocal == 110) {
                    NPC.velocity = new Vector2(chargeFromLeft ? 38f : -38f, 0);
                    SoundEngine.PlaySound(ComboDashSound, NPC.Center);
                    ScreenPla?.SetScreenShake(6, 10);
                    ACMScreenShakeSystem.Add(8f);
                }
                if (NPC.velocity.Length() < 42)
                    NPC.velocity *= 1.04f;
                NPC.Center = new Vector2(NPC.Center.X, MathHelper.Lerp(NPC.Center.Y, synergyLaneY, 0.3f));
            }
            else {
                // —— 收招: 减速, 给马面填球的空档 ——
                SynergyPhase = 3;
                NPC.damage = 10;
                NPC.velocity *= 0.9f;
            }
            NPC.rotation = NPC.rotation.AngleLerp(Math.Clamp(NPC.velocity.X * .08f, -.6f, .6f), .1f);
        }
        public override void AI() {
            Lighting.AddLight(NPC.Center, Color.DarkRed.ToVector3());

            // 地府氛围染屏 (ElementalScreenTint, 不占全屏后处理名额): 半血/协同更浓。
            float tint = 0.22f;
            if (NPC.life < NPC.lifeMax * .5f) tint += 0.14f;
            if (NPC.ai[3] == 3) tint += 0.1f;
            NiuMaScreenSystem.Publish(NPC.Center, tint);

            NPC.damage = 10; // 基线接触伤害; 连携冲锋帧由 Ai_3 临时提升后自动回落。

            if (player == null || NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest();
            }
            if (NPC.ai[3] < 0) {
                var scr = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
                if (NPC.ai[3] == -1) {
                    scr.SetScreenPos(NPC.Center + new Vector2(0, -100));
                    scr.SetZoom(1.4f);
                    NPC.ai[0]++;
                    if (NPC.ai[0] < 60) {
                        NPC.velocity = new Vector2(0, -2);
                    }
                    else {
                        NPC.velocity *= .9f;
                        for (int i = 0; i < 7; i++) {
                            var d = Dust.NewDustDirect(NPC.position, NPC.width, 10, DustID.GemDiamond);
                            d.noGravity = true;
                            d.velocity = new Vector2(0, -20).RotatedByRandom(1);
                        }
                    }
                    if (NPC.ai[0] > 240) {
                        ReSet();
                        NPC.ai[3] = 0;
                        NPC.dontTakeDamage = false;
                    }
                    Draw_Alpha = MathHelper.Lerp(Draw_Alpha, 1, .09f);
                }
                else if (NPC.ai[3] == -2)//复活演出
                {
                    NPC.Center = NPC_MaMian.Center + new Vector2(0, -250);
                    if (NPC_MaMian.ai[0] > 60)
                        Draw_Alpha = MathHelper.Lerp(Draw_Alpha, 1, .02f);
                    if (NPC_MaMian.ai[0] > 230) {
                        ReSet();
                        NPC.ai[3] = 0;
                        NPC.dontTakeDamage = false;
                    }

                }
                NPC.rotation = NPC.rotation.AngleLerp(0, .08f);
            }
            else {
                if (player != null) {
                    if (NPC.life > (NPC.lifeMax * .5f)) {
                        if (NPC.ai[3] == 0) {
                            Ai_0(5, 200);
                        }
                        else if (NPC.ai[3] == 1) {
                            Ai_1(0);
                        }
                    }
                    else {
                        // 半血: 不再 500 伤 / 7 链的纯数值升级; 改为新规则 (可读冲锋预告 + 凝视 + 协同连携)。
                        if (NPC.ai[3] == 0) {
                            Ai_0(8, 260);
                        }
                        else if (NPC.ai[3] == 1) {
                            Ai_1(2, 3);
                        }
                        else if (NPC.ai[3] == 2) {
                            Ai_2();
                        }
                        else if (NPC.ai[3] == 3) {
                            Ai_3();
                        }
                    }
                }
                else {
                    NPC.velocity *= .9f;
                }
            }
            base.AI();
        }
        private bool HasRespawn = false;
        public override bool CheckDead() {
            if (!HasRespawn && NPC_MaMian.life > NPC_MaMian.lifeMax * .3f) {
                HasRespawn = true;
                Draw_Alpha = 0;
                NPC.dontTakeDamage = true;   // 重生体淡入期无敌 (淡入完成由 ai[3]==-2 解除)
                NPC.ai[3] = -2;
                NPC.velocity *= 0;

                // 引魂同伴 (马面) 默认无敌; 仅当玩家站入尸位光圈才可被打断 (反制窗口)。
                NPC_MaMian.dontTakeDamage = true;
                NPC_MaMian.velocity *= 0;
                NPC_MaMian.ai[3] = -1;
                ReSet();
                (NPC_MaMian.ModNPC as MaMian).ReSet();
                NPC.life = (int)(NPC.lifeMax * .5f);
                SoundEngine.PlaySound(RoarSound, NPC.Center);//复活提示

                // 在阵亡者尸位生成反制光圈 (站圈内 -> 引魂者可被打断)。
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<NiuMaRevivalCircle>(), 0, 0f, Main.myPlayer, NPC_MaMian.whoAmI);
                return false;
            }
            return base.CheckDead();
        }

    }
    public class MaMian : ModNPC
    {
        //声音资源
        private static readonly SoundStyle VolleySound = SoundID.Item73 with { Volume = .8f };
        private static readonly SoundStyle SoulPullSound = SoundID.DD2_MonkStaffGroundImpact with { Volume = .9f };
        private static readonly SoundStyle BoomChargeSound = SoundID.Item74 with { Volume = 1f };

        public override void SetDefaults() {
            NPC.width = 70;
            NPC.height = 70;
            NPC.lifeMax = 10000;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0;
            NPC.damage = 10;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.netAlways = true;
            NPC.defense = 13;
            NPC.chaseable = false;
            NPC.rarity = 2;
            NPC.scale = 2.4f;
            base.SetDefaults();
        }
        private float Draw_Alpha = 1;
        private bool Draw_Tail = false;

        public override bool PreDraw(SpriteBatch sb, Vector2 scrPos, Color col) {
            //减速领域地纹 (硬化 API ArenaRunic): 持续/站位危险 = 低饱和脉动圈, 非红。
            if (!Main.dedServ)
                DrawDomainDecal();

            //强化视觉表现: 紫色尾焰+发光
            var tex = TextureAssets.Npc[Type].Value;
            var rec = NPC.frame;
            var spe = NPC.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            if (Draw_Tail) {
                var Tailcol = Color.Purple * .5f;
                Tailcol.A = 0;
                for (float i = 0; i < NPC.oldPos.Length; i++) {
                    var a = 1 - i / NPC.oldPos.Length * .4f;
                    sb.Draw(tex, NPC.oldPos[(int)i] + rec.Size() * .5f * NPC.scale - scrPos, rec, Tailcol * Draw_Alpha * a, NPC.rotation, rec.Size() * .5f, NPC.scale, spe, 0);
                }
            }
            sb.Draw(tex, NPC.Center - scrPos, rec, col * Draw_Alpha, NPC.rotation, rec.Size() * .5f, NPC.scale, spe, 0);
            var glowCol = Color.Purple; glowCol.A = 0;
            sb.Draw(tex, NPC.Center - scrPos, rec, glowCol * .45f * Draw_Alpha, NPC.rotation, rec.Size() * .5f, NPC.scale * 1.08f, spe, 0);

            //勾魂牵引索 (硬化 API DrawBeam): 标记中 = 幽紫 → 近到期渐红。
            if (!Main.dedServ)
                DrawSoulHookTether();
            return false;
        }

        private void DrawDomainDecal() {
            float inten = (float)NPC.localAI[1];
            if (inten <= 0.12f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;
            float worldR = DomainRadius * inten;
            ACMShaders.WorldDecalParams(NPC.Center, worldR, out Vector2 uv, out float radiusFrac, out float aspect);
            Color primary = TelegraphColors.NetherViolet;
            Color secondary = TelegraphColors.GhostGreen;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(inten * 0.55f, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
        }

        private void DrawSoulHookTether() {
            var p = Main.player[NPC.target];
            if (p == null || !p.active || p.dead)
                return;
            var np = p.GetModPlayer<NiuMaPlayer>();
            if (np.SoulHookTimer <= 0)
                return;
            float frac = 1f - np.SoulHookTimer / 180f; // 越近到期越亮
            Color core = Color.Lerp(TelegraphColors.NetherViolet, TelegraphColors.Execution, frac * 0.55f);
            ACMShaders.DrawBeam(NPC.Center, p.Center, 6f, core, new Color(60, 30, 110), 0.45f + 0.5f * frac, 1.6f);
        }
        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            NiuMaLoot.AddBossLoot(npcLoot, ModContent.NPCType<NiuTou>());
        }

        public Player player => Main.player[NPC.target];
        public int NPC_NiuTou_Count = 0;
        private NPC NPC_NiuTou => Main.npc[NPC_NiuTou_Count];
        private float DomainRadius = 600f;   // 当前减速领域半径 (供地纹绘制)
        private bool HasRespawn = false;
        public override bool CheckDead() {
            if (!HasRespawn && NPC_NiuTou.life > NPC_NiuTou.lifeMax * .3f) {
                HasRespawn = true;
                Draw_Alpha = 0;
                NPC.dontTakeDamage = true;   // 重生体淡入期无敌
                NPC.ai[3] = -2;
                NPC.velocity *= 0;

                // 引魂同伴 (牛头) 默认无敌; 仅当玩家站入尸位光圈才可被打断 (反制窗口)。
                NPC_NiuTou.dontTakeDamage = true;
                NPC_NiuTou.velocity *= 0;
                NPC_NiuTou.ai[3] = -1;
                ReSet();
                (NPC_NiuTou.ModNPC as NiuTou).ReSet();
                NPC.life = (int)(NPC.lifeMax * .5f);
                SoundEngine.PlaySound(SoulPullSound, NPC.Center);//复活音效

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<NiuMaRevivalCircle>(), 0, 0f, Main.myPlayer, NPC_NiuTou.whoAmI);
                return false;
            }
            return base.CheckDead();
        }
        public void ReSet() {
            for (int i = 0; i <= 2; i++) {
                NPC.ai[i] = 0;
            }
        }
        private Vector2 ToVec;
        private bool StartMove = false;
        public override void OnSpawn(IEntitySource source) {
            ToVec = NPC.Center;
            base.OnSpawn(source);
        }
        private void Ai0(int num, int dam, int Diverge = 1, int next = 0) {
            //多次弹幕齐射阶段
            NPC.ai[0]++;
            if (NPC.ai[1] < num) {
                if (NPC.ai[0] > 40) {
                    if (NPC.ai[0] % 40 == 0) {
                        NPC.ai[1]++;
                        for (int i = 0; i < Diverge; i++) {
                            var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center, (player.Center - NPC.Center).NormalizeVector().RotatedByRandom(2) * 5, ModContent.ProjectileType<DarkGreenProj>(), dam, 2);
                            p.ai[2] = NPC.whoAmI;
                        }
                        SoundEngine.PlaySound(VolleySound, NPC.Center);//齐射音效
                    }
                }
            }
            else {
                NPC.ai[1]++;
                if (NPC.ai[1] > 240) {
                    ReSet();
                    NPC.ai[3] = next;
                }
            }

        }
        private void Ai_Const_0(double TimeDis, int timeLength, double R) {
            //持续领域减速/半血加强
            DomainRadius = (float)R;
            if (++NPC.localAI[0] > TimeDis) {
                NPC.localAI[1] = MathHelper.Lerp(NPC.localAI[1], 1, .05f);

                if (TimeDis > 0)
                    if (NPC.localAI[0] > TimeDis + timeLength) {
                        NPC.localAI[0] = 0;
                    }
                foreach (var p in Main.player) {
                    if (p != null)
                        if (p.active && !p.dead) {
                            if (p.Distance(NPC.Center) < R * NPC.localAI[1]) {
                                if (NPC.life > NPC.lifeMax * .5f) {
                                    p.AddBuff(ModContent.BuffType<DeclineSpeedBuff_1>(), 6);
                                }
                                else {
                                    p.AddBuff(ModContent.BuffType<DeclineSpeedBuff_2>(), 6);
                                    if (NPC.localAI[1] > .6f && NPC.ai[0] % 30 == 0) SoundEngine.PlaySound(SoulPullSound, NPC.Center);//半血灵魂牵引脉冲
                                }
                                // 魂蚀: 久站灵魂领域被缓慢侵蚀 (地府身份层)。
                                if (NPC.localAI[1] > .5f && NPC.ai[0] % 45 == 0)
                                    UnderworldField.AddSoulErosion(p, 1);
                            }
                        }
                }
            }
            else {
                NPC.localAI[1] = MathHelper.Lerp(NPC.localAI[1], 0, .05f);
            }
            if (NPC.localAI[1] > .12)
                for (float j = 0; j < 1; j += 1 / 4f) {
                    for (float i = 0; i < 1; i += .2f) {
                        var dust = Dust.NewDustPerfect(NPC.Center + new Vector2(0, (float)R * NPC.localAI[1]).RotatedBy(j * MathHelper.TwoPi + i * .2f + Main.timeForVisualEffects * .09), DustID.CorruptTorch);
                        dust.noGravity = true;
                    }
                }
        }
        private void Ai2() {
            //大范围爆裂蓄能
            NPC.ai[0]++;
            if (NPC.ai[0] < 120) {
                var v = NPC.ai[0] / 120f;
                var p = new Vector2(0, -100).RotatedBy(v * MathHelper.TwoPi * 2) * (1f - v);
                Dust.NewDustPerfect(p, DustID.DemonTorch).noGravity = true;
            }
            if (NPC.ai[0] == 140) {
                SoundEngine.PlaySound(BoomChargeSound, NPC.Center);//爆裂蓄能音效
                for (int i = -1; i <= 1; i++)
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(0, -5).RotatedBy(i), ModContent.ProjectileType<DarkGreenBoomProj>(), 1500, 2, player.whoAmI);
            }
            if (NPC.ai[0] > 200) {
                ReSet();
                NPC.ai[3] = 0;
            }
        }
        private void Ai3_Synergy() {
            //协同阶段「勾魂锁命连携」马面侧: 读牛头车道相位, 在其收招/铺垫空档填充缓慢灵魂球,
            //牛头压线 (预告/冲锋) 时静默 —— 与牛头交替, 逼玩家学配合 (非拼 DPS)。
            NPC.ai[0]++;
            Draw_Tail = true;

            NPC niu = NPC_NiuTou;
            // 牛头不在场 / 已离开协同 -> 马面同步退出。
            if (niu == null || !niu.active || niu.ai[3] != 3) {
                ReSet();
                NPC.ai[3] = 0;
                return;
            }
            if (NPC.ai[0] == 1)
                SoundEngine.PlaySound(SoulPullSound, NPC.Center);

            // 悬于上方, 避开牛头横扫车道。
            NPC.velocity = Vector2.Lerp(NPC.velocity, (player.Center + new Vector2(0, -360) - NPC.Center).NormalizeVector() * 8, .05f);

            int phase = niu.ModNPC is NiuTou nt ? nt.SynergyPhase : 1;
            // 仅在牛头铺垫(0)/收招(3)空档释放慢球; 牛头压线(1/2)时不抢读屏。
            if ((phase == 0 || phase == 3) && NPC.ai[0] % 26 == 0) {
                SoundEngine.PlaySound(VolleySound, NPC.Center);
                for (int i = -2; i <= 2; i++) {
                    var dir = (player.Center - NPC.Center).NormalizeVector().RotatedBy(i * 0.22f);
                    var p = Projectile.NewProjectileDirect(NPC.GetSource_FromThis(), NPC.Center, dir * 3.4f, ModContent.ProjectileType<SoulOrbProj>(), 240, 2);
                    p.ai[2] = NPC.whoAmI;
                }
            }
        }
        public override void AI() {
            Lighting.AddLight(NPC.Center, Color.Purple.ToVector3());

            // 地府氛围染屏 (与牛头共用, 取 max): 半血/协同更浓。
            float tint = 0.22f;
            if (NPC.life < NPC.lifeMax * .5f) tint += 0.14f;
            if (NPC.ai[3] == 3) tint += 0.1f;
            NiuMaScreenSystem.Publish(NPC.Center, tint);

            if (player == null || NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest();
            }
            if (NPC.ai[3] < 0) {
                var scr = Main.LocalPlayer.GetModPlayer<NiuMaPlayer>();
                if (NPC.ai[3] == -1) {
                    scr.SetScreenPos(NPC.Center + new Vector2(0, -100));
                    scr.SetZoom(1.4f);
                    NPC.ai[0]++;
                    if (NPC.ai[0] < 60) {
                        NPC.velocity = new Vector2(0, -2);
                    }
                    else {
                        NPC.velocity *= .9f;
                        for (int i = 0; i < 7; i++) {
                            var d = Dust.NewDustDirect(NPC.position, NPC.width, 10, DustID.GemDiamond);
                            d.noGravity = true;
                            d.velocity = new Vector2(0, -20).RotatedByRandom(1);
                        }
                    }
                    if (NPC.ai[0] > 240) {
                        ReSet();
                        NPC.ai[3] = 0;
                        NPC.dontTakeDamage = false;
                    }
                    Draw_Alpha = MathHelper.Lerp(Draw_Alpha, 1, .09f);
                }
                else if (NPC.ai[3] == -2)//复活演出
                {
                    NPC.Center = NPC_NiuTou.Center + new Vector2(0, -250);
                    if (NPC_NiuTou.ai[0] > 60)
                        Draw_Alpha = MathHelper.Lerp(Draw_Alpha, 1, .02f);
                    if (NPC_NiuTou.ai[0] > 230) {
                        ReSet();
                        NPC.ai[3] = 0;
                        NPC.dontTakeDamage = false;
                    }

                }
                NPC.rotation = NPC.rotation.AngleLerp(0, .08f);
            }
            else {
                if (player != null) {

                    if (NPC.life > NPC.lifeMax * .5f) {
                        if (NPC.ai[3] == 0) {
                            Ai0(1, 300, 4, 0);
                        }
                        Ai_Const_0(15 * 60, 20 * 60, 600);

                    }
                    else {
                        Ai_Const_0(-1, 114514, 550);

                        if (NPC.ai[3] == 0) {
                            //半血: 不再翻倍齐射; 维持单波 + 新机制「勾魂」。
                            Ai0(1, 300, 4, 2);
                        }
                        else if (NPC.ai[3] == 2) {
                            Ai2();
                        }
                        else if (NPC.ai[3] == 3) {
                            Ai3_Synergy();
                        }

                        //勾魂 (控制者新机制): 非协同阶段周期性标记玩家; 协同进入由牛头指挥, 此处不再自行触发。
                        if (NPC.ai[3] != 3) {
                            NPC.localAI[2] -= 1f;
                            if (NPC.localAI[2] <= 0f) {
                                NPC.localAI[2] = 360f;
                                player?.GetModPlayer<NiuMaPlayer>()?.ApplySoulHook(NPC.whoAmI);
                                SoundEngine.PlaySound(SoulPullSound, NPC.Center);
                            }
                        }
                    }
                    if (StartMove) {
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (ToVec - NPC.Center).NormalizeVector() * 50, 0.02f);
                        if (NPC.Distance(ToVec) < 100)
                            StartMove = false;
                    }
                    else {
                        if (player.Distance(NPC.Center) > 900) {
                            if (!StartMove) {
                                ToVec = player.Center + new Vector2(0, -NiuMaHelper.Rand_Float(400, 600)).RotatedByRandom(1);
                                ToVec.X += player.velocity.X * 50;
                                StartMove = true;
                            }
                        }
                    }
                    var dis = (NPC.Center - ToVec).Length();
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (ToVec - NPC.Center).NormalizeVector() * 12 * Math.Clamp(dis * .03f, 0, 1), .07f);
                    NPC.direction = NPC.velocity.X > 0 ? 1 : -1;
                    Draw_Tail = true;
                }
                else {
                    NPC.velocity *= .9f;
                }
            }
            base.AI();
        }
        public override bool PreAI() {
            Draw_Tail = false;
            return base.PreAI();
        }

    }
}