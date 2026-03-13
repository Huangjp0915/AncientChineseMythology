using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Enemys
{
    /// <summary>
    /// �ظ��й�����ϵͳ
    /// �����ظ�����������йֵ�����
    /// </summary>
    public class UnderworldEnemySpawnSystem : ModSystem
    {
        #region ��������
        /// <summary>���ɼ������֡��</summary>
        private const int SpawnCheckInterval = 90;

        /// <summary>���ͬʱ���ڵĵظ��й�����</summary>
        private const int MaxUnderworldEnemies = 10;

        /// <summary>ÿ�����ɵ��������</summary>
        private const int MaxSpawnPerCheck = 2;

        /// <summary>���ɾ��뷶Χ����С��</summary>
        private const float MinSpawnDistance = 500f;

        /// <summary>���ɾ��뷶Χ�����</summary>
        private const float MaxSpawnDistance = 1000f;

        /// <summary>�ظ������ֵ��������ײ�Ϊ��׼��</summary>
        private const int UnderworldDepthFromBottom = 200;
        #endregion

        #region ״̬
        private int spawnTimer = 0;
        #endregion

        public override void PostUpdateWorld() {
            // �������˴�������
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            spawnTimer++;
            if (spawnTimer < SpawnCheckInterval) return;
            spawnTimer = 0;

            // ��ÿ����Ҽ������
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.active) continue;

                // �������Ƿ��ڵظ�������
                if (!IsInUnderworldRegion(player)) continue;

                // ��鵱ǰ�ظ��й�����
                int currentEnemyCount = CountUnderworldEnemies();
                if (currentEnemyCount >= MaxUnderworldEnemies) continue;

                // �������ɵй�
                TrySpawnEnemies(player, MaxUnderworldEnemies - currentEnemyCount);
            }
        }

        /// <summary>
        /// ���λ���Ƿ��ڵظ�����
        /// </summary>
        public static bool IsInUnderworldRegion(Player player) => UnderworldFogEffect.IsActive(player);

        /// <summary>
        /// ͳ�Ƶ�ǰ���ڵĵظ��й�����
        /// </summary>
        private static int CountUnderworldEnemies() {
            int count = 0;
            int yakshaType = ModContent.NPCType<Yaksha>();
            int deceasedType = ModContent.NPCType<TheDeceasedPerson>();
            int bonesType = ModContent.NPCType<ThebonesinTheTomb>();
            int harvesterType = ModContent.NPCType<SoulHarvester>();

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == yakshaType || npc.type == deceasedType ||
                    npc.type == bonesType || npc.type == harvesterType) {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// ����Ϊָ��������ɵй�
        /// </summary>
        private static void TrySpawnEnemies(Player player, int maxCount) {
            int spawned = 0;

            for (int attempt = 0; attempt < 15 && spawned < Math.Min(maxCount, MaxSpawnPerCheck); attempt++) {
                // ���ѡ������λ��
                Vector2 spawnPos = FindSpawnPosition(player);
                if (spawnPos == Vector2.Zero) continue;

                // ���ѡ��й�����
                int npcType = ChooseEnemyType(player);
                if (npcType == -1) continue;

                // ���ɵй�
                var source = new Terraria.DataStructures.EntitySource_SpawnNPC();
                int npcIndex = NPC.NewNPC(source, (int)spawnPos.X, (int)spawnPos.Y, npcType);
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                    NPC npc = Main.npc[npcIndex];
                    npc.target = player.whoAmI;

                    // ��������Ч��
                    SpawnEffect(spawnPos, npcType);

                    spawned++;

                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Ѱ�Һ��ʵ�����λ��
        /// </summary>
        private static Vector2 FindSpawnPosition(Player player) {
            for (int i = 0; i < 25; i++) {
                // �������;���
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(MinSpawnDistance, MaxSpawnDistance);
                Vector2 offset = angle.ToRotationVector2() * distance;
                Vector2 testPos = player.Center + offset;

                // ����Ƿ������緶Χ��
                int tileX = (int)(testPos.X / 16f);
                int tileY = (int)(testPos.Y / 16f);

                if (tileX < 50 || tileX > Main.maxTilesX - 50) continue;
                if (tileY < 50 || tileY > Main.maxTilesY - 50) continue;

                // ����Ƿ��ڵظ�������
                if (!IsInUnderworldRegion(player)) continue;

                // ������ɵ��Ƿ���Ч�����ڷ����ڣ�
                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;

                // ȷ����һ���ռ�
                bool hasSpace = true;
                for (int checkY = -2; checkY <= 2; checkY++) {
                    for (int checkX = -1; checkX <= 1; checkX++) {
                        int cx = tileX + checkX;
                        int cy = tileY + checkY;
                        if (cx < 0 || cx >= Main.maxTilesX || cy < 0 || cy >= Main.maxTilesY) {
                            hasSpace = false;
                            break;
                        }
                        Tile checkTile = Main.tile[cx, cy];
                        if (checkTile.HasTile && Main.tileSolid[checkTile.TileType]) {
                            hasSpace = false;
                            break;
                        }
                    }
                    if (!hasSpace) break;
                }

                if (!hasSpace) continue;

                return testPos;
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// ������ҽ���ѡ��й�����
        /// </summary>
        private static int ChooseEnemyType(Player player) {
            // ������Ϸ���ȵ������ɸ���
            bool hardMode = Main.hardMode;
            bool postPlantera = NPC.downedPlantBoss;
            bool postMoonLord = NPC.downedMoonlord;

            int roll = Main.rand.Next(100);

            if (postMoonLord) {
                // �������������ʹ�߸�����
                if (roll < 30) {
                    return ModContent.NPCType<SoulHarvester>();
                }
                else if (roll < 55) {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
                else if (roll < 80) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
            }
            else if (postPlantera) {
                // ����֮���󣺾���ֲ�
                if (roll < 25) {
                    return ModContent.NPCType<SoulHarvester>();
                }
                else if (roll < 50) {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
                else if (roll < 75) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
            }
            else if (hardMode) {
                // ����ģʽ�������й�Ϊ��
                if (roll < 10) {
                    return ModContent.NPCType<SoulHarvester>();
                }
                else if (roll < 35) {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
                else if (roll < 65) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
            }
            else {
                // ��ͨģʽ��ֻ���ɽ����ĵй�
                if (roll < 40) {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
                else if (roll < 80) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
            }
        }

        /// <summary>
        /// �����Ӿ�Ч��
        /// </summary>
        private static void SpawnEffect(Vector2 position, int npcType) {
            int dustType;
            Color dustColor;

            if (npcType == ModContent.NPCType<Yaksha>()) {
                dustType = DustID.Torch;
                dustColor = new Color(255, 100, 50);
            }
            else if (npcType == ModContent.NPCType<TheDeceasedPerson>()) {
                dustType = DustID.SpectreStaff;
                dustColor = new Color(100, 150, 255);
            }
            else if (npcType == ModContent.NPCType<ThebonesinTheTomb>()) {
                dustType = DustID.Bone;
                dustColor = new Color(200, 200, 180);
            }
            else {
                dustType = DustID.Shadowflame;
                dustColor = new Color(150, 80, 200);
            }

            // �İ���Ч
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                int dust = Dust.NewDust(position, 0, 0, dustType, vel.X, vel.Y, 100, dustColor, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            // �ڰ�����Ч��
            for (int i = 0; i < 4; i++) {
                Vector2 smokeVel = Main.rand.NextVector2Circular(2f, 2f);
                int smoke = Dust.NewDust(position, 0, 0, DustID.Smoke, smokeVel.X, smokeVel.Y, 150, Color.Black, 2f);
                Main.dust[smoke].noGravity = true;
            }
        }
    }

    /// <summary>
    /// �ظ�����ȫ��NPC�޸�
    /// ���ӵظ�����йֵĻ���������
    /// </summary>
    public class UnderworldEnemyGlobalNPC : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            // �ڵظ��������ӵй�������
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(player)) {
                // �������ɼ�������������ʣ�
                spawnRate = (int)(spawnRate * 0.7f);
                // ���������������
                maxSpawns = (int)(maxSpawns * 1.3f);
            }
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            // �ڵظ��������ӵظ��йֵ����ɳ�
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(spawnInfo.Player)) {
                // ������Ϸ���ȵ���
                bool hardMode = Main.hardMode;
                bool postPlantera = NPC.downedPlantBoss;

                // ���ӵظ��й�
                float baseRate = hardMode ? 0.15f : 0.08f;
                float rareRate = postPlantera ? 0.1f : 0.03f;

                pool[ModContent.NPCType<Yaksha>()] = baseRate;
                pool[ModContent.NPCType<TheDeceasedPerson>()] = baseRate * 1.2f;

                if (hardMode) {
                    pool[ModContent.NPCType<ThebonesinTheTomb>()] = baseRate * 0.8f;
                }

                if (postPlantera) {
                    pool[ModContent.NPCType<SoulHarvester>()] = rareRate;
                }
            }
        }
    }

    /// <summary>
    /// �ظ��й�����Ч��
    /// �ظ��й��ڵظ������ڻ������
    /// </summary>
    public class UnderworldEnemyBuff : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool isUnderworldEnemy = false;
        private float auraTimer = 0f;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return entity.type == ModContent.NPCType<Yaksha>() ||
                   entity.type == ModContent.NPCType<TheDeceasedPerson>() ||
                   entity.type == ModContent.NPCType<ThebonesinTheTomb>() ||
                   entity.type == ModContent.NPCType<SoulHarvester>();
        }

        public override void SetDefaults(NPC entity) {
            isUnderworldEnemy = true;
        }

        public override void AI(NPC npc) {
            if (!isUnderworldEnemy) return;

            auraTimer += 0.03f;
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) {
            if (!isUnderworldEnemy) return;

            // �ڵظ���������ɶ����˺�
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(Main.player[npc.target])) {
                modifiers.FinalDamage *= 1.1f;
            }
        }

        public override void OnKill(NPC npc) {
            if (!isUnderworldEnemy) return;

            // ��ɱ�ظ��йֵĶ��⽱��
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(Main.player[npc.target])) {
                // ���⾭������Ч��
                int dustType = DustID.Shadowflame;
                if (npc.type == ModContent.NPCType<Yaksha>()) {
                    dustType = DustID.Torch;
                }
                else if (npc.type == ModContent.NPCType<TheDeceasedPerson>()) {
                    dustType = DustID.SpectreStaff;
                }
                else if (npc.type == ModContent.NPCType<ThebonesinTheTomb>()) {
                    dustType = DustID.Bone;
                }

                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    int dust = Dust.NewDust(npc.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }
}
