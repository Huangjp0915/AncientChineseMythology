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
    //  环绕重锤（自 Jiangcen.cs 拆出并重做）：将臣的"仪仗 + 武器"
    //  ai[0]=Boss whoAmI, ai[1]=序号(0..5), ai[2]=状态, ai[3]=状态计时
    //  状态: 0公转 / 1受命蓄力 / 2径向猛砸 / 3嵌驻+拔回 / 4收拢护体
    //        5边界就位 / 6弦线冲刺(前段=预告) / 7失能坠落 / 8坠地熄灭
    // ============================================================
    internal class JiangcenHammer : ModNPC
    {
        private const int SlamTime = 30;    //猛砸飞行
        private const int EmbedTime = 14;   //砸到尽头的嵌驻停顿(重量感)
        private const int ReturnTime = 46;
        private const int ChordAimTime = 36; //弦线冲刺前的预告持锤
        private const float OrbitRadius = 150f;

        private int ChargeTime => BossJc != null && BossJc.InPhase2 ? 70 : 90;

        private NPC BossNPC => Main.npc[(int)NPC.ai[0]];
        private Jiangcen BossJc => BossNPC.ModNPC as Jiangcen;

        private ref float State => ref NPC.ai[2];
        private ref float Timer => ref NPC.ai[3];
        private int Index => (int)NPC.ai[1];

        //弦线冲刺方向(预告期锁定, 各端从同步位置确定性推得)
        private Vector2 chordDir = Vector2.UnitX;
        //死亡坠落的本地延迟计时
        private int fallDelay = -1;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            NPC.width = 76;
            NPC.height = 76;
            NPC.damage = 0;
            NPC.defense = 20;
            NPC.lifeMax = 60000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCHit4;
            NPC.value = 0f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
        }

        public override bool CheckActive() => false;

        public override void AI() {
            NPC boss = BossNPC;
            if (!boss.Alives() || boss.ModNPC is not Jiangcen jc) {
                //本体已真死: 电火花中熄灭
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 10; i++) {
                        int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0, 0, 100, default, 1.6f);
                        Main.dust[d].noGravity = true;
                    }
                }
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }
            NPC.realLife = boss.whoAmI;
            NPC.target = boss.target;

            //本体进入死亡演出: 六锤按序失能坠落
            if (jc.Phase == Jiangcen.BossPhase.Death && State < 7) {
                if (fallDelay < 0)
                    fallDelay = Index * 10;
                if (--fallDelay <= 0) {
                    State = 7;
                    Timer = 0;
                    NPC.velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -2f);
                }
                else {
                    //断电抖动
                    NPC.velocity *= 0.9f;
                    NPC.rotation += Main.rand.NextFloat(-0.05f, 0.05f);
                    return;
                }
            }

            float orbitAngle = jc.HammerOrbit * 0.6f + MathHelper.TwoPi / 6f * Index;
            float orbitR = State == 4 ? 74f : OrbitRadius;
            Vector2 orbitPos = boss.Center + orbitAngle.ToRotationVector2() * orbitR;
            Timer++;

            switch ((int)State) {
                case 0: //公转(待命): 弹簧追踪轨道位 → 有惯性滞后的重量感
                    NPC.damage = 0;
                    NPC.Center = Vector2.Lerp(NPC.Center, orbitPos, 0.22f);
                    NPC.velocity = Vector2.Zero;
                    NPC.rotation = SlerpAngle(NPC.rotation, boss.AngleTo(NPC.Center), 0.25f);
                    break;

                case 1: { //受命蓄力: 悬停变红, 末 14 帧向本体反拉(late-snap 反向蓄势)
                    NPC.damage = 0;
                    Vector2 dir = (orbitPos - boss.Center).SafeNormalize(Vector2.UnitX);
                    float reelT = MathHelper.Clamp((Timer - (ChargeTime - 14)) / 14f, 0f, 1f);
                    float reel = MathF.Pow(reelT, 3f) * 88f;
                    //蓄力细颤(能量攒不住的感觉), 随进度加剧
                    float chargeT = MathHelper.Clamp(Timer / ChargeTime, 0f, 1f);
                    Vector2 shiver = Main.rand.NextVector2Circular(1f, 1f) * (1.5f * chargeT);
                    NPC.Center = orbitPos - dir * reel + shiver;
                    NPC.velocity = Vector2.Zero;
                    NPC.rotation = dir.ToRotation();

                    if (!VaultUtils.isServer && Timer % 4 == 0) {
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.RedTorch, 0, 0, 100, default, 1.6f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = Main.rand.NextVector2Circular(2, 2);
                    }
                    if (Timer >= ChargeTime) {
                        State = 2;
                        Timer = 0;
                        NPC.velocity = dir * (Main.expertMode ? 44f : 37f);
                        NPC.rotation = dir.ToRotation();
                        SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        JiangcenThunderPrisonSystem.Pulse(NPC.Center, 0.6f, TelegraphColors.Lethal);
                        NPC.netUpdate = true;
                    }
                    break;
                }

                case 2: //径向猛砸(长矩形扫掠命中区)
                    NPC.damage = jc.GetBossDamage(1.3f);
                    NPC.velocity *= 0.985f;
                    if (!VaultUtils.isServer) {
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, 0, 0, 100, Color.DarkRed, 2f);
                        Main.dust[d].noGravity = true;
                    }
                    if (Timer >= SlamTime) {
                        State = 3;
                        Timer = 0;
                        NPC.damage = 0;
                        NPC.velocity = Vector2.Zero;
                        //嵌驻瞬间: 闷响 + 火花(砸到了"尽头")
                        SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.6f, Volume = 0.9f }, NPC.Center);
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 10; i++) {
                                int d = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 100, default, 1.4f);
                                Main.dust[d].noGravity = true;
                                Main.dust[d].velocity = Main.rand.NextVector2Circular(5, 5);
                            }
                        }
                        NPC.netUpdate = true;
                    }
                    break;

                case 3: //嵌驻停顿 → 拔回轨道(先顿后拔, 重量感)
                    NPC.damage = 0;
                    if (Timer <= EmbedTime) {
                        NPC.velocity = Vector2.Zero;
                        NPC.rotation += Main.rand.NextFloat(-0.02f, 0.02f); //余震微颤
                    }
                    else {
                        NPC.Center = Vector2.Lerp(NPC.Center, orbitPos, 0.13f);
                        NPC.rotation = SlerpAngle(NPC.rotation, boss.AngleTo(NPC.Center), 0.15f);
                        if (Timer >= EmbedTime + ReturnTime || Vector2.Distance(NPC.Center, orbitPos) < 24f) {
                            State = 0;
                            Timer = 0;
                        }
                    }
                    break;

                case 4: //收拢护体(换阶段/大招演出): 贴身快转
                    NPC.damage = 0;
                    NPC.Center = Vector2.Lerp(NPC.Center, orbitPos, 0.3f);
                    NPC.velocity = Vector2.Zero;
                    NPC.rotation = boss.AngleTo(NPC.Center);
                    break;

                case 5: { //边界就位(六锤连狱): 飞往雷牢边缘自己的位置
                    NPC.damage = 0;
                    float slotAng = jc.ChordBaseAngle + MathHelper.TwoPi / 6f * Index;
                    Vector2 slot = jc.ArenaCenter + slotAng.ToRotationVector2() * jc.PrisonRadius;
                    NPC.Center = Vector2.Lerp(NPC.Center, slot, 0.14f);
                    NPC.velocity = Vector2.Zero;
                    //指向弦线方向(即将冲刺的路径)
                    NPC.rotation = SlerpAngle(NPC.rotation, ComputeChordDir(jc).ToRotation(), 0.2f);
                    break;
                }

                case 6: { //弦线冲刺: 前 ChordAimTime 帧持锤预告(反拉), 然后 1 帧点火对穿雷牢
                    if (Timer <= ChordAimTime) {
                        NPC.damage = 0;
                        chordDir = ComputeChordDir(jc);
                        float reelT = MathHelper.Clamp((Timer - (ChordAimTime - 12)) / 12f, 0f, 1f);
                        float slotAng = jc.ChordBaseAngle + MathHelper.TwoPi / 6f * Index;
                        Vector2 slot = jc.ArenaCenter + slotAng.ToRotationVector2() * jc.PrisonRadius;
                        NPC.Center = slot - chordDir * MathF.Pow(reelT, 3f) * 80f;
                        NPC.velocity = Vector2.Zero;
                        NPC.rotation = chordDir.ToRotation();
                        if (Timer == ChordAimTime) {
                            NPC.velocity = chordDir * (Main.expertMode ? 42f : 36f);
                            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.15f, Volume = 1.1f }, NPC.Center);
                            ACMScreenShakeSystem.Add(6f);
                            NPC.netUpdate = true;
                        }
                    }
                    else {
                        NPC.damage = jc.GetBossDamage(1.25f);
                        //对穿到雷牢另一侧后转入嵌驻拔回
                        if (Vector2.Distance(NPC.Center, jc.ArenaCenter) > jc.PrisonRadius + 40f && Timer > ChordAimTime + 16) {
                            State = 3;
                            Timer = 0;
                            NPC.damage = 0;
                            NPC.velocity = Vector2.Zero;
                            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
                            NPC.netUpdate = true;
                        }
                        //保底出口
                        if (Timer > ChordAimTime + 90) {
                            State = 3;
                            Timer = 0;
                            NPC.damage = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    break;
                }

                case 7: //失能坠落(死亡演出): 断电重锤自由落体
                    NPC.damage = 0;
                    NPC.velocity.X *= 0.98f;
                    NPC.velocity.Y = Math.Min(NPC.velocity.Y + 0.5f, 14f);
                    NPC.rotation += NPC.velocity.Y * 0.006f * (Index % 2 == 0 ? 1 : -1);
                    if (Terraria.Collision.SolidCollision(NPC.position + NPC.velocity, NPC.width, NPC.height) || Timer > 180) {
                        State = 8;
                        Timer = 0;
                        NPC.velocity = Vector2.Zero;
                        SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.8f, Volume = 1.1f }, NPC.Center);
                        ACMScreenShakeSystem.Add(3.5f);
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 14; i++) {
                                int d = Dust.NewDust(NPC.Bottom - new Vector2(NPC.width / 2, 6), NPC.width, 10, DustID.Smoke, 0, -1f, 130, default, 1.6f);
                                Main.dust[d].noGravity = false;
                            }
                        }
                        NPC.netUpdate = true;
                    }
                    break;

                default: //8 坠地熄灭: 静静躺着, 偶尔迸出残余火花
                    NPC.damage = 0;
                    NPC.velocity = Vector2.Zero;
                    if (!VaultUtils.isServer && Main.rand.NextBool(30)) {
                        int d = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 120, default, 1.1f);
                        Main.dust[d].noGravity = true;
                    }
                    break;
            }
        }

        //角度插值(走最短弧)
        private static float SlerpAngle(float from, float to, float amount)
            => from + MathHelper.WrapAngle(to - from) * amount;

        //弦线方向: 从自己的边界槽位指向对侧偏转 ±50° 的落点 → 六条弦交织穿过牢心区域
        private Vector2 ComputeChordDir(Jiangcen jc) {
            float slotAng = jc.ChordBaseAngle + MathHelper.TwoPi / 6f * Index;
            float destAng = slotAng + MathHelper.Pi + MathHelper.ToRadians(Index % 2 == 0 ? -50f : 50f);
            Vector2 from = jc.ArenaCenter + slotAng.ToRotationVector2() * jc.PrisonRadius;
            Vector2 to = jc.ArenaCenter + destAng.ToRotationVector2() * jc.PrisonRadius;
            return (to - from).SafeNormalize(Vector2.UnitX);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();

            DrawSlamTelegraph();
            DrawChordTelegraph();

            //蓄力期间逐渐变红预告 / 猛砸中炽红 / 坠地熄灭发黑
            Color tint = drawColor;
            int state = (int)NPC.ai[2];
            if (state == 1) {
                float t = MathHelper.Clamp(NPC.ai[3] / ChargeTime, 0, 1);
                float flash = 0.5f + 0.5f * (float)Math.Sin(NPC.ai[3] * (0.2f + t * 0.4f));
                tint = Color.Lerp(drawColor, new Color(255, 40, 40) * (0.7f + 0.3f * flash), 0.4f + 0.6f * t);
            }
            else if (state == 2 || (state == 6 && NPC.ai[3] > ChordAimTime)) {
                tint = Color.Lerp(drawColor, new Color(255, 70, 60), 0.7f);
            }
            else if (state >= 7) {
                tint = Color.Lerp(drawColor, new Color(40, 38, 45), 0.75f);
            }

            //速度门控残影(只有真的快才配拖影)
            if (NPC.velocity.LengthSquared() > 120f) {
                float sengs = 0.24f;
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                    spriteBatch.Draw(mainValue, drawOldPos, rectangle, tint * sengs
                        , NPC.oldRot[i] + MathHelper.PiOver2, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                    sengs *= 0.8f;
                }
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, tint
                , NPC.rotation + MathHelper.PiOver2, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);

            //雷狱阶段锤体附电(状态 7/8 断电不再附)
            if (state < 7 && BossJc != null && BossJc.InPhase2 && MythologyConfig.Trail != TrailQualityLevel.Off) {
                JiangcenVFX.DrawBodyArcs(spriteBatch, NPC.Center, 46f, 0.5f, NPC.whoAmI);
            }
            return false;
        }

        // ===== 猛砸径向预告线: 蓄力期间沿"对穿走廊"渐强的致命红线, 让径向猛砸可读 =====
        private void DrawSlamTelegraph() {
            if (Main.dedServ || NPC.ai[2] != 1)
                return;
            NPC boss = BossNPC;
            if (!boss.Alives())
                return;

            float t = MathHelper.Clamp(NPC.ai[3] / ChargeTime, 0f, 1f);
            Vector2 dir = (NPC.Center - boss.Center).SafeNormalize(Vector2.UnitX);
            Vector2 start = NPC.Center;
            Vector2 end = NPC.Center + dir * 1100f;

            float intensity = 0.15f + 0.7f * t;
            float w = 4f + 12f * t;
            ACMShaders.DrawBeam(start, end, w,
                TelegraphColors.Lethal, new Color(120, 10, 15, 0), intensity, 1.6f, 2.0f);
        }

        // ===== 弦线冲刺预告: 持锤期沿整条弦的致命红线(贯穿雷牢) =====
        private void DrawChordTelegraph() {
            if (Main.dedServ || NPC.ai[2] != 6 || NPC.ai[3] > ChordAimTime)
                return;
            Jiangcen jc = BossJc;
            if (jc == null)
                return;

            float t = MathHelper.Clamp(NPC.ai[3] / ChordAimTime, 0f, 1f);
            Vector2 dir = ComputeChordDir(jc);
            Vector2 end = NPC.Center + dir * jc.PrisonRadius * 2f;
            ACMShaders.DrawBeam(NPC.Center, end, 4f + 10f * t,
                TelegraphColors.Lethal, new Color(120, 10, 15, 0), 0.2f + 0.65f * t, 1.6f, 2.0f);
        }
    }
}
