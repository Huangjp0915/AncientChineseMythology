using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent; // TextureAssets.Gore
using Terraria.DataStructures;
using AncientChineseMythology.Tiles.Placable;
using AncientChineseMythology.Biomes;

namespace AncientChineseMythology.NPCs.Monsters
{
    /// <summary>
    /// Chang Ghost —— 长鬼。
    /// * 仅在 BloodSeaBiome 的陆地或海底生成。
    /// * 若实体<strong>自身</strong>离开 BloodSeaBiome，立即死亡（StrikeInstantKill）。
    /// * 行走动画 6 帧，自定义追击 AI，死亡爆散自定义骨块。
    /// </summary>
    public class ChangGhost : ModNPC
    {
        // ──────────────────── 常量 ────────────────────
        private const int FrameCount    = 6;
        private const int FrameDuration = 6;
        private const float MoveSpeed   = 0.8f;
        private const float JumpStrength = 6f;
        private const float MaxFallSpeed = 6f;
        private const int   DamagePerTickOutside = 2; // 血海外每 tick 失去生命值

        // BloodSea 检测半径 & 阈值 —— MUST match BloodSeaBiome.IsBiomeActive(tileCount)
        private const int ScanRadiusX   = 50;  // tiles
        private const int ScanRadiusY   = 45;  // tiles
        private const int RequiredTiles = 50; // same as ModPlayer count threshold

        private int frameTimer;

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Monsters/ChangGhost/Walk";

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = FrameCount;
        }

        public override void SetDefaults()
        {
            NPC.width  = 34;
            NPC.height = 46;
            NPC.damage = 88;
            NPC.defense = 50;
            NPC.lifeMax = 1200;
            NPC.knockBackResist = 0.4f;
            NPC.value = 180f;

            NPC.aiStyle = -1; // custom AI
            NPC.noGravity = false;
            NPC.noTileCollide = false;

            NPC.HitSound   = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath2;
        }

        // ──────────────────── 生成 ────────────────────
        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            bool inBloodSea = spawnInfo.Player.InModBiome(ModContent.GetInstance<BloodSeaBiome>());
            if (!inBloodSea || spawnInfo.PlayerSafe) return 0f;

            // 生成点必须有实心地面（陆地或水底陆地）且处于地表层
            bool solidBelow  = WorldGen.SolidTile(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY + 1);
            bool validGround = solidBelow;
            bool isSurface   = spawnInfo.SpawnTileY < Main.worldSurface;

            return validGround && isSurface ? 0.25f : 0f;
        }

        // ──────────────────── AI ────────────────────
        public override void AI()
        {
            // ① 血海生存检测 —— 离开后持续掉血
            if (!IsInBloodSea())
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // 每 tick 扣血，直至死亡
                    NPC.life -= DamagePerTickOutside;
                    if (NPC.life <= 0)
                    {
                        if (NPC.life <= 0 && Main.netMode != NetmodeID.Server)
                        {
                            var pos = NPC.position;
                            var vel = NPC.velocity * 0.4f;
                            Gore.NewGore(NPC.GetSource_Death(), pos, vel, ModContent.GoreType<Gores.ChangGhost.ChangGhost_HeadGore>());
                            Gore.NewGore(NPC.GetSource_Death(), pos, vel, ModContent.GoreType<Gores.ChangGhost.ChangGhost_ArmGore>());
                            Gore.NewGore(NPC.GetSource_Death(), pos, vel, ModContent.GoreType<Gores.ChangGhost.ChangGhost_LegGore>());
                        }
                        NPC.StrikeInstantKill();
                        return; // 死亡
                    }
                }

                // 生成一点骨尘提示正在腐化
                if (Main.rand.NextBool(4))
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, 0f, -1.2f);
            }

            // ② 追击逻辑
            NPC.TargetClosest(true);
            Player player = NPC.HasPlayerTarget ? Main.player[NPC.target] : null;
            if (player == null || !player.active || player.dead)
            {
                NPC.velocity.X *= 0.7f;
                return;
            }

            NPC.direction = NPC.spriteDirection = player.Center.X < NPC.Center.X ? -1 : 1;
            NPC.velocity.X = NPC.direction * MoveSpeed;

            if (NPC.collideX && NPC.velocity.Y == 0f)
                NPC.velocity.Y = -JumpStrength;

            if (NPC.velocity.Y > MaxFallSpeed)
                NPC.velocity.Y = MaxFallSpeed;
        }

        // ──────────────────── 血海判定（基于 Tile 扫描） ────────────────────
        private bool IsInBloodSea()
        {
            // NOTE: 确保下列 Tile/Wall ID 与 BloodSeaBiome.UpdateBiomes 中使用的一致！
            int bloodTiles = 0;
            Point tileCenter = NPC.Center.ToTileCoordinates();
            for (int i = -ScanRadiusX; i <= ScanRadiusX; i++)
            {
                int x = tileCenter.X + i;
                if (x < 10 || x >= Main.maxTilesX - 10) continue; // 边界保护

                for (int j = -ScanRadiusY; j <= ScanRadiusY; j++)
                {
                    int y = tileCenter.Y + j;
                    if (y < 10 || y >= Main.maxTilesY - 10) continue;

                    Tile tile = Framing.GetTileSafely(x, y);

                    bool isBloodTile = tile.TileType == ModContent.TileType<BloodSeaSand>();
                    if (isBloodTile)
                    {
                        if (++bloodTiles >= RequiredTiles)
                            return true;
                    }
                }
            }
            return false;
        }

        // ──────────────────── 动画 ────────────────────
        public override void FindFrame(int frameHeight)
        {
            if (++frameTimer >= FrameDuration)
            {
                frameTimer = 0;
                NPC.frame.Y += frameHeight;
                if (NPC.frame.Y >= frameHeight * FrameCount)
                    NPC.frame.Y = 0;
            }
        }

        // ──────────────────── 受击 / 死亡视觉 ────────────────────
        public override void HitEffect(NPC.HitInfo hit)
        {
            // 受击时骨尘
            for (int i = 0; i < 5; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, hit.HitDirection, -1f);

            // 死亡时客户端生成 Gore（包括因血海外腐化死亡）
            if (NPC.life <= 0 && Main.netMode != NetmodeID.Server)
            {
                var pos = NPC.position;
                var vel = NPC.velocity * 0.4f;
                Gore.NewGore(NPC.GetSource_Death(), pos, vel, ModContent.GoreType<Gores.ChangGhost.ChangGhost_HeadGore>());
                Gore.NewGore(NPC.GetSource_Death(), pos, vel, ModContent.GoreType<Gores.ChangGhost.ChangGhost_ArmGore>());
                Gore.NewGore(NPC.GetSource_Death(), pos, vel, ModContent.GoreType<Gores.ChangGhost.ChangGhost_LegGore>());
            }
        }
    }
}
