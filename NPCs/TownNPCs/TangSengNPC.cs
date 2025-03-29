using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Items;
using System;

namespace AncientChineseMythology.NPCs.TownNPCs
{
    [AutoloadHead]
    public class TangSengNPC : ModNPC
    {
        // 占位贴图，真正动画在 PreDraw 中管理
        public override string Texture => "AncientChineseMythology/Textures/Tangseng/TangSengNPC_Left";
        // 头像
        public override string HeadTexture => "AncientChineseMythology/Textures/Tangseng/TangSengNPC_Head";

        // 状态控制
        private bool isAttacking = false;  
        private float frameCounter = 0f; 
        private int moveTimer = 0;      // 走动计时
        private int idleTimer = 0;      // 停顿计时
        private int attackCooldown = 0; // 攻击冷却

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1; // 只占位
            NPCID.Sets.NoTownNPCHappiness[Type] = true;
        }

        public override void SetDefaults() {
            //缩小碰撞体积，让鼠标检测范围更贴合
            NPC.width = 16;
            NPC.height = 28;

            NPC.aiStyle = -1;       // 不用原版TownNPC AI
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.noGravity = false;  // 允许重力
            NPC.noTileCollide = false;

            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;

            TownNPCStayingHomeless = true;
        }

        public override bool CanTownNPCSpawn(int numTownNPCs) => true;
        public override bool CanChat() => true;

        public override List<string> SetNPCNameList() {
            return new List<string> { "唐僧" };
        }

        public override string GetChat() {
            string[] dialogues = {
                "我感应到人间妖气日盛，恐有截教之乱。",
                "封神大战的余波尚未平息，万望小心。",
                "多加留意那些“妖气碎片”，它们暗示着更大的阴谋……",
                "若想守护三界，先从一根小小的木棍开始吧……"
            };
            return dialogues[Main.rand.Next(dialogues.Length)];
        }

        // 设置两个按钮：第一个“帮助”，第二个“商店”或其他
        public override void SetChatButtons(ref string button, ref string button2) {
            button = "帮助";
            button2 = "商店"; 
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName) {
            Player player = Main.LocalPlayer;

            //点击时让 NPC 面向玩家
            NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;
            NPC.spriteDirection = NPC.direction;

            // 停止移动一段时间，让对话更自然
            moveTimer = 0;   
            idleTimer = 120; // 停顿约2秒

            if (firstButton) {
                // “帮助”逻辑（检测棍子进阶）
                var stickProgression = new (int itemType, string itemName, string craftHint)[]
                {
                    (ModContent.ItemType<WoodenStick>(), "木棍","你可以去地底找寻一些铁矿，或许对你有些帮助"),
                    (ModContent.ItemType<IronStick>(),   "铁棍", "想要更进一步的话，金灿灿的或许不错？"),
                    (ModContent.ItemType<GoldenStick>(), "金棍", "施主，嫌弃伤害不够？你可以在上面镶嵌一些东西试试"),
                    (ModContent.ItemType<GemStick>(),    "宝石棍","下一步或许你就该下地狱了，你不下地狱谁下地狱，阿弥陀佛"),
                    (ModContent.ItemType<RuyiStick>(),   "如意棍","夜晚域外生物会给你带来你想要的东西，远古的黑暗和光明还有天空也会有你想要的东西"),
                    (ModContent.ItemType<TrueRuyiStick>(),"真·如意棍","或许你需要把那个邪恶的教徒召唤的东西干掉，阿弥陀佛"),
                    (ModContent.ItemType<RuyiJinguBang>(),"如意金箍棒","或者你可以去海边钓鱼试试，说不定有大货")
                };

                int highestIndex = -1;
                for (int i = 0; i < stickProgression.Length; i++) {
                    int type = stickProgression[i].itemType;
                    if (player.inventory.Any(item => item != null && item.type == type)) {
                        highestIndex = i;
                    }
                }

                if (highestIndex == -1) {
                    Main.npcChatText = "你还没有任何棍子，要不要找我拿一根呢？";
                }
                else {
                    var (curID, curName, curHint) = stickProgression[highestIndex];
                    if (highestIndex == stickProgression.Length - 1)
                    {
                        Main.npcChatText = $"你已经有这根棍子了还不满足吗？再往上可就得找那些神仙了！{stickProgression[highestIndex].craftHint}";
                    }
                    else {
                        var (nextID, nextName, nextHint) = stickProgression[highestIndex + 1];
                        Main.npcChatText = $"你现在有“{curName}”，下一步可以合成“{nextName}”。\n{nextHint}";
                    }
                }
            }
            else {
                //点击“商店” => 打开商店
                shopName = "TangSengShop";
            }
            Main.player[Main.myPlayer].SetTalkNPC(NPC.whoAmI);
        }

        // 定义专属商店
        public override void AddShops() {
            new NPCShop(Type, "TangSengShop")
                .Add<WoodenStick>()
                .Add<IronStick>()
                // 也可加更多物品
                .Register();
        }

        public override void AI() {
            // ============= 检测附近怪物 => 是否攻击 =============
            bool nearEnemy = false;
            float detectRange = 80f; // 范围小些
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (other.active && !other.friendly && other.damage > 0) {
                    float dist = Vector2.Distance(NPC.Center, other.Center);
                    if (dist < detectRange) {
                        nearEnemy = true;
                        break;
                    }
                }
            }
            isAttacking = nearEnemy;

            // ============= 简易走动逻辑 =============
            if (idleTimer > 0) {
                // NPC 在“停顿”状态
                idleTimer--;
                NPC.velocity.X = 0f;
            }
            else {
                // NPC 正常随机行走
                if (moveTimer > 0) {
                    NPC.velocity.X = 0.8f * NPC.direction;
                    moveTimer--;
                }
                else {
                    // 切换到下一阶段
                    if (Main.rand.NextBool()) {
                        // 停顿 1~2秒
                        idleTimer = Main.rand.Next(60, 120);
                    }
                    else {
                        // 行走 1~3秒，并随机方向
                        moveTimer = Main.rand.Next(60, 180);
                        NPC.direction = (Main.rand.NextBool() ? 1 : -1);
                    }
                }
            }

            // ============= 无地面 -> 转向 =============
            Vector2 front = NPC.Center + new Vector2(NPC.direction * (NPC.width / 2 + 2), 20f);
            // 若前方没地，转向
            if (!WorldGen.SolidTile((int)front.X / 16, (int)front.Y / 16)) {
                NPC.direction *= -1;
            }

            // =============  跳跃逻辑（跨1~2格台阶） =============
            if (NPC.collideY) {
                // 在地面上时，检查前方略高处
                Vector2 blockCheckPos = NPC.Center + new Vector2(NPC.direction * (NPC.width / 2 + 2), -8f);
                Tile tileAhead = Framing.GetTileSafely((int)blockCheckPos.X / 16, (int)blockCheckPos.Y / 16);
                if (tileAhead.HasTile && Main.tileSolid[tileAhead.TileType]) {
                    NPC.velocity.Y = -5f; // 跳一下
                }
            }

            NPC.spriteDirection = NPC.direction;

            // ============= 攻击判定 =============
            if (isAttacking) {
                if (attackCooldown <= 0) {
                    // 攻击范围(小方块)
                    int hitboxWidth = 20;
                    int hitboxHeight = 20;
                    Rectangle attackHitbox;
                    if (NPC.spriteDirection == 1) {
                        attackHitbox = new Rectangle((int)NPC.Right.X, (int)(NPC.Center.Y - hitboxHeight / 2), hitboxWidth, hitboxHeight);
                    }
                    else {
                        attackHitbox = new Rectangle((int)(NPC.Left.X - hitboxWidth), (int)(NPC.Center.Y - hitboxHeight / 2), hitboxWidth, hitboxHeight);
                    }

                    // 对范围内敌人造成伤害
                    foreach (NPC target in Main.npc) {
                        if (target.active && !target.friendly && target.lifeMax > 5 && !target.dontTakeDamage) {
                            if (attackHitbox.Intersects(target.Hitbox)) {
                                int damage = 50;
                                float knockBack = 3f;
                                int hitDirection = NPC.spriteDirection;

                                NPC.HitInfo hitInfo = new NPC.HitInfo {
                                    Damage = damage,
                                    Knockback = knockBack,
                                    HitDirection = hitDirection,
                                    Crit = false
                                };
                                target.StrikeNPC(hitInfo, false, false);
                            }
                        }
                    }
                    attackCooldown = 30;
                }
                else {
                    attackCooldown--;
                }
            }
            else {
                attackCooldown = 0;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //选用贴图
            Texture2D texture;
            int totalFrames;
            bool isMoving = (Math.Abs(NPC.velocity.X) > 0.1f);

            if (isAttacking) {
                texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Tangseng/TangSengNPC_Attack").Value;
                totalFrames = 2;
            }
            else if (isMoving) {
                // 根据 spriteDirection 选择左右贴图
                if (NPC.spriteDirection == -1) {
                    texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Tangseng/TangSengNPC_Left").Value;
                }
                else {
                    texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Tangseng/TangSengNPC_Right").Value;
                }
                totalFrames = 4;
            }
            else {
                // 静止 => 用右贴图第0帧
                texture = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Tangseng/TangSengNPC_Right").Value;
                totalFrames = 4;
            }

            //动画帧
            int frameWidth = texture.Width / totalFrames;
            int frameHeight = texture.Height;
            float animSpeed = 5f;

            if (isMoving || isAttacking) {
                frameCounter += 1f;
            }
            else {
                frameCounter = 0f;
            }

            int currentFrame = (int)(frameCounter / animSpeed) % totalFrames;
            if (!isMoving && !isAttacking) {
                currentFrame = 0;
            }
            Rectangle sourceRect = new Rectangle(currentFrame * frameWidth, 0, frameWidth, frameHeight);

            //绘制坐标 => 以 NPC.Bottom 作为“脚”
            Vector2 drawPos = NPC.Bottom - screenPos;
            Vector2 origin = new Vector2(frameWidth / 2f, frameHeight);
            drawPos.Y += NPC.gfxOffY; // 如果还略浮空，可在此加/减

            //翻转
            SpriteEffects effects = (NPC.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //画
            spriteBatch.Draw(texture, drawPos, sourceRect, drawColor, NPC.rotation, origin, 1.5f, effects, 0f);

            return false;
        }
    }
}
