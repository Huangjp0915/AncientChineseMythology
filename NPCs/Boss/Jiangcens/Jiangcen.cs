using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Jiangcens
{
    public class JiangcenHammerItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";
        public override void SetDefaults() {
            Item.width = 150;
            Item.height = 132;
            Item.damage = 680;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 22;
            Item.shootSpeed = 25f;
            Item.knockBack = 6f;
            Item.shoot = ModContent.ProjectileType<JiangcenHammerProj>();
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Red;
            Item.value = 2000;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
        }
    }

    public class JiangcenHammerProj : BaseHeldProj
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.localNPCHitCooldown = 30;
            Projectile.extraUpdates = 3;
            Projectile.penetrate = -1;
            Projectile.width = Projectile.height = 132;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 紫色雷电爆发粒子
            for (int i = 0; i < 125; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(16f, 26f);
                Dust d = Dust.NewDustPerfect(
                    target.Center,
                    DustID.PurpleTorch, // 紫色火焰
                    dustVel,
                    150,
                    Color.MediumPurple,
                    Main.rand.NextFloat(11.2f, 31.8f)
                );
                d.noGravity = true;
            }

            // 黑暗雾气
            for (int i = 0; i < 115; i++) {
                Dust smoke = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(13f, 33f),
                    200,
                    Color.Purple * 0.7f,
                    Main.rand.NextFloat(11f, 21.5f)
                );
                smoke.noGravity = true;
            }

            // 雷鸣音效
            SoundEngine.PlaySound(SoundID.DD2_LightningBugZap, target.Center);

            base.OnHitNPC(target, hit, damageDone);
        }

        public override void AI() {
            //紫色光效
            Lighting.AddLight(
                Projectile.Center,
                0.5f,
                0.2f,
                0.6f
            );

            Projectile.rotation += 0.4f; // 转得更快

            //拖尾闪光
            Dust trail = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.MagicMirror,
                    -Projectile.velocity * 0.2f,
                    150,
                    Color.MediumPurple,
                    1.2f
                );
            trail.noGravity = true;

            if (Projectile.soundDelay == 0) {
                Projectile.soundDelay = 12;
                SoundEngine.PlaySound(SoundID.Item7, Projectile.position); //魔法雷鸣
            }

            switch (Projectile.ai[0]) {
                case 0f:
                    Projectile.ai[1] += 1f;
                    if (Projectile.ai[1] >= 40f) {
                        Projectile.ai[0] = 1f;
                        Projectile.ai[1] = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;
                case 1f:
                    float returnSpeed = 25f;
                    float acceleration = 5f;
                    Vector2 playerVec = Owner.Center - Projectile.Center;
                    if (playerVec.Length() > 4000f) {
                        Projectile.Kill();
                    }
                    playerVec.Normalize();
                    playerVec *= returnSpeed;

                    //X方向加速
                    if (Projectile.velocity.X < playerVec.X) {
                        Projectile.velocity.X += acceleration;
                        if (Projectile.velocity.X < 0f && playerVec.X > 0f)
                            Projectile.velocity.X += acceleration;
                    }
                    else if (Projectile.velocity.X > playerVec.X) {
                        Projectile.velocity.X -= acceleration;
                        if (Projectile.velocity.X > 0f && playerVec.X < 0f)
                            Projectile.velocity.X -= acceleration;
                    }

                    //Y方向加速
                    if (Projectile.velocity.Y < playerVec.Y) {
                        Projectile.velocity.Y += acceleration;
                        if (Projectile.velocity.Y < 0f && playerVec.Y > 0f)
                            Projectile.velocity.Y += acceleration;
                    }
                    else if (Projectile.velocity.Y > playerVec.Y) {
                        Projectile.velocity.Y -= acceleration;
                        if (Projectile.velocity.Y > 0f && playerVec.Y < 0f)
                            Projectile.velocity.Y -= acceleration;
                    }

                    //回到玩家后消失
                    if (Main.myPlayer == Projectile.owner) {
                        Rectangle projHitbox = new Rectangle((int)Projectile.position.X, (int)Projectile.position.Y, Projectile.width, Projectile.height);
                        Rectangle playerHitbox = new Rectangle((int)Owner.position.X, (int)Owner.position.Y, Owner.width, Owner.height);
                        if (projHitbox.Intersects(playerHitbox)) {
                            Projectile.Kill();
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();
            float sengs = 0.6f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 drawOldPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                Main.spriteBatch.Draw(mainValue, drawOldPos, rectangle, lightColor * sengs
                    , Projectile.oldRot[i] + MathHelper.PiOver2, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            Main.spriteBatch.Draw(mainValue, Projectile.Center - Main.screenPosition, rectangle, lightColor
                , Projectile.rotation + MathHelper.PiOver2, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    [AutoloadBossHead]
    internal class Jiangcen : ModNPC
    {
        private float aiTimer;
        private float shockwave;
        private float shootTimer;
        private int projectileBaseDamage = 50;
        private float storePosX = 0;
        private float storePosY = 0;
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
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
            NPC.lifeMax = 420000;
            NPC.aiStyle = -1;
            AIType = -1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Yingou");
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<JiangcenHammerItem>()));
        }

        public override bool CheckActive() {
            return false;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void AI() {
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];
            if (!target.active || target.dead) {
                NPC.TargetClosest();//
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.ai[0] = -1;
                    NPC.ai[1] = 0f;
                    NPC.ai[2] = 0f;
                }
            }

            ref float generalTimer = ref NPC.ai[2];
            ref float attackTimer = ref NPC.ai[1];
            ref float state = ref NPC.ai[0];

            //进入战斗的演出
            if (generalTimer == 0) {
                if (!VaultUtils.isServer && !SkyManager.Instance[JiangcenSky.name].IsActive()) {
                    SkyManager.Instance.Activate(JiangcenSky.name); //暗红天空
                }

                if (!VaultUtils.isClient) {
                    for (int i = 0; i < 6; i++) {
                        NPC.NewNPCDirect(NPC.FromObjectGetParent(), NPC.Center, ModContent.NPCType<JiangcenHammer>(), NPC.whoAmI, NPC.whoAmI, i);
                    }
                }

                for (int i = 0; i < 50; i++) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4), 150, Color.DarkRed, 2f);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.position);
            }

            //距离狂暴
            float distToPlayer = Vector2.Distance(NPC.Center, target.Center);
            bool enraged = distToPlayer > 1000;

            if (state == 0) {
                aiTimer++;
                shootTimer++;
                if (aiTimer < 300)
                    FlyTo(new Vector2(target.Center.X - 200, target.Center.Y - 200), 0.1f, 14f);
                else
                    FlyTo(new Vector2(target.Center.X + 200, target.Center.Y - 200), 0.1f, 14f);

                int shootRate = enraged ? 3 : 6;
                if (shootTimer % shootRate == 0 && shootTimer < 30) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f }, NPC.position);
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 pos = NPC.Center + Main.rand.NextVector2Square(-150, 150);
                        float Speed = 12f;
                        float rotation = (float)Math.Atan2(pos.Y - target.Center.Y, pos.X - target.Center.X);
                        Vector2 projSpeed = new Vector2((float)((Math.Cos(rotation) * Speed) * -1), (float)((Math.Sin(rotation) * Speed) * -1));
                        int proj = Projectile.NewProjectile(this.FromObjectGetParent(), pos, projSpeed, ModContent.ProjectileType<JiangcenFireBall>(), projectileBaseDamage, 0f, Main.myPlayer);

                        //血雾特效
                        for (int p = 0; p < 15; p++) {
                            Vector2 dustPos = Main.projectile[proj].Center + Main.rand.NextVector2Circular(10, 10);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Shadowflame, Main.rand.NextFloat(-2, 2), Main.rand.NextFloat(-2, 2), 100, Color.DarkRed, 1.6f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                }
                if (shootTimer > 75) shootTimer = 0;
                if (aiTimer > 600) {
                    aiTimer = 2;
                    shootTimer = 0;
                    state++;
                }
            }
            else if (state == 1) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f }, NPC.position);
                    float speed = 5f;
                    float numberProjectiles = Main.expertMode ? (Main.masterMode ? 12 : 8) : 6;
                    float rotation = MathHelper.ToRadians(360);
                    for (int i = 0; i < numberProjectiles; i++) {
                        Vector2 perturbedSpeed = Vector2.One.RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (numberProjectiles - 1))) * speed;
                        Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, perturbedSpeed, ModContent.ProjectileType<HeuderHammer>(), (int)(projectileBaseDamage * 1.2f), 2f, Main.myPlayer);
                    }
                }
                state = aiTimer;
                aiTimer = 0;
                shootTimer = 0;
            }
            else if (state == 2) {
                aiTimer++;
                if (shootTimer == 0) {
                    ShadowflameTP(target.Center - new Vector2(0, 300));
                    NPC.velocity = Vector2.Zero;
                }
                else if (shootTimer > 20) {
                    NPC.velocity.Y = 30;
                }
                Tile tileTest = Framing.GetTileSafely((int)(NPC.Bottom.X / 16), (int)(NPC.Bottom.Y / 16));
                shootTimer++;
                if ((tileTest.HasTile && Main.tileSolid[tileTest.TileType] && !TileID.Sets.Platforms[tileTest.TileType] && shootTimer > 30) || shootTimer > 60) {
                    shootTimer = 0;

                    for (int i = 0; i < 20; i++) {
                        Dust dust = Main.dust[Dust.NewDust(new Vector2(NPC.position.X, NPC.Bottom.Y - 16), NPC.width, 16, DustID.Shadowflame, 0f, 0f, 100, Color.DarkRed, 2.5f)];
                        dust.noGravity = true;
                        if (dust.position.X < NPC.Center.X) dust.velocity.X = Main.rand.NextFloat(0.8f, 1.2f) * -6f;
                        else dust.velocity.X = Main.rand.NextFloat(0.8f, 1.2f) * 6f;
                        dust.velocity.Y = Main.rand.NextFloat(-10, -2);
                    }
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.position);
                }
                if (aiTimer > 300) {
                    aiTimer = 0;
                    shootTimer = 0;
                    state++;
                    if (NPC.life > NPC.lifeMax * 0.5f) state++;
                    ShadowflameTP(target.Center - new Vector2(0, 500));
                    NPC.velocity = Vector2.Zero;
                }
            }
            else if (state == 3) {
                FlyTo(new Vector2(target.Center.X, target.Center.Y), 0.1f, 14f);
                shootTimer--;
                if (shootTimer <= 0) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f }, NPC.position);

                    float speed = 5f;
                    float numberProjectiles = Main.masterMode ? 3 : 2;
                    for (int i = 0; i < numberProjectiles; i++) {
                        int proj = -1;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float rotation = (float)Math.Atan2(NPC.Center.Y - target.Center.Y, NPC.Center.X - target.Center.X);
                            proj = Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center.X, NPC.Center.Y
                                , (float)((Math.Cos(rotation) * speed) * -1), (float)((Math.Sin(rotation) * speed) * -1)
                                , ModContent.ProjectileType<HeuderHammer>(), projectileBaseDamage, 2f, Main.myPlayer);
                        }
                        if (proj != -1) {
                            Projectile ice = Main.projectile[proj];
                            int distance = (int)((NPC.width / 2) * 0.8f);
                            float rad = (MathHelper.ToRadians(360) / numberProjectiles) * i;
                            ice.position.X = NPC.Center.X - (int)(Math.Cos(rad) * distance) - ice.width / 2;
                            ice.position.Y = NPC.Center.Y - (int)(Math.Sin(rad) * distance) - ice.height / 2;
                            int numDusts = 20;
                            for (int p = 0; p < numDusts; p++) {
                                Vector2 position = (Vector2.One * new Vector2(ice.width / 2f, ice.height) * 0.3f * 0.5f).RotatedBy((double)((p - (numDusts / 2 - 1)) * 6.28318548f / numDusts), default(Vector2)) + ice.Center;
                                Vector2 velocity = position - ice.Center;
                                int dust = Dust.NewDust(position + velocity, 0, 0, DustID.Shadowflame, velocity.X * 2f, velocity.Y * 2f, 100, Color.DarkRed, 1.4f);
                                Main.dust[dust].noGravity = true;
                                Main.dust[dust].velocity = Vector2.Normalize(velocity) * 2f;
                            }
                        }
                    }
                    shootTimer = 45;
                    aiTimer++;
                }
                if (aiTimer > 3) {
                    aiTimer = 0;
                    shootTimer = 0;
                    state++;
                }
            }
            else if (state == 4) {
                aiTimer++;
                if (shockwave == 0) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.4f }, NPC.position);
                    shockwave = 1;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int numProj = Main.expertMode ? Main.masterMode ? 35 : 25 : 15;
                        float speed = Main.expertMode ? Main.masterMode ? 17f : 15f : 13f;
                        for (int i = 0; i < numProj; i++) {
                            Projectile proj = Main.projectile[Projectile.NewProjectile(NPC.FromObjectGetParent()
                                , NPC.Center.X + Main.rand.Next(-1000, 1000), NPC.Center.Y - Main.rand.Next(800, 1800)
                                , 0, speed, ModContent.ProjectileType<JiangcenFireBall>(), projectileBaseDamage, 0f, Main.myPlayer)];
                            proj.rotation = Main.rand.NextFloat((float)Math.PI * 2);
                        }
                    }
                }
                if (aiTimer > 300) {
                    shockwave = 0;
                    aiTimer = 0;
                    shootTimer = 0;
                    state++;
                }
            }
            else if (state == 5) {
                float dashSpeed = Main.expertMode ? Main.masterMode ? 33 : 26 : 18;
                int tpDist = 700;
                if (NPC.life < NPC.lifeMax * 0.75f) tpDist -= 100;
                if (NPC.life < NPC.lifeMax * 0.5f) tpDist -= 100;
                if (NPC.life < NPC.lifeMax * 0.25f) tpDist -= 100;
                aiTimer++;
                if (shootTimer == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    //int orbitalcount = Main.expertMode ? Main.masterMode ? 11 : 8 : 5;
                    shootTimer++;
                }
                if (aiTimer < 90) {
                    Vector2 toTarget = new Vector2(target.Center.X - NPC.Center.X, target.Center.Y - NPC.Center.Y);
                    toTarget.Normalize();
                    if (Vector2.Distance(target.Center, NPC.Center) >= 30) {
                        NPC.velocity = toTarget * 0.1f;
                    }
                }
                else if (aiTimer == 90) {
                    SoundEngine.PlaySound(SoundID.Roar with { PitchRange = (-0.1f, 0.2f) }, NPC.position);
                    ShadowflameTP(target.Center + new Vector2(tpDist, 0));
                }
                else if (aiTimer > 90 && aiTimer < 210) {
                    NPC.velocity.Y = 0;
                    NPC.velocity.X = -dashSpeed;
                }
                else if (aiTimer == 210) {
                    SoundEngine.PlaySound(SoundID.Roar with { PitchRange = (-0.1f, 0.2f) }, NPC.position);
                    ShadowflameTP(target.Center + new Vector2(-tpDist, 0));
                }
                else if (aiTimer > 210) {
                    NPC.velocity.Y = 0;
                    NPC.velocity.X = dashSpeed;
                }
                if (aiTimer > 330) {
                    aiTimer = 6;
                    shootTimer = 0;
                    state = 1;
                    CircularTP(target, 500);
                    NPC.velocity = Vector2.Zero;
                }
            }
            else if (state == 6) {
                FlyTo(new Vector2(target.Center.X, target.Center.Y), 0.1f, 14f);
                aiTimer++;
                if (shootTimer == 0) {
                    storePosX = target.Center.X + Main.rand.Next(-600, 600);
                    storePosY = target.Center.Y + Main.rand.Next(-300, 300);
                    NPC.netUpdate = true;
                }
                else if (shootTimer < 0) {
                    Vector2 storedPos = new Vector2(storePosX, storePosY);
                    Dust dust = Main.dust[Dust.NewDust(storedPos, 2, 2, DustID.Shadowflame, 0f, 0f, 200, Color.DarkRed, 2.5f)];
                    dust.noGravity = true;
                    dust.fadeIn = 1.3f;
                    Vector2 vector = Main.rand.NextVector2Square(-1, 1f);
                    vector.Normalize();
                    vector *= 3f;
                    dust.velocity = vector;
                    dust.position = storedPos - vector * 15;
                }
                shootTimer--;
                if (shootTimer <= -60 && Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f }, NPC.position);
                    Vector2 storedPos = new Vector2(storePosX, storePosY);
                    float Speed = 12f;
                    float rotation = (float)Math.Atan2(storedPos.Y - target.Center.Y, storedPos.X - target.Center.X);
                    Vector2 projSpeed = new Vector2((float)((Math.Cos(rotation) * Speed) * -1), (float)((Math.Sin(rotation) * Speed) * -1));
                    Projectile.NewProjectile(this.FromObjectGetParent(), storedPos, projSpeed, ModContent.ProjectileType<JiangcenFireBall>(), projectileBaseDamage, 0f, Main.myPlayer);
                    shootTimer = 0;
                }
                if (aiTimer % 120 == 0) {
                    CircularTP(target, 500);
                }
                if (aiTimer > 600) {
                    aiTimer = 0;
                    shootTimer = 0;
                    state++;
                    CircularTP(target, 650);
                }
            }
            else if (state == 7) {
                FlyTo(new Vector2(target.Center.X, target.Center.Y), 0.05f, 3f);
                aiTimer++;
                shootTimer--;
                if (shootTimer <= 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = -0.4f }, NPC.Center);
                    Vector2 mouth = new Vector2(NPC.Center.X, NPC.Center.Y + 40);
                    float Speed = 14f;
                    float rotation = (float)Math.Atan2(mouth.Y - target.Center.Y, mouth.X - target.Center.X);
                    Vector2 projSpeed = new Vector2((float)((Math.Cos(rotation) * Speed) * -1), (float)((Math.Sin(rotation) * Speed) * -1));
                    projSpeed = projSpeed.RotatedByRandom(MathHelper.ToRadians(10));
                    Projectile.NewProjectile(this.FromObjectGetParent(), mouth, projSpeed, ModContent.ProjectileType<JiangcenFireBall>(), projectileBaseDamage, 0f, Main.myPlayer);
                    shootTimer = 6;
                }
                if (aiTimer > 270) {
                    aiTimer = 0;
                    shootTimer = 0;
                    state++;
                }
            }
            else if (state == 8) {
                aiTimer++;
                if (shootTimer == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    CircularTP(target, 500);
                    NPC.velocity = Vector2.Zero;
                    storePosX = target.Center.X;
                    storePosY = target.Center.Y;
                    float Speed = 24f;
                    float rotation = (float)Math.Atan2(NPC.Center.Y - target.Center.Y, NPC.Center.X - target.Center.X);
                    Vector2 projSpeed = new Vector2((float)((Math.Cos(rotation) * Speed) * -1), (float)((Math.Sin(rotation) * Speed) * -1));
                    Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center, projSpeed, ModContent.ProjectileType<JiangcenFireBall>(), 0, 0, Main.myPlayer);
                }
                else if (shootTimer == -30) {
                    Vector2 toTarget = new Vector2(target.Center.X - NPC.Center.X, target.Center.Y - NPC.Center.Y);
                    toTarget.Normalize();
                    NPC.velocity = toTarget * 20;
                }
                if (shootTimer < -30) {
                    NPC.velocity *= 0.99f;
                }
                shootTimer--;
                if (shootTimer <= -100) {
                    shootTimer = 0;
                }
                if (aiTimer > 360) {
                    aiTimer = 0;
                    shootTimer = -30;
                    state = 0;
                    ShadowflameTP(target.Center + new Vector2(0, -300));
                    NPC.velocity = Vector2.Zero;
                }
            }

            //瞬移加血雾
            void ShadowflameTP(Vector2 newPos) {
                for (int i = 0; i < 30; i++) {
                    int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4), 150, Color.DarkRed, 2f);
                    Main.dust[dust].noGravity = true;
                }
                NPC.Center = newPos;
                NPC.netUpdate = true;
            }

            generalTimer++;
        }

        private void Teleport(Vector2 toPos) {
            SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
            for (int k = 0; k < 50; k++) {
                Dust d = Main.dust[Dust.NewDust(NPC.Center + (toPos - NPC.Center) * Main.rand.NextFloat() - new Vector2(4, 4), 16, 16, Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.SilverFlame)];
                d.noGravity = true;
                d.velocity *= 1.2f;
                if (d.type == 41) d.scale *= 1.8f;
                else d.scale *= 2.8f;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center.X, NPC.Center.Y, 0, 0, ModContent.ProjectileType<JiangcenFireBall>(), 0, 0f, Main.myPlayer);
                NPC.Center = toPos;
                NPC.netUpdate = true;
                Projectile.NewProjectile(NPC.FromObjectGetParent(), NPC.Center.X, NPC.Center.Y, 0, 0, ModContent.ProjectileType<JiangcenFireBall>(), 0, 0f, Main.myPlayer);
            }
        }

        private void CircularTP(Player P, float dist) {
            double angle = Main.rand.NextDouble() * 2d * Math.PI;
            Vector2 offset = new Vector2((float)Math.Sin(angle) * dist, (float)Math.Cos(angle) * dist);
            Teleport(P.Center + offset);
            NPC.netUpdate = true;
        }

        private void FlyTo(Vector2 location, float acceleration, float speed) {
            float targetX = location.X - NPC.Center.X;
            float targetY = location.Y - NPC.Center.Y;
            float targetPos = (float)Math.Sqrt((double)(targetX * targetX + targetY * targetY));
            targetPos = speed / targetPos;
            targetX *= targetPos;
            targetY *= targetPos;
            if (NPC.velocity.X < targetX) {
                NPC.velocity.X = NPC.velocity.X + acceleration;
                if (NPC.velocity.X < 0f && targetX > 0f) {
                    NPC.velocity.X = NPC.velocity.X + acceleration;
                }
            }
            else if (NPC.velocity.X > targetX) {
                NPC.velocity.X = NPC.velocity.X - acceleration;
                if (NPC.velocity.X > 0f && targetX < 0f) {
                    NPC.velocity.X = NPC.velocity.X - acceleration;
                }
            }
            if (NPC.velocity.Y < targetY) {
                NPC.velocity.Y = NPC.velocity.Y + acceleration;
                if (NPC.velocity.Y < 0f && targetY > 0f) {
                    NPC.velocity.Y = NPC.velocity.Y + acceleration;
                }
            }
            else if (NPC.velocity.Y > targetY) {
                NPC.velocity.Y = NPC.velocity.Y - acceleration;
                if (NPC.velocity.Y > 0f && targetY < 0f) {
                    NPC.velocity.Y = NPC.velocity.Y - acceleration;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , 0, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , NPC.rotation, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class JiangcenHammer : ModNPC
    {
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
            NPC.value = 20000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.dontCountMe = true;
            NPC.dontTakeDamage = true;
        }

        public override bool CheckActive() {
            return false;
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

            ref float generalTimer = ref NPC.ai[3];
            ref float attackTimer = ref NPC.ai[2];
            ref float state = ref NPC.ai[1];

            NPC boss = Main.npc[(int)NPC.ai[0]];
            if (!boss.Alives() || boss.ModNPC is not Jiangcen) {
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }

            NPC.realLife = boss.whoAmI;
            NPC.target = boss.target;
            Jiangcen modNpc = (Jiangcen)boss.ModNPC;

            Vector2 offsetPos;

            offsetPos = (boss.ai[2] * 0.1f + MathHelper.TwoPi / 6f * NPC.ai[1]).ToRotationVector2() * 120;

            NPC.Center = boss.Center + offsetPos;
            NPC.rotation = boss.To(NPC.Center).ToRotation();

            generalTimer++;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();
            float sengs = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                Vector2 drawOldPos = NPC.oldPos[i] + NPC.Size / 2 - Main.screenPosition;
                spriteBatch.Draw(mainValue, drawOldPos, rectangle, drawColor * sengs
                    , NPC.oldRot[i] + MathHelper.PiOver2, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
                sengs *= 0.8f;
            }
            spriteBatch.Draw(mainValue, NPC.Center - Main.screenPosition, rectangle, drawColor
                , NPC.rotation + MathHelper.PiOver2, rectangle.Size() / 2, NPC.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class HeuderHammer : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Jiangcens/JiangcenHammer";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 84;
            Projectile.tileCollide = false;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 660;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
        }
        public override void AI() {
            Projectile.rotation += 0.1f;
            if (++Projectile.ai[0] > 190) {
                Player player = Projectile.Center.FindClosestPlayer();
                if (player != null) {
                    Projectile.SmoothHomingBehavior(player.Center + (Projectile.whoAmI % 6 * 0.2f).ToRotationVector2() * 60, 1f, 0.1f);
                }
            }
        }
        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Color drawColor = lightColor * (Projectile.alpha / 255f);
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

    internal class JiangcenFireBall : ModProjectile
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
                if (proj.type != ModContent.ProjectileType<JiangcenFireBall>()) {
                    continue;
                }
                proj.Kill();
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    int dustType = DustID.Shadowflame;
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

    internal class JiangcenSky : CustomSky
    {
        private bool active;
        private float intensity;
        private const float maxIntensity = 0.6f;
        private Color skyColor;
        internal static string name;
        public static void LoadInstance() {
            name = "AncientChineseMythology:JiangcenSky";
            SkyManager.Instance[name] = new JiangcenSky();
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
            NPC boss = GetBoss();
            Vector2 pullShake = Vector2.Zero;

            if (boss != null) {
                //更混乱的抖动：随时间波动 + 向Boss拉扯
                float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;
                Vector2 jitter = new Vector2(
                    (float)Math.Sin(time * 6f),
                    (float)Math.Cos(time * 4.2f)
                ) * (1.5f * intensity);

                pullShake = (boss.Center - Main.LocalPlayer.Center)
                    .SafeNormalize(Vector2.Zero) * (2f * intensity) + jitter;
            }

            //呼吸式亮度波动
            float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f);
            Color finalColor = skyColor * (intensity * pulse);

            spriteBatch.Draw(
                VaultAsset.placeholder2.Value,
                new Rectangle((int)pullShake.X, (int)pullShake.Y, Main.screenWidth, Main.screenHeight),
                finalColor
            );
        }

        public override void Update(GameTime gameTime) {
            NPC boss = GetBoss();
            if (boss != null) {
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);

                //非线性渐变，更突兀的近距离颜色变化
                t *= t;

                //三段式 + 深红脉动
                Color nearRed = new Color(160, 0, 20);
                if (Main.GlobalTimeWrappedHourly % 1f < 0.5f)
                    nearRed = Color.Lerp(nearRed, new Color(200, 20, 40), 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f));

                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(15, 8, 30),    //深紫
                    new Color(20, 50, 50),   //冷蓝绿
                    nearRed                  //血红脉动
                );

                //强度更快进入
                intensity = MathHelper.Min(maxIntensity, intensity + 0.02f);

                active = true;
            }
            else {
                intensity = MathHelper.Max(0f, intensity - 0.015f);
                if (intensity <= 0f) {
                    Deactivate();
                }
            }
        }

        //方便调用的获取Boss方法
        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Jiangcen>())
                    return npc;
            }
            return null;
        }
    }
}
