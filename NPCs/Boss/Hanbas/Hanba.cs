using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.Bosses;
using AncientChineseMythology.Systems;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hanbas
{
    [AutoloadBossHead]
    internal class Hanba : ModNPC
    {
        [VaultLoaden("AncientChineseMythology/NPCs/Boss/Hanbas/")]
        internal static Asset<Texture2D> Talisman = null;
        private int frame;
        private const int maxFrame = 4;
        public static int ReelBackTime => Main.masterMode ? 50 : 60;
        private static readonly List<Vector2> EyesOffset = [];
        private readonly int[] otherAI = new int[aiSlot];
        private const int aiSlot = 4;
        private Vector2 OrigRestrictionPos;
        internal bool HasTalisman;
        private int shownMilestone; //仅本地视觉里程碑计数(不联网)
        public override void Load() {
            EyesOffset.Add(new Vector2(0, -44));
            EyesOffset.Add(new Vector2(0, 50));
            EyesOffset.Add(new Vector2(34, 34));
            EyesOffset.Add(new Vector2(-46, -26));
            EyesOffset.Add(new Vector2(44, -26));
            EyesOffset.Add(new Vector2(-34, 34));
            EyesOffset.Add(new Vector2(-54, 12));
            EyesOffset.Add(new Vector2(54, 12));
        }
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = maxFrame;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.npcSlots = 14f;
            NPC.width = 140;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 60;
            NPC.value = Item.buyPrice(0, 50, 0, 0);
            NPC.lifeMax = 400000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Hanba");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 10, 20));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HanbaBook>()));
        }

        public override void OnKill() {
            DownedBossSystem.downedHanba = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            for (int i = 0; i < aiSlot; i++) {
                writer.Write(otherAI[i]);
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            for (int i = 0; i < aiSlot; i++) {
                otherAI[i] = reader.ReadInt32();
            }
        }

        public override bool CheckActive() {
            return false;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            if (HasTalisman) {
                return false;//有符纸时不绘制小血条
            }
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void AI() {
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

            if (generalTimer == 0) {
                if (!VaultUtils.isServer && !SkyManager.Instance[HanbaSky.name].IsActive()) {
                    SkyManager.Instance.Activate(HanbaSky.name);
                }
                if (!HasTalisman && !VaultUtils.isClient) {
                    HasTalisman = true;
                    NPC.NewNPCDirect(NPC.FromObjectGetParent(), NPC.Center
                        , ModContent.NPCType<Talisman>(), ai0: NPC.whoAmI, target: NPC.target);
                }
            }

            Lighting.AddLight(NPC.Center, Color.Orange.ToVector3() * NPC.scale);

            float hoverSpeed = 32f;

            NPC.damage = NPC.defDamage;

            NPC.dontTakeDamage = HasTalisman;

            //一次性 HP 里程碑演出节拍(纯本地视觉, 不改 FSM/不联网; 各客户端按同步血量各自触发一次)
            if (!HasTalisman && !Main.dedServ && NPC.lifeMax > 0) {
                float lifeFrac = NPC.life / (float)NPC.lifeMax;
                if (shownMilestone == 0 && lifeFrac <= 0.66f) {
                    shownMilestone = 1;
                    PhaseBeat();
                }
                else if (shownMilestone == 1 && lifeFrac <= 0.33f) {
                    shownMilestone = 2;
                    PhaseBeat();
                }
            }

            bool setNPCRot = true;

            switch (state) {
                //失去目标，脱战
                case -1f:
                    if (attackTimer == 0) {
                        HanbaLaser.AllVanish();
                        HanbaBigLaser.AllVanish();
                    }

                    NPC.velocity = new Vector2(0, 60);

                    attackTimer++;

                    if (attackTimer > 180) {
                        NPC.active = false;
                        NPC.netUpdate = true;
                    }

                    break;
                //靠近预热
                case 0f:
                    NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(destination) * hoverSpeed, 0.1f);

                    if (NPC.WithinRange(destination, NPC.velocity.Length() * 1.65f)) {
                        NPC.velocity = NPC.SafeDirectionTo(target.Center) * -7f;
                        state = 1f;
                        attackTimer = 0f;
                        otherAI[0]++;
                        NPC.netUpdate = true;
                    }
                    break;
                //蓄力准备
                case 1f:
                    NPC.velocity *= 0.975f;
                    attackTimer++;

                    if (attackTimer == ReelBackTime / 2) {
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        ACMUtils.AddScreenShake(3f);
                        if (!VaultUtils.isClient) {
                            for (int i = 0; i < 8; i++) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, NPC.Center.To(target.Center).RotatedByRandom(0.6f).UnitVector() * 10
                                        , ModContent.ProjectileType<HanbaFireBall>(), GetBossDamage(), 2f, Main.myPlayer);
                            }
                        }
                    }

                    if (attackTimer >= ReelBackTime) {
                        //冲刺方向扰动
                        float dashAngleOffset = Main.rand.NextFloat(-0.12f, 0.12f);
                        Vector2 dashDir = NPC.SafeDirectionTo(target.Center).RotatedBy(dashAngleOffset);
                        NPC.velocity = dashDir * hoverSpeed;

                        NPC.oldPos = new Vector2[NPC.oldPos.Length];
                        state = 2f;
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;
                //冲刺阶段
                case 2f:
                    NPC.knockBackResist = 0f;
                    NPC.damage = 95;
                    if (attackTimer == 0) {
                        SoundEngine.PlaySound(SoundID.Roar, NPC.Center, (ActiveSound soundInstance) => {
                            soundInstance.Position = NPC.Center;
                            return true;
                        });
                        ACMUtils.AddScreenShake(5f);
                    }
                    attackTimer++;

                    //冲刺失败后进入短暂思考状态
                    if (attackTimer > 60f || NPC.collideX || NPC.collideY) {
                        NPC.velocity = -Vector2.UnitY.RotatedByRandom(0.6f) * 3f;
                        state = 3f;
                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;
                //停顿等待阶段
                case 3f:
                    NPC.velocity *= 0.9f;
                    attackTimer++;

                    if (attackTimer > 20f) {
                        if (!VaultUtils.isClient) {
                            NPC.localAI[0] = Main.rand.NextFloat();
                            NPC.netUpdate = true;
                        }

                        state = 0f;
                        if (otherAI[0] > 4 && !HasTalisman) {//触发切换到下一阶段，需要打掉符纸
                            otherAI[0] = 0;
                            state = 4f;
                        }

                        attackTimer = 0f;
                        NPC.netUpdate = true;
                    }
                    break;
                //蝗虫过境
                case 4f:
                    attackTimer++;

                    Vector2 hoverTargetPos = target.Center + new Vector2(0, -300);

                    if ((attackTimer < 120f || !NPC.WithinRange(hoverTargetPos, NPC.velocity.Length() * 1.65f)) && otherAI[1] == 0) {
                        NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hoverTargetPos) * hoverSpeed, 0.1f);
                    }
                    else {
                        if (otherAI[1] == 0) {
                            attackTimer = 0;
                        }

                        otherAI[1] = 1;
                        NPC.velocity *= 0.9f;

                        //多阶段释放
                        if (attackTimer == 30) {
                            if (!VaultUtils.isClient) {
                                Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                                , ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f);

                                Projectile.NewProjectile(NPC.GetSource_FromAI(), GetPlayerByRandOffest(target), Vector2.Zero, ModContent.ProjectileType<LocustSet>(), NPC.damage, 2);
                            }
                        }

                        if (attackTimer == 150) {
                            if (!VaultUtils.isClient) {
                                Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                                , ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f);

                                //上下收束式攻击
                                Vector2 basePos = GetPlayerByRandOffest(target);
                                for (int i = -2; i <= 2; i++) {
                                    Vector2 offset = new Vector2(i * 150, -400);
                                    Vector2 pos = basePos + offset;
                                    Vector2 velocity = (basePos - pos).SafeNormalize(Vector2.UnitY);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, velocity, ModContent.ProjectileType<LocustSet>(), GetBossDamage(), 2);
                                }
                            }
                        }

                        //来吧你这狗种，我旱魃武神便要在此将你轰下口牙
                        if (attackTimer == 270) {
                            //斜角双发，模拟大范围扫荡
                            Vector2 basePos = GetPlayerByRandOffest(target);

                            Vector2[] angles = [
                                MathHelper.PiOver4.ToRotationVector2(),
                                    (-MathHelper.PiOver4).ToRotationVector2(),
                                    (MathHelper.PiOver4 * 0.5f).ToRotationVector2(),
                                    (-MathHelper.PiOver4 * 0.5f).ToRotationVector2()
                            ];

                            if (!VaultUtils.isClient) {
                                Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                                , ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f);

                                foreach (var dir in angles) {
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), basePos, dir, ModContent.ProjectileType<LocustSet>(), GetBossDamage(), 2);
                                }
                            }
                        }

                        if (attackTimer == 390) {
                            if (!VaultUtils.isClient) {
                                Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                                , ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f);

                                //最后一波蝗虫突击：他妈的一百匹力量大圆弧+追踪
                                Vector2 basePos = GetPlayerByRandOffest(target);
                                for (int i = 0; i < 8; i++) {
                                    float angle = MathHelper.Lerp(-MathHelper.PiOver2 - 0.5f, -MathHelper.PiOver2 + 0.5f, i / 7f);
                                    Vector2 dir = angle.ToRotationVector2();
                                    Vector2 spawnPos = basePos + dir * 400;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, -dir, ModContent.ProjectileType<LocustSet>(), GetBossDamage(), 2);
                                }
                            }
                        }

                        if (attackTimer > 520) {
                            attackTimer = 0;
                            otherAI[1] = 0;
                            otherAI[0]++; //循环使用该阶段
                            if (otherAI[0] > 2) {
                                otherAI[0] = 0;
                                state = 5f;//循环两次蝗虫过境后切换到下一阶段
                            }
                        }
                    }
                    break;
                //眼睛吐火球魅力时刻
                case 5f:
                    if (attackTimer <= 160) {//小于160的常规移动，而大于这个时间的则受惯性冲刺影响
                        NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(target.Center) * hoverSpeed / 4f, 0.1f);
                    }

                    attackTimer++;

                    if (attackTimer >= 60 && attackTimer < 150) {
                        NPC.velocity *= 0.8f;
                    }

                    //阶段1：蓄力
                    if (attackTimer == 80) {
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        if (!VaultUtils.isServer) {
                            foreach (var eyeOffset in EyesOffset) {
                                Vector2 eyePos = NPC.Center + eyeOffset;
                                for (int i = 0; i < 15; i++) {
                                    Dust.NewDustPerfect(eyePos, DustID.Torch, Main.rand.NextVector2Unit() * Main.rand.NextFloat(3), 150, Color.OrangeRed, 1.5f);
                                }
                            }
                        }
                    }

                    //阶段2：火球喷射
                    if (attackTimer == 120) {
                        NPC.velocity *= 0.5f;
                        SoundEngine.PlaySound(SoundID.Item20, NPC.Center);

                        foreach (var eyeOffset in EyesOffset) {
                            Vector2 shootPos = NPC.Center + eyeOffset;
                            Vector2 fireDir = eyeOffset.UnitVector();
                            if (!VaultUtils.isClient) {
                                for (int i = 0; i < 3; i++) {
                                    float speed = 10f + i * 2f;
                                    Vector2 velocity = fireDir.RotatedByRandom(MathHelper.ToRadians(4)) * speed;
                                    float rotPwoer = i + 1;
                                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), shootPos, velocity
                                        , ModContent.ProjectileType<HanbaFireBall>(), GetBossDamage(), 2f, Main.myPlayer, 0, 0, rotPwoer);

                                    Main.projectile[proj].scale = 1f + i * 0.25f;
                                }
                            }
                        }
                    }

                    //阶段3：瞬移 + 冲刺 + 重置
                    if (attackTimer == 140) {
                        TeleportNearTarget(target);//神出鬼没的魅力
                    }

                    if (attackTimer == 160) {
                        DashTowardTarget(target, hoverSpeed);//30万匹力量火车冲刺
                    }

                    if (attackTimer > 180) {
                        attackTimer = 0;
                        otherAI[0]++;
                        if (otherAI[0] > 6) {
                            otherAI[0] = 0;
                            state = 6f;
                        }
                    }

                    break;
                //鬼眼，开！三层鬼域，这座城市，我接管了
                case 6f:
                    if (attackTimer == 0) {
                        HanbaFireBall.KillAll();//干掉上阶段可能遗留的火球
                        OrigRestrictionPos = target.Center;
                        NPC.Center = OrigRestrictionPos;
                        NPC.velocity *= 0.5f;
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);//魔法瞬移声
                    }

                    if (attackTimer == 10) {
                        SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                        ACMUtils.AddScreenShake(9f); //鬼域牢笼降临 set-piece 节拍
                        if (!VaultUtils.isClient) {
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                            , ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f);
                        }
                    }

                    if (attackTimer == 90) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f }, NPC.Center);
                        if (!VaultUtils.isClient) {
                            foreach (var eyeOffset in EyesOffset) {
                                Vector2 eyePos = NPC.Center + eyeOffset;
                                int proj = Projectile.NewProjectile(NPC.FromObjectGetParent(), eyePos, eyeOffset.UnitVector()
                                    , ModContent.ProjectileType<HanbaLaser>(), GetBossDamage(), 2);
                                Main.projectile[proj].ai[0] = NPC.whoAmI;
                                if (Main.projectile[proj].ModProjectile is HanbaLaser laser) {
                                    laser.offsetData = eyeOffset;
                                }
                            }
                        }
                    }

                    attackTimer++;

                    if (attackTimer > 90) {
                        NPC.ChasingBehavior(target.Center, 6);
                        NPC.rotation += 0.02f;
                        setNPCRot = false;
                    }
                    else {
                        NPC.velocity *= 0.8f;
                    }

                    CarftRestriction();

                    if (attackTimer > 600) {
                        attackTimer = 0;
                        state = 7f;
                        NPC.rotation = MathHelper.WrapAngle(NPC.rotation);//角度归圆化，防止切换阶段时触发爱的魔力转圈圈
                        HanbaLaser.AllVanish();//消除生成的小眼激光
                    }
                    break;
                //性感淫叫然后喷射巨大黄金太阳柱，他妈的旱灾转动一百万匹力量终极无敌黄金剑
                case 7f:
                    if (attackTimer < 20) {
                        NPC.velocity *= 0.8f;
                    }

                    if (attackTimer == 20) {
                        SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                    }

                    if (attackTimer > 20 && attackTimer < 60 && attackTimer % 6 == 0 && !VaultUtils.isClient) {
                        Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<Shockwave>(), 0, 0);
                    }

                    if (attackTimer >= 90 && attackTimer < 120 && attackTimer % 6 == 0) {
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        ACMUtils.AddScreenShake(4f); //渐强蓄力轰鸣
                        if (!VaultUtils.isServer) {
                            Vector2 eyePos = NPC.Center;
                            for (int i = 0; i < 115; i++) {
                                Dust.NewDustPerfect(eyePos, DustID.Torch, Main.rand.NextVector2Unit() * Main.rand.NextFloat(13), 150, Color.OrangeRed, Main.rand.NextFloat(1.5f, 3f));
                            }
                        }
                    }

                    if (attackTimer == 130) {
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f }, NPC.Center);
                        ACMUtils.AddScreenShake(11f); //黄金太阳柱终极大招释放
                        if (!VaultUtils.isClient) {
                            int proj = Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                            , ModContent.ProjectileType<HanbaBigLaser>(), GetBossDamage(), 0, -1, NPC.whoAmI);
                            Main.projectile[proj].rotation = target.Center.To(NPC.Center).ToRotation();
                        }
                    }

                    if (attackTimer > 130 && attackTimer % 10 == 0) {
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        if (!VaultUtils.isClient) {
                            Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, Vector2.Zero
                            , ModContent.ProjectileType<Shockwave>(), 0, 0, -1, 0, 0.6f);
                        }
                    }

                    attackTimer++;

                    if (attackTimer > 600) {
                        attackTimer = 0f;
                        state = 4;
                        NPC.netUpdate = true;
                        HanbaBigLaser.AllVanish();
                    }

                    break;

            }

            generalTimer++;
            if (setNPCRot) {
                NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.velocity.X * 0.02f, 0.1f);
            }

            VaultUtils.ClockFrame(ref frame, 5, maxFrame - 1);
        }

        internal Vector2 GetOrigPos() => OrigRestrictionPos;

        internal void TalismanKill() {
            HasTalisman = false;
            NPC.ai[0] = 4f;
            NPC.ai[1] = 0f;
            otherAI[0] = 0;
            NPC.netUpdate = true;
        }

        private void CarftRestriction() {
            int size = 800;
            int hemdFemd = 800;
            for (int j = 0; j < 3; j++) {
                //生成两个粒子（上下）
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 localOffset = new Vector2(-1800, i * Main.rand.NextFloat(size, size + hemdFemd));
                    Vector2 rotatedOffset = localOffset;
                    Vector2 spanPos = OrigRestrictionPos + rotatedOffset;

                    Vector2 velocity = new Vector2(Main.rand.Next(32, 60), 0);

                    PRTLoader.NewParticle<LocustPRT>(spanPos, velocity);
                }
                //生成两个粒子（左右）
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 localOffset = new Vector2(i * Main.rand.NextFloat(size, size + hemdFemd), -1200);
                    Vector2 rotatedOffset = localOffset;
                    Vector2 spanPos = OrigRestrictionPos + rotatedOffset;

                    Vector2 velocity = new Vector2(0, Main.rand.Next(32, 60));

                    PRTLoader.NewParticle<LocustPRT>(spanPos, velocity);
                }
            }
        }

        private int GetBossDamage(float scaling = 1f, bool getOrigDamage = false) {
            int num = NPC.damage;
            if (getOrigDamage) {
                num = NPC.defDamage;
            }
            return (int)(num * scaling);
        }

        private static Vector2 GetPlayerByRandOffest(Player player) => player.Center + new Vector2(Main.rand.Next(-300, 300), Main.rand.Next(-300, 300));

        private void TeleportNearTarget(Player target) {
            Vector2 offset = Main.rand.NextVector2Unit() * Main.rand.Next(300, 500);
            NPC.position = target.Center + offset - NPC.Size / 2f;

            //粒子效果
            for (int i = 0; i < 30; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                Dust.NewDustPerfect(NPC.Center, DustID.PurpleTorch, dustVel, 100, Color.Magenta, 1.5f).noGravity = true;
            }

            //音效
            SoundEngine.PlaySound(SoundID.Item8, NPC.Center);//魔法瞬移声
        }

        private void DashTowardTarget(Player target, float speed) {
            Vector2 dir = NPC.SafeDirectionTo(target.Center);
            NPC.velocity = dir * speed;

            //残影或风特效
            for (int i = 0; i < 10; i++) {
                Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(16, 16);
                Dust.NewDustPerfect(pos, DustID.Smoke, -dir * 2f, 100, Color.Gray, 1.2f).noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
        }

        //一次性 HP 里程碑视觉节拍 (纯本地: 震屏 + 焦火爆裂 + 咆哮; 不改 FSM/不联网)
        private void PhaseBeat() {
            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.3f }, NPC.Center);
            ACMUtils.AddScreenShake(8f);
            for (int i = 0; i < 40; i++) {
                Vector2 v = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Torch, v, 120, Color.OrangeRed, Main.rand.NextFloat(1.5f, 3f));
                d.noGravity = true;
            }
        }

        //鬼域牢笼边界贴花 (ArenaRunic 法阵环), 让 800 半径限制场地范围更可读。自管批次并恢复默认批。
        private static void DrawCageDecal(SpriteBatch sb, Vector2 worldCenter, float worldRadius, float intensity) {
            if (Main.dedServ || intensity <= 0.01f)
                return;

            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            // V2: 缩放感知世界→屏幕UV 走共享 WorldDecalParams (含 zoom 项)。
            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 centerUV, out float radiusUV, out float aspect);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uRadius"]?.SetValue(radiusUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Flame.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(new Color(150, 20, 20).ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(1f); // 方形竞技场: 800 半笼用矩形边界 (§item7) 而非圆环

            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.NonPremultiplied);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life > 0) {
                return;
            }
            int Hanba_Body = Mod.Find<ModGore>("Hanba_Body2").Type;
            int Hanba_Body2 = Mod.Find<ModGore>("Hanba_Body2").Type;
            int Hanba_Eye = Mod.Find<ModGore>("Hanba_Eye").Type;
            int Hanba_Top = Mod.Find<ModGore>("Hanba_Top").Type;

            var entitySource = NPC.GetSource_Death();

            for (int i = 0; i < 2; i++) {
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Body);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Body2);
                Gore.NewGore(entitySource, NPC.position, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Top);
            }
            foreach (var pos in EyesOffset) {
                Gore.NewGore(entitySource, NPC.Center + pos, new Vector2(Main.rand.Next(-6, 7), Main.rand.Next(-6, 7)), Hanba_Eye);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //鬼域牢笼期(状态6)绘制竞技场边界贴花, 让限制场地范围可读 (绘制在 Boss 之前=底层)
            if ((int)NPC.ai[0] == 6) {
                float at = NPC.ai[1];
                float fadeIn = MathHelper.Clamp(at / 90f, 0f, 1f);
                float fadeOut = MathHelper.Clamp((600f - at) / 80f, 0f, 1f);
                DrawCageDecal(spriteBatch, OrigRestrictionPos, 800f, fadeIn * fadeOut * 0.85f);
            }

            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = VaultUtils.GetRectangle(mainValue, frame, maxFrame);
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);

            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type != ModContent.NPCType<Talisman>()) {
                    continue;
                }
                if (npc.ai[0] != NPC.whoAmI) {
                    continue;
                }
                if (npc.ModNPC is Talisman talisman) {
                    talisman.DoDraw(spriteBatch, drawColor);
                }
            }
            return false;
        }
    }

    internal class Talisman : ModNPC
    {
        private Hanba Hanba { get; set; }
        public override void SetDefaults() {
            NPC.npcSlots = 4f;
            NPC.width = 40;
            NPC.height = 140;
            NPC.defense = 25;
            NPC.damage = 60;
            NPC.value = Item.buyPrice(0, 5, 0, 0);
            NPC.lifeMax = 60000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = 50000;
            if (Main.expertMode) {
                NPC.lifeMax += 5000;
            }
            if (Main.masterMode) {
                NPC.lifeMax += 5000;
            }
        }

        public override void AI() {
            NPC npc = Main.npc[(int)NPC.ai[0]];
            if (npc.Alives() && npc.ModNPC is not null && npc.ModNPC is Hanba boss) {
                Hanba = boss;
                NPC.Center = Hanba.NPC.Center;
                NPC.rotation = Hanba.NPC.rotation;
            }
        }

        public override void OnKill() {
            if (Hanba.NPC.Alives()) {
                Hanba.TalismanKill();
            }
        }

        public void DoDraw(SpriteBatch spriteBatch, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, null, drawColor
                , NPC.rotation, mainValue.Size() / 2, 0.4f, SpriteEffects.None, 0);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }

    internal class HanbaFireBall : ModProjectile//超级无敌狗屎恶心人追踪火球之宫崎英高小时候上学被着火的导弹追
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
                if (proj.type != ModContent.ProjectileType<HanbaFireBall>()) {
                    continue;
                }
                proj.Kill();
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.Torch, Projectile.velocity.X / 2, Projectile.velocity.Y / 2);
                    Main.dust[dust].scale = Main.rand.NextFloat(1f, 3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (++Projectile.ai[0] < 100) {
                Projectile.velocity = Projectile.velocity.RotatedBy(0.02f * Projectile.ai[2]);
            }
            else {
                Player player = Projectile.Center.FindClosestPlayer(3200, true);
                if (player is not null) {
                    Vector2 targetSpeed = Projectile.SafeDirectionTo(player.Center) * Projectile.velocity.Length() * 1.4f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetSpeed, 0.04f);
                }
            }
        }
    }

    internal class HanbaBigLaser : HanbaLaser
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        // 巨大黄金太阳柱: 用 localAI[2] 作为生长源, 更宽, 烈焰金核心 + 赤焰边缘。
        protected override float BeamGrow => MathHelper.Clamp(Projectile.localAI[2] / 30f, 0f, 1f);
        protected override float BeamHalfWidth => Weith * 24f;
        protected override Color BeamCore => new Color(255, 225, 110);
        protected override Color BeamEdge => new Color(210, 60, 20);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 1120;
            Projectile.tileCollide = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            base.PreDraw(ref lightColor);
            float grow = BeamGrow;
            if (grow > 0.05f)
                ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.17f, grow * 0.9f, new Color(255, 210, 120));
            return false;
        }

        public static new void AllVanish() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<HanbaBigLaser>()) {
                    continue;
                }

                proj.ai[1] = 1;
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            Weith = 6 * (Projectile.localAI[2] / 30f);
            Projectile.timeLeft = 1120;

            NPC npc = Main.npc[(int)Projectile.ai[0]];
            if (npc.Alives() && npc.ModNPC is not null && npc.ModNPC is Hanba boss) {
                Vector2 origin = boss.NPC.Center;

                Projectile.Center = origin;

                Leng = 4000; //计算打到边缘的长度

                Player player = Projectile.Center.FindClosestPlayer(4000);
                if (player is not null) {
                    Projectile.rotation = Projectile.rotation.RotTowards(Projectile.Center.To(player.Center).ToRotation(), 0.02f);
                }

                for (int i = 0; i < 255; i++) {
                    Vector2 pos = Projectile.Center + Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(Leng);
                    pos += VaultUtils.GetNormalVector(Projectile.rotation.ToRotationVector2()) * Main.rand.NextFloat(-Weith / 2, Weith / 2) * 22;
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Torch, Main.rand.NextVector2Unit() * Main.rand.NextFloat(3), 150, Color.OrangeRed, 1.5f);
                    dust.noGravity = true;
                }
            }
            else {
                Projectile.ai[1] = 1f;
            }

            if (Projectile.ai[1] == 0) {
                if (Projectile.localAI[2] < 30) {
                    Projectile.localAI[2]++;
                }
            }
            else {
                if (Projectile.localAI[2] > 0) {
                    Projectile.localAI[2]--;
                }
                else {
                    Projectile.Kill();
                }
            }

            // 大招持续低频轰鸣 (随生长缩放; Add 内部取 max + 客户端守卫)。
            ACMUtils.AddScreenShake(BeamGrow * 3f);

            Time++;
        }
    }

    [VaultLoaden("AncientChineseMythology/NPCs/Boss/Hanbas/")]
    internal class HanbaLaser : ModProjectile
    {
        internal static Asset<Texture2D> UltimaRayEnd = null;
        internal static Asset<Texture2D> UltimaRayMid = null;
        internal static Asset<Texture2D> UltimaRayStart = null;
        public Vector2 offsetData;
        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }
        public ref float Weith => ref Projectile.localAI[0];
        public float Leng {
            get => Projectile.localAI[1];
            set => Projectile.localAI[1] = value;
        }

        // —— BeamGrad 原语接线 (V2: 旱魃激光抽象为共享 beam 原语供雷柱/链电复用) ——
        // 0~1 生长进度: 同时驱动 alpha 淡入淡出与宽度成长, 也充当蓄力 telegraph 计时。
        protected virtual float BeamGrow => MathHelper.Clamp(Projectile.localAI[0] / 30f, 0f, 1f);
        // 屏幕像素半宽(随 Weith 成长)。
        protected virtual float BeamHalfWidth => Weith * 20f;
        // 身份色: 焦橙核心 + 血红边缘 (统一 §6.1, 红只用于致命源)。
        protected virtual Color BeamCore => new Color(255, 150, 40);
        protected virtual Color BeamEdge => new Color(150, 12, 22);

        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 1120;
            Projectile.tileCollide = false;
        }

        public static void AllVanish() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<HanbaLaser>()) {
                    continue;
                }

                proj.ai[1] = 1;
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            Weith = 1 * (Projectile.localAI[0] / 30f);
            Projectile.timeLeft = 1120;
            Projectile.rotation = Projectile.velocity.ToRotation();

            NPC npc = Main.npc[(int)Projectile.ai[0]];
            if (npc.Alives() && npc.ModNPC is not null && npc.ModNPC is Hanba boss) {
                Vector2 origin = boss.NPC.Center + offsetData.RotatedBy(boss.NPC.rotation);
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(boss.NPC.rotation);

                Projectile.Center = origin;
                Projectile.rotation = dir.ToRotation();

                Leng = DistanceToRectEdge(origin, dir, boss.GetOrigPos(), 800, 800); //计算打到边缘的长度

                Vector2 endPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * (Leng + Projectile.width);
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustPerfect(endPos, DustID.Torch, Main.rand.NextVector2Unit() * Main.rand.NextFloat(3), 150, Color.OrangeRed, 1.5f);
                    dust.noGravity = true;
                }
            }
            else {
                Projectile.ai[1] = 1f;
            }

            if (Projectile.ai[1] == 0) {
                if (Projectile.localAI[0] < 30) {
                    Projectile.localAI[0]++;
                }
            }
            else {
                if (Projectile.localAI[0] > 0) {
                    Projectile.localAI[0]--;
                }
                else {
                    Projectile.Kill();
                }
            }

            Time++;
        }

        public static float DistanceToRectEdge(Vector2 origin, Vector2 direction, Vector2 rectCenter, float sizeX, float sizeY) {
            //半宽/高
            float halfX = sizeX;
            float halfY = sizeY;

            //计算矩形的边界
            float left = rectCenter.X - halfX;
            float right = rectCenter.X + halfX;
            float top = rectCenter.Y - halfY;
            float bottom = rectCenter.Y + halfY;

            //单位化方向向量
            direction = direction.SafeNormalize(Vector2.UnitY);

            float tMin = float.PositiveInfinity;

            //检查 X 边界
            if (direction.X != 0f) {
                float tx1 = (left - origin.X) / direction.X;
                float tx2 = (right - origin.X) / direction.X;

                foreach (float t in new[] { tx1, tx2 }) {
                    if (t > 0) {
                        float y = origin.Y + t * direction.Y;
                        if (y >= top && y <= bottom)
                            tMin = Math.Min(tMin, t);
                    }
                }
            }

            //检查 Y 边界
            if (direction.Y != 0f) {
                float ty1 = (top - origin.Y) / direction.Y;
                float ty2 = (bottom - origin.Y) / direction.Y;

                foreach (float t in new[] { ty1, ty2 }) {
                    if (t > 0) {
                        float x = origin.X + t * direction.X;
                        if (x >= left && x <= right)
                            tMin = Math.Min(tMin, t);
                    }
                }
            }

            //返回距离
            return float.IsInfinity(tMin) ? 2000f : tMin;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float grow = BeamGrow;
            if (grow <= 0.001f)
                return false;

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float length = Leng + Projectile.width;

            // 蓄力 telegraph: 生长前半段把光束染向纯红致命预警(§6.1), 完全展开后回到身份色。
            float warn = 1f - MathHelper.Clamp(grow / 0.5f, 0f, 1f);
            Color core = Color.Lerp(BeamCore, TelegraphColors.Lethal, warn * 0.65f);
            Color edge = Color.Lerp(BeamEdge, TelegraphColors.Lethal, warn);

            // V2: 提升后的共享 beam 原语 (旱魃为其首发验证者; 视觉与旧本地 DrawBeamGrad 一致)。
            Vector2 endPos = Projectile.Center + dir.SafeNormalize(Vector2.UnitX) * length;
            ACMShaders.DrawBeam(Projectile.Center, endPos, BeamHalfWidth, core, edge, grow);
            return false;
        }
    }

    internal class Shockwave : ModProjectile
    {
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0)
                ACMUtils.AddScreenShake(3f);
            Projectile.ai[0]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Color drawColor = Color.Orange * (1f - Projectile.ai[0] / 30f);
            drawColor.A = 0;
            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null
                , drawColor, 0, value.Size() / 2, Projectile.ai[0] / 10f * Projectile.ai[1], SpriteEffects.None, 0);
            return false;
        }
    }

    internal class LocustSet : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (Projectile.localAI[0] < 40) {
                if (Projectile.localAI[0] == 0) {
                    SoundEngine.PlaySound(SoundID.Item84 with { Pitch = -0.2f });
                    Projectile.localAI[1] = 30;
                }
                Projectile.localAI[0]++;
            }
            else {
                if (Projectile.localAI[1] > 0) {
                    Projectile.localAI[1]--;
                }

                float spanLength = Projectile.localAI[0] * 5 / 2;

                //生成两个粒子（上下）
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 localOffset = new Vector2(-1800, i * Main.rand.NextFloat(spanLength, 800));
                    Vector2 rotatedOffset = localOffset.RotatedBy(Projectile.velocity.ToRotation());
                    Vector2 spanPos = Projectile.Center + rotatedOffset;

                    Vector2 velocity = new Vector2(64, 0).RotatedBy(Projectile.velocity.ToRotation());

                    PRTLoader.NewParticle<LocustPRT>(spanPos, velocity);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D back = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            int width = 4400;
            int height = (int)(Projectile.localAI[0] * 5);
            float alpha = Projectile.localAI[1] / 60f;

            Rectangle rect = new Rectangle(-width / 2, -height / 2, width, height);
            Vector2 origin = new Vector2(rect.Width / 2, rect.Height / 2);

            // 蝗虫墙是即将横扫的致命路径 → 预警带偏向纯红(§6.1), 略掺焦金保留身份。
            Color bandColor = Color.Lerp(TelegraphColors.Lethal, new Color(255, 170, 40), 0.35f);
            bandColor.A = 160;
            Main.spriteBatch.Draw(back, drawPos, rect, bandColor * alpha
                , Projectile.velocity.ToRotation(), origin, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }

    internal class LocustPRT : BasePRT
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hanbas/Locust";
        private float waveOffset;

        public override void SetProperty() {
            Lifetime = 160;
            ShouldKillWhenOffScreen = false;
            waveOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            Scale = Main.rand.NextFloat(0.4f, 1f);
        }

        public override void AI() {
            Rotation = Velocity.ToRotation();

            //振荡轨迹
            float waveStrength = 2.5f;
            Vector2 normal = Velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Position += normal * (float)Math.Sin(Time / 6f + waveOffset) * waveStrength;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            SpriteEffects spriteEffects = Velocity.X > 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null, Color.White,
                Rotation + MathHelper.Pi + MathHelper.PiOver4 * Math.Sign(Velocity.X), TexValue.Size() / 2, Scale, spriteEffects, 0);

            return false;
        }
    }

    [VaultLoaden("AncientChineseMythology/Textures/Backgrounds/")]
    internal class HanbaSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.6f;
        private Color skyColor;
        internal static string name;
        internal static Asset<Texture2D> HanbaSkySun;
        internal static Asset<Texture2D> HanbaSkyColorBar;
        public static void LoadInstance() {
            name = "AncientChineseMythology:HanbaSky";
            SkyManager.Instance[name] = new HanbaSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //轻微抖动效果，营造焦灼震颤氛围
            Vector2 shake = Main.rand.NextVector2Circular(2f * intensity, 2f * intensity);

            //背景天空层（主色调）
            spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight), skyColor * intensity);

            //渲染渐变色层（比如橙红色霞光）
            spriteBatch.Draw(HanbaSkyColorBar.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White * intensity);

            //渲染焦日（带光晕）
            Vector2 sunPos = new Vector2(1447, 80);
            Color sunColor = new Color(255, 180, 100, 0) * intensity * 1.2f;
            spriteBatch.Draw(HanbaSkySun.Value, sunPos, null, sunColor, 0f, new Vector2(90), 1.5f, SpriteEffects.None, 0f);
        }

        public override bool IsActive() {
            return active;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override void Update(GameTime gameTime) {
            if (NPC.AnyNPCs(ModContent.NPCType<Hanba>())) {
                NPC boss = null;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == ModContent.NPCType<Hanba>()) {
                        boss = npc;
                        break;
                    }
                }

                if (boss != null) {
                    float distance = Main.LocalPlayer.Distance(boss.Center);
                    float t = MathHelper.Clamp(distance / 1600f, 0f, 1f); //越近越暗红
                    skyColor = VaultUtils.MultiStepColorLerp(t,
                        new Color(100, 30, 0),    //焦棕
                        new Color(140, 20, 20),   //血红
                        new Color(255, 80, 0));   //炽橙

                    if (intensity < maxIntensity)
                        intensity += 0.01f;

                    active = true;
                }
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            return inColor * (1f - intensity);
        }
    }
}
