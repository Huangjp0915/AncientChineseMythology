using AncientChineseMythology.NPCs.Boss.Hanbas;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using static AncientChineseMythology.Projectiles.RuyiStickSpearProjectile_3;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    [AutoloadBossHead]
    internal class Yingou : ModNPC
    {
        public int seed = -1;
        public static int ReelBackTime => Main.masterMode ? 50 : 60;
        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 110;
            NPC.height = 110;
            NPC.damage = 66;
            NPC.defense = 60;
            NPC.lifeMax = 420000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCHit1;
            NPC.value = 20000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.defense = 40;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Yingou");
        }


        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
        }
        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
        }

        public Random random = null;
        public bool spawnHands = true;
        public enum AttackAIStyle
        {
            Idle,
            Melee,
            Wave,
            Circle
        }
        public int aichange = 0;
        public AttackAIStyle aitype = AttackAIStyle.Melee;
        public float circleCounter = 0;
        public float circlespeed = 0;
        internal ref int swordDir => ref otherAI[3];
        private readonly int[] otherAI = new int[aiSlot];
        private const int aiSlot = 4;
        public override void AI() {
            if (spawnHands) {
                spawnHands = false;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, 1);
                    NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<YingouHand>(), 0, NPC.whoAmI, -1);
                }
            }

            if (!VaultUtils.isServer && !SkyManager.Instance[YingouSky.name].IsActive()) {
                SkyManager.Instance.Activate(YingouSky.name);
            }

            if (swordDir == 0) {
                swordDir = 1;
            }

            random ??= new Random(seed);

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.Alives()) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.Alives()) {
                    NPC.ai[0] = -1;
                    NPC.ai[1] = 0f;
                    NPC.ai[2] = 0f;
                }
            }

            float angend = MathHelper.Lerp(0, MathHelper.TwoPi, NPC.localAI[0]) + Main.rand.NextFloat(-0.1f, 0.1f);
            //更自然的出生偏移角度（非对称 + 扰动）
            Vector2 spawnOffset = Vector2.UnitY.RotatedBy(angend) * 300f;
            Vector2 destination = target.Center + spawnOffset;

            ref float generalTimer = ref NPC.ai[2];
            ref float attackTimer = ref NPC.ai[1];
            ref float state = ref NPC.ai[0];

            switch (state) {
                //他妈砍砍砍宫崎英高小故事之小时候被爸爸拿着两把杀猪刀追着屁股砍
                case 0:
                    if (aitype == AttackAIStyle.Circle) {
                        circleCounter += circlespeed * 0.25f; //降低旋转速率，防止鬼畜
                        float radiusPulse = 1f + 0.05f * (float)Math.Sin(Main.GameUpdateCount * 0.05f);
                        if (aichange < 2.5f * 60) {
                            circlespeed = MathHelper.Clamp(circlespeed + 0.006f, 0, 1.2f);
                            circlespeed *= 0.985f;
                        }
                        else {
                            circlespeed *= 0.99f;
                        }

                        //环绕粒子演出
                        if (Main.rand.NextBool(3)) {
                            Dust.NewDustPerfect(NPC.Center, DustID.FireworkFountain_Yellow,
                                Vector2.UnitX.RotatedByRandom(MathHelper.TwoPi) * Main.rand.NextFloat(0.5f, 1.5f),
                                150, default, 1.2f).noGravity = true;
                        }
                    }

                    if (!NPC.HasValidTarget) {
                        NPC.target = NPC.FindClosestPlayer();
                    }

                    if (NPC.HasValidTarget) {
                        NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 0.34f;
                        NPC.velocity *= 0.98f;
                        aichange++;

                        //状态切换
                        if (aitype == AttackAIStyle.Idle) {
                            aitype = AttackAIStyle.Melee;
                            aichange = 0;
                        }
                        if (aitype == AttackAIStyle.Melee && aichange > 4 * 60) {
                            aitype = AttackAIStyle.Wave;
                            aichange = 0;
                        }
                        if (aitype == AttackAIStyle.Wave && aichange > 8 * 60) {
                            aitype = AttackAIStyle.Circle;
                            aichange = 0;
                            circleCounter = 0;
                            circlespeed = 0;
                        }
                        if (aitype == AttackAIStyle.Circle && aichange > 4 * 60) {
                            aitype = AttackAIStyle.Melee;
                            aichange = 0;
                            state = 1f;
                            if (++otherAI[2] > 1) {
                                otherAI[2] = 0;
                                state = 5f;
                            }
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                //靠近预热
                case 1f:
                    //加一点颤动感
                    Vector2 jitter = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f));
                    NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(destination) * 16 + jitter, 0.1f);

                    if (NPC.WithinRange(destination, NPC.velocity.Length() * 1.65f)) {
                        NPC.velocity = NPC.SafeDirectionTo(target.Center) * -7f;
                        state = 2f;
                        attackTimer = 0f;
                        otherAI[0]++;
                        NPC.netUpdate = true;
                    }
                    break;
                
                //蓄力准备
                case 2f:
                    NPC.velocity *= 0.975f;
                    attackTimer++;

                    //充能光效
                    for (int i = 0; i < 6; i++) {
                        Vector2 offset = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.Next(NPC.width);
                        Dust.NewDustPerfect(NPC.Center + offset, DustID.GoldFlame, Vector2.Zero, 150, default, 1.8f).noGravity = true;
                    }

                    if (attackTimer == ReelBackTime / 2) {
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        if (!VaultUtils.isClient) {
                            //炫酷版发射
                            int fireballCount = 8;
                            float spread = MathHelper.ToRadians(60); //总散射角
                            float baseAngle = NPC.DirectionTo(target.Center).ToRotation();

                            for (int i = 0; i < fireballCount; i++) {
                                //计算每发的角度
                                float angleOffset = MathHelper.Lerp(-spread / 2, spread / 2, i / (float)(fireballCount - 1));

                                //基础速度带点随机
                                float speed = Main.rand.NextFloat(8f, 12f);
                                Vector2 velocity = baseAngle.ToRotationVector2().RotatedBy(angleOffset) * speed;

                                //粒子特效：发射闪光
                                if (!VaultUtils.isServer) {
                                    for (int d = 0; d < 8; d++) {
                                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.PurpleTorch, velocity.X, velocity.Y, 150, default, 2f);
                                        Main.dust[dust].noGravity = true;
                                        Main.dust[dust].velocity *= 1.5f;
                                    }
                                }

                                //分帧延迟发射，让视觉上像“扇形展开”
                                float delay = i * 0.6f; //每发间隔3帧
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity,
                                        ModContent.ProjectileType<YingouFireBall>(), GetBossDamage(), 2f, Main.myPlayer, 0, 0, delay);
                            }
                            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 15); //屏幕震动
                        }
                    }

                    if (attackTimer >= ReelBackTime) {
                        float dashAngleOffset = Main.rand.NextFloat(-0.12f, 0.12f);
                        Vector2 dashDir = NPC.SafeDirectionTo(target.Center).RotatedBy(dashAngleOffset);
                        NPC.velocity = dashDir * 16;

                        NPC.oldPos = new Vector2[NPC.oldPos.Length];
                        state = 3f;
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;

                //冲刺阶段
                case 3f:
                    NPC.knockBackResist = 0f;
                    NPC.damage = 95;

                    if (attackTimer == 0) {
                        swordDir *= -1;
                        SoundEngine.PlaySound(SoundID.Roar, NPC.Center, s => { s.Position = NPC.Center; return true; });
                        aitype = AttackAIStyle.Circle;
                        aichange = 0;
                        circleCounter = 0;
                        circlespeed = 0;
                    }
                    attackTimer++;

                    if (attackTimer > 60f || NPC.collideX || NPC.collideY) {
                        NPC.velocity = -Vector2.UnitY.RotatedByRandom(0.6f) * 3f;
                        state = 4f;
                        attackTimer = 0f;
                        aitype = AttackAIStyle.Idle;
                        NPC.netUpdate = true;
                    }
                    break;

                //停顿等待阶段
                case 4f:
                    NPC.velocity *= 0.9f;
                    attackTimer++;

                    if (attackTimer == 20f) {
                        aitype = AttackAIStyle.Melee;
                    }

                    if (attackTimer > 20f) {
                        if (!VaultUtils.isClient) {
                            NPC.localAI[0] = Main.rand.NextFloat();
                            NPC.netUpdate = true;
                        }

                        state = 1f;
                        if (otherAI[0] > 4) {
                            otherAI[0] = 0;
                            state = 0f;
                        }
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;

                //大刀地狱
                case 5f:
                    aitype = AttackAIStyle.Idle;
                    attackTimer++;

                    Vector2 hoverTargetPos = target.Center + new Vector2(0, -300);

                    if ((attackTimer < 120f || !NPC.WithinRange(hoverTargetPos, NPC.velocity.Length() * 1.65f)) && otherAI[1] == 0) {
                        NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hoverTargetPos) * 36, 0.1f);
                    }
                    else {
                        if (otherAI[1] == 0) {
                            attackTimer = 0;
                        }

                        otherAI[1] = 1;
                        NPC.velocity *= 0.9f;

                        if (attackTimer % 60 == 10) {
                            SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                            if (!VaultUtils.isClient) {
                                Vector2 basePos = GetPlayerByRandOffest(target);
                                float baseAngle = 0f;
                                for (int i = -1; i <= 1; i++) {
                                    float angleOffset = i * 0.15f; //角度偏移，错开发射方向
                                    Vector2 dir = (basePos - NPC.Center).SafeNormalize(Vector2.UnitX).RotatedBy(baseAngle + angleOffset);
                                    Vector2 spawnPos = basePos + new Vector2(i * 100, 0);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, dir, ModContent.ProjectileType<SaberHell>(), GetBossDamage(), 2);
                                }
                            }
                        }

                        if (attackTimer % 60 == 25) {
                            SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                            if (!VaultUtils.isClient) {
                                Vector2 basePos = GetPlayerByRandOffest(target) + new Vector2(0, -350);
                                for (int i = -2; i <= 2; i++) {
                                    Vector2 offset = new Vector2(i * 80, 0);
                                    Vector2 pos = basePos + offset;
                                    Vector2 velocity = (basePos - pos).SafeNormalize(Vector2.UnitY);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, velocity, ModContent.ProjectileType<SaberHell>(), GetBossDamage(), 2);
                                }
                            }
                        }

                        if (attackTimer == 170 || attackTimer == 210) {
                            SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                            if (!VaultUtils.isClient) {
                                Vector2 basePos = GetPlayerByRandOffest(target);
                                Vector2[] angles = {
                                    MathHelper.PiOver4.ToRotationVector2(),
                                    (-MathHelper.PiOver4).ToRotationVector2(),
                                    (MathHelper.PiOver4 * 0.5f).ToRotationVector2(),
                                    (-MathHelper.PiOver4 * 0.5f).ToRotationVector2()
                                };

                                foreach (var dir in angles) {
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), basePos, dir, ModContent.ProjectileType<SaberHell>(), GetBossDamage(), 2);
                                }
                            }
                        }

                        if (attackTimer >= 270 && attackTimer <= 390 && attackTimer % 20 == 0) {
                            SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                            if (!VaultUtils.isClient) {
                                Vector2 basePos = GetPlayerByRandOffest(target);
                                int shotCount = 6;
                                for (int i = 0; i < shotCount; i++) {
                                    float angle = MathHelper.Lerp(-MathHelper.PiOver2 - 0.4f, -MathHelper.PiOver2 + 0.4f, i / (float)(shotCount - 1));
                                    Vector2 dir = angle.ToRotationVector2();
                                    Vector2 spawnPos = basePos + dir * 350;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, -dir, ModContent.ProjectileType<SaberHell>(), GetBossDamage(), 2);
                                }
                            }
                        }

                        if (attackTimer > 440) {
                            attackTimer = 0;
                            otherAI[1] = 0;
                            
                            if (++otherAI[0] > 1) {
                                state = 1f;
                                attackTimer = 0f;
                                break;
                            }

                            float dashAngleOffset = Main.rand.NextFloat(-0.12f, 0.12f);
                            Vector2 dashDir = NPC.SafeDirectionTo(target.Center).RotatedBy(dashAngleOffset);
                            NPC.velocity = dashDir * 16;

                            NPC.oldPos = new Vector2[NPC.oldPos.Length];
                            state = 6f;
                            attackTimer = 0f;
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                //过渡
                case 6f:
                    NPC.knockBackResist = 0f;
                    NPC.damage = 95;

                    if (attackTimer == 0) {
                        swordDir *= -1;
                        SoundEngine.PlaySound(SoundID.Roar, NPC.Center, s => { s.Position = NPC.Center; return true; });
                        aitype = AttackAIStyle.Circle;
                        aichange = 0;
                        circleCounter = 0;
                        circlespeed = 0;
                    }
                    attackTimer++;

                    if (attackTimer > 60f || NPC.collideX || NPC.collideY) {
                        NPC.velocity = -Vector2.UnitY.RotatedByRandom(0.6f) * 3f;
                        state = 5f;
                        attackTimer = 0f;
                        aitype = AttackAIStyle.Idle;
                        NPC.netUpdate = true;
                    }
                    break;
            }

            if (!NPC.HasValidTarget) {
                aitype = AttackAIStyle.Idle;
            }

            generalTimer++;

        }
        private static Vector2 GetPlayerByRandOffest(Player player) 
            => player.Center + new Vector2(Main.rand.Next(-300, 300), Main.rand.Next(-300, 300));
        private int GetBossDamage(float scaling = 1f, bool getOrigDamage = false) {
            int num = NPC.damage;
            if (getOrigDamage) {
                num = NPC.defDamage;
            }
            return (int)(num * scaling);
        }
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[NPC.type].Value;
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, null, drawColor * sengs
                    , 0, mainValue.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            Main.EntitySpriteDraw(mainValue, NPC.Center - Main.screenPosition, null, drawColor, NPC.rotation, mainValue.Size() / 2, NPC.scale, SpriteEffects.None);
            return false;
        }
    }

    internal class YingouHand : ModNPC
    {
        [VaultLoaden("AncientChineseMythology/NPCs/Boss/Yingous/")]
        private static Asset<Texture2D> SwordSlashTexture;
        public List<Vector2> oldPos = new List<Vector2>();
        public List<float> oldRots = new List<float>();
        public int attackCd = 0;
        public int noHomingTime = 0;
        public Player handPlayer = null;
        public int handPlayerTime = 0;
        public int handUp = 0;
        public int counter1 = 6;
        public int direction {
            get { return (int)NPC.ai[1]; }
            set { NPC.ai[1] = value; }
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.NPCBestiaryDrawModifiers nPCBestiaryDrawModifiers = new();
            nPCBestiaryDrawModifiers.Hide = true;
            NPCID.Sets.NPCBestiaryDrawModifiers value = nPCBestiaryDrawModifiers;
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = value;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 11;
        }

        public override void SetDefaults() {
            NPC.width = 76;
            NPC.height = 76;
            NPC.damage = 0;
            NPC.defense = 60;
            NPC.lifeMax = 60000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCHit1;
            NPC.value = 20000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.TrailCacheLength[Type] = 11;
        }

        public static Rectangle getRectCentered(Vector2 center, float w, float h) {
            return new Rectangle((int)(center.X - w / 2), (int)(center.Y - h / 2), (int)w, (int)h);
        }

        public static float getDistance(Vector2 v1, Vector2 v2) {
            return ((float)Math.Sqrt(Math.Pow(v2.X - v1.X, 2) + Math.Pow(v2.Y - v1.Y, 2)));
        }

        public override bool ModifyCollisionData(Rectangle victimHitbox, ref int immunityCooldownSlot, ref MultipliableFloat damageMultiplier, ref Rectangle npcHitbox) {
            npcHitbox = getRectCentered(NPC.Center + NPC.rotation.ToRotationVector2() * 64 * NPC.scale, NPC.width * NPC.scale, NPC.height * NPC.scale);
            return true;
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return handPlayerTime <= 0;
        }
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            if (handPlayerTime <= 0) {
                handPlayer = target;
                handPlayerTime = 8;
            }
        }
        public float circleDist = 0;
        public bool circle = false;
        public bool needSpawnRotProj = true;
        public float swingAngle;
        public float swingPhase;
        public override void AI() {
            if (counter1 > 0) {
                counter1--;
                return;
            }

            NPC owner = Main.npc[(int)NPC.ai[0]];
            Player target = Main.player[owner.target];

            if (!owner.Alives() || owner.ModNPC is not Yingou) {  //修正了Alives()为active，假设是笔误
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }

            NPC.realLife = owner.whoAmI;
            NPC.target = owner.target;
            Yingou modNpc = (Yingou)owner.ModNPC;

            //根据攻击类型设置减速系数
            NPC.velocity *= (modNpc.aitype == Yingou.AttackAIStyle.Melee) ? 0.94f : 0.98f;  //略微调整减速以增加挥舞的流畅感

            //修改后的近战攻击逻辑：从直线冲刺改为弧形挥舞攻击，模拟刀刃扫荡
            if (modNpc.aitype == Yingou.AttackAIStyle.Melee) {
                if (attackCd <= 0) {
                    //启动弧形挥舞：计算初始方向，然后在过程中添加旋转偏移
                    Vector2 directionToTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    NPC.velocity += directionToTarget * 36;  //初始冲刺速度降低，以允许弧形路径
                    noHomingTime = 45;  //延长无 homing 时间以完成挥舞
                    swingPhase = 0;  //新变量：挥舞阶段，用于控制弧形
                    swingAngle = 0f;  //新变量：当前挥舞角度
                    attackCd = modNpc.random.Next(120, 200);  //略微调整冷却以匹配新动画

                    SoundEngine.PlaySound(
                        SoundID.Item71 with {
                            Volume = 1.0f, //音量
                            PitchVariance = 0.1f //轻微音调变化
                        },
                        NPC.Center
                    );
                }
                else {
                    attackCd--;
                }

                //在挥舞过程中添加侧向速度以创建弧形路径
                if (noHomingTime > 0 && swingPhase == 0) {
                    swingAngle += MathHelper.ToRadians(4f);  //每帧增加角度，形成弧形
                    Vector2 perpendicular = NPC.velocity.RotatedBy(MathHelper.PiOver2);  //垂直于当前速度的方向
                    NPC.velocity += perpendicular.SafeNormalize(Vector2.Zero) * (float)Math.Sin(swingAngle) * 2f;  //添加正弦波偏移实现摆动
                    if (swingAngle >= MathHelper.Pi) {  //挥舞半圈后结束阶段
                        swingPhase = 1;

                        SoundEngine.PlaySound(
                        SoundID.Item71 with {
                            Volume = 0.8f,
                            Pitch = -0.2f
                        },
                        NPC.Center
                    );
                    }
                }
            }
            else {
                handPlayerTime = 0;
                swingPhase = 0;  //重置挥舞变量
                swingAngle = 0f;
            }

            //虚空光球攻击逻辑：保持原样，但略微调整以匹配整体节奏
            if (modNpc.aitype == Yingou.AttackAIStyle.Wave) {
                if (handUp >= 0) {
                    if (handUp == 0) {
                        NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 28;  //略减速度以区别
                        noHomingTime = 35;

                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.0f, PitchVariance = 0.1f }, NPC.Center);
                    }
                    handUp--;
                }
                if (attackCd <= 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.0f, PitchVariance = 0.1f }, NPC.Center);
                    handUp = 58;
                    attackCd = modNpc.random.Next(100, 180);
                }
                else {
                    attackCd--;
                }
            }
            else {
                handUp = 0;
            }

            circle = false;
            //修改后的环绕攻击逻辑：从简单环绕改为螺旋靠近，增加近战压迫感
            if (modNpc.aitype == Yingou.AttackAIStyle.Circle) {
                circle = true;
                if (needSpawnRotProj) {
                    needSpawnRotProj = false;
                }

                //螺旋效果：距离逐渐缩小，同时保持旋转
                circleDist = circleDist + (getDistance(owner.Center, target.Center) * 0.8f - circleDist) * 0.015f;  //调整系数，使其缓慢靠近玩家
                if (circleDist < 200) {  //最小距离，防止过于贴近
                    circleDist = 200;
                }
                modNpc.circleCounter += 0.05f;  //略增旋转速度以增强动态感

                if (Main.GameUpdateCount % 20 == 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, PitchVariance = 0.2f }, NPC.Center);
                }
            }
            else {
                circleDist = 100;
                needSpawnRotProj = true;
            }

            NPC.damage = owner.damage;
            NPC.scale = owner.scale;

            Vector2 targetPos = owner.Center + new Vector2(direction * 100 * NPC.scale, (handUp > 0 ? -80 : 0));
            if (modNpc.aitype == Yingou.AttackAIStyle.Circle) {
                //在螺旋中更新位置
                targetPos = owner.Center + new Vector2(circleDist * direction, 0).RotatedBy(modNpc.circleCounter * modNpc.swordDir);
                NPC.Center += (targetPos - NPC.Center) * 0.35f;  //略减平滑系数以增加挥舞感
            }
            else {
                if (handPlayerTime > 0) {
                    handPlayer.Center = NPC.Center + NPC.rotation.ToRotationVector2() * 86;
                    handPlayer.velocity *= 0;
                }
                if (noHomingTime > 0) {
                    noHomingTime--;
                }
                else {
                    NPC.Center += (targetPos - NPC.Center) * 0.22f;  //略调整以匹配新挥舞
                    if (handPlayerTime > 0) {
                        handPlayerTime--;
                        if (handPlayerTime == 0) {
                            handPlayer.velocity = (owner.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 20f;
                        }
                    }
                }
            }

            if (getDistance(targetPos, NPC.Center) > 4600) {
                NPC.Center = targetPos;
            }

            NPC.rotation = (NPC.Center - owner.Center).ToRotation();

            oldPos.Add(NPC.Center);
            oldRots.Add(NPC.rotation);
            if (oldPos.Count > 24) {
                oldPos.RemoveAt(0);
                oldRots.RemoveAt(0);
            }
        }
        public override bool CheckActive() {
            return false;
        }
        public override void PostAI() {
            NPC owner = Main.npc[(int)NPC.ai[0]];
            if (!owner.active) {
                return;
            }

        }
        public float trailOffset = 0;
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            trailOffset += 0.06f;

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            //结束当前批次，开始一个新的加色混合绘制
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制三角带顶点方法，避免重复代码
            void DrawTrail(Texture2D texture, Color startColor, Color endColor) {
                List<ColoredVertex> vertices = new();

                int count = oldRots.Count;
                for (int i = 0; i < count; i++) {
                    float t = i / (float)count;
                    Color c = Color.Lerp(startColor * 0.01f, endColor, t) * 1f;
                    Vector2 basePos = oldPos[i] - Main.screenPosition;
                    Vector2 rotVec = oldRots[i].ToRotationVector2();
                    float scaleFactor = 1 - t;
                    float offset1 = 16 + 180 * NPC.scale * scaleFactor * 0.5f;
                    float offset2 = 16 + 180 * NPC.scale - 80 * NPC.scale * scaleFactor * 0.5f;

                    vertices.Add(new ColoredVertex(basePos + rotVec * offset1, new Vector3(t + trailOffset, 1, 1), c));
                    vertices.Add(new ColoredVertex(basePos + rotVec * offset2, new Vector3(t + trailOffset, 0, 1), c));
                }

                if (vertices.Count >= 3) {
                    gd.Textures[0] = texture;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }
            }

            //绘制金色到红色的轨迹
            DrawTrail(VaultAsset.placeholder2.Value, Color.Gold, Color.Red);
            //绘制白色轨迹
            DrawTrail(SwordSlashTexture.Value, Color.White, Color.White);

            //结束加色混合，恢复正常混合状态绘制NPC主体
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = direction > 0 ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            float rotation = NPC.rotation + (direction > 0 ? MathHelper.ToRadians(18) : MathHelper.ToRadians(-18 + 180));
            SpriteEffects effects = direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                float rot = NPC.oldRot[i] + (direction > 0 ? MathHelper.ToRadians(18) : MathHelper.ToRadians(-18 + 180));
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                Main.EntitySpriteDraw(tex, drawOldPos, null, Color.White * sengs, rot, origin, NPC.scale * (sengs + 0.8f), effects);
                sengs *= 0.98f;
            }

            Main.EntitySpriteDraw(tex, NPC.Center - Main.screenPosition, null, drawColor, rotation, origin, NPC.scale, effects);

            return false;
        }
    }

    internal class YingouFireBall : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 220;
            Projectile.tileCollide = false;
        }

        public static void KillAll() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<YingouFireBall>()) {
                    continue;
                }
                proj.Kill();
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    int dustType = Main.rand.NextBool(2) ? DustID.Torch : DustID.Shadowflame;
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                        dustType, Projectile.velocity.X / 2, Projectile.velocity.Y / 2, 150,
                        default, Main.rand.NextFloat(1f, 3.5f));
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.6f;
                }
            }

            Projectile.ai[0]++;

            //初期螺旋阶段
            if (Projectile.ai[0] < 80) {
                //在旋转基础上加入抖动偏移
                float jitter = (float)Math.Sin(Projectile.ai[0] * 0.3f) * 0.1f;
                Projectile.velocity = Projectile.velocity.RotatedBy((0.025f + jitter) * Projectile.ai[2]);
            }
            //停顿脉冲阶段
            else if (Projectile.ai[0] == 80) {
                Projectile.velocity *= 0.3f;
                if (!VaultUtils.isServer) {
                    //脉冲粒子
                    for (int i = 0; i < 30; i++) {
                        Vector2 offset = Main.rand.NextVector2Circular(1f, 1f) * 40f;
                        int dust = Dust.NewDust(Projectile.Center + offset, 0, 0,
                            DustID.PurpleTorch, 0f, 0f, 0, default, 2f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = offset.SafeNormalize(Vector2.Zero) * 4f;
                    }
                }
            }
            //追踪阶段
            else {
                Player player = Projectile.Center.FindClosestPlayer(3200, true);
                if (player != null) {
                    //速度周期性波动
                    float speedFactor = 1.2f + 0.3f * (float)Math.Sin(Projectile.ai[0] * 0.15f);
                    Vector2 targetSpeed = Projectile.SafeDirectionTo(player.Center) * Projectile.velocity.Length() * speedFactor;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetSpeed, 0.05f);
                }
            }
        }
    }

    internal class SaberHell : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.velocity = Projectile.velocity.UnitVector();
            if (Projectile.localAI[0] < 40) {
                if (Projectile.localAI[0] == 0) {
                    Projectile.localAI[1] = 30;
                }
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] == 40) {
                    int num = 1000;
                    int num2 = 36;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent()
                        , Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2
                        , ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack
                        , Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                    Projectile.velocity *= -1;
                    Projectile.NewProjectile(Projectile.FromObjectGetParent()
                        , Projectile.Center + Projectile.velocity * num, Projectile.velocity * -num2
                        , ModContent.ProjectileType<SaberKiller>(), Projectile.damage, Projectile.knockBack
                        , Main.myPlayer, Projectile.Center.X, Projectile.Center.Y);
                }
            }
            else {
                if (Projectile.localAI[1] > 0) {
                    Projectile.localAI[1]--;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D back = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            int width = 4400;
            int height = (int)(Projectile.localAI[0] * 3);
            float alpha = Projectile.localAI[1] / 60f;

            Rectangle rect = new Rectangle(-width / 2, -height / 2, width, height);
            Vector2 origin = new Vector2(rect.Width / 2, rect.Height / 2);
            Color drawColor = VaultUtils.MultiStepColorLerp(Projectile.localAI[0] / 40f, Color.Azure, Color.Red);
            Main.spriteBatch.Draw(back, drawPos, rect, drawColor with { A = 155 } * alpha
                , Projectile.velocity.ToRotation(), origin, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }

    internal class SaberKiller : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Yingous/YingouHand";
        public override void SetDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            Projectile.width = Projectile.height = 84;
            Projectile.tileCollide = false;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 360;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
        }
        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            if (targetPos.Distance(Projectile.Center) < 120) {
                Projectile.velocity /= 2f;
                if (Projectile.ai[2] == 0) {
                    SoundEngine.PlaySound(SoundID.Item89, targetPos);
                    for (int i = 0; i < 115; i++) {
                        Vector2 sparkPos = targetPos + Main.rand.NextVector2Circular(60, 60);
                        int dust = Dust.NewDust(sparkPos, 0, 0, DustID.Torch, 0, 0);
                        Main.dust[dust].velocity = Main.rand.NextVector2Circular(6, 6);
                        Main.dust[dust].velocity *= 1.5f;
                        Main.dust[dust].scale = Main.rand.NextFloat(1.2f, 3f);
                        Main.dust[dust].noGravity = true;
                    }
                }
                Projectile.ai[2] = 1f;
            }
            if (Projectile.ai[2] == 1f) {
                Projectile.alpha -= 5;
                if (Projectile.alpha <= 0f) {
                    Projectile.Kill();
                }
                Projectile.alpha = (int)MathHelper.Clamp(Projectile.alpha, 0, 255);
            }
        }
        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Color drawColor = Color.White * (Projectile.alpha / 255f);
            float sengs = 0.3f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                Main.spriteBatch.Draw(value, oldPos, null, drawColor * sengs
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
                sengs *= 0.9f;
            }
            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, drawColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class YingouSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.6f;
        private Color skyColor;
        internal static string name;
        public static void LoadInstance() {
            name = "AncientChineseMythology:YingouSky";
            SkyManager.Instance[name] = new YingouSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() {
            return active;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override Color OnTileColor(Color inColor) {
            return inColor * (1f - intensity);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //计算朝向Boss的拉扯抖动
            NPC boss = GetBoss();
            Vector2 pullShake = Vector2.Zero;
            if (boss != null) {
                pullShake = (boss.Center - Main.LocalPlayer.Center)
                    .SafeNormalize(Vector2.Zero) * (2f * intensity);
            }

            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)pullShake.X, (int)pullShake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);
        }

        public override void Update(GameTime gameTime) {
            NPC boss = GetBoss();
            if (boss != null) {
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 1600f, 0f, 1f); //越近越接近深紫与血红

                //渐变主色调
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(20, 10, 40),    //暗紫
                    new Color(10, 40, 40),    //冷蓝绿
                    new Color(120, 0, 0));    //深血红

                if (intensity < maxIntensity)
                    intensity += 0.01f;

                active = true;
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        //方便调用的获取Boss方法
        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Yingou>())
                    return npc;
            }
            return null;
        }
    }
}
