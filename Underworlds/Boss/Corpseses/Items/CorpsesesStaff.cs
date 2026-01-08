using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses.Items
{
    /// <summary>
    /// 枉死千骸法杖 - 召唤Boss的幽灵手掌跟随并攻击敌人
    /// </summary>
    internal class CorpsesesStaff : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 2685;
            Item.DamageType = DamageClass.Summon;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<CorpsesesHandMinion>();
            Item.shootSpeed = 0f;
            Item.mana = 10;
            Item.noMelee = true;
            Item.buffType = ModContent.BuffType<CorpsesesHandMinionBuff>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 清除旧召唤物
            player.AddBuff(Item.buffType, 2);

            // 召唤新的手掌
            var projectile = Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            return false;
        }

        public override void AddRecipes() {
            CreateRecipe().AddIngredient(ModContent.ItemType<Corpsefragments>(), 12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 幽灵手掌召唤物Buff
    /// </summary>
    public class CorpsesesHandMinionBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<CorpsesesHandMinion>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>
    /// 幽灵手掌召唤物 - 跟随玩家并自动攻击
    /// </summary>
    public class CorpsesesHandMinion : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Boss/Corpseses/CorpsesHand";

        private enum MinionState
        {
            Idle,       // 空闲跟随
            Targeting,  // 锁定目标
            Slapping,   // 拍击攻击
            Retracting  // 回收
        }

        private MinionState State {
            get => (MinionState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float AttackCooldown => ref Projectile.localAI[0];

        private NPC targetNPC;
        private Vector2 idlePosition;
        private Vector2 slapStartPos;
        private Vector2 slapTargetPos;
        private float slapProgress;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            Main.projPet[Projectile.type] = true;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            // 检查玩家状态
            if (!player.active || player.dead) {
                player.ClearBuff(ModContent.BuffType<CorpsesesHandMinionBuff>());
                return;
            }

            // 维持Buff
            if (player.HasBuff(ModContent.BuffType<CorpsesesHandMinionBuff>())) {
                Projectile.timeLeft = 2;
            }

            StateTimer++;
            if (AttackCooldown > 0) AttackCooldown--;

            // 根据状态执行AI
            switch (State) {
                case MinionState.Idle:
                    HandleIdleState(player);
                    break;
                case MinionState.Targeting:
                    HandleTargetingState(player);
                    break;
                case MinionState.Slapping:
                    HandleSlappingState(player);
                    break;
                case MinionState.Retracting:
                    HandleRetractingState(player);
                    break;
            }

            // 产生暗影粒子
            if (Main.rand.NextBool(10)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.5f;
            }
        }

        private void HandleIdleState(Player player) {
            // 计算空闲位置（在玩家周围盘旋）
            float orbitAngle = Main.GlobalTimeWrappedHourly * 2f + Projectile.whoAmI * MathHelper.PiOver2;
            float orbitRadius = 100f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 20f;

            idlePosition = player.Center + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle)) * orbitRadius;

            // 平滑移动到空闲位置
            Vector2 toIdle = idlePosition - Projectile.Center;
            float speed = MathHelper.Clamp(toIdle.Length() * 0.15f, 2f, 20f);

            if (toIdle.Length() > 10f) {
                Projectile.velocity = toIdle.SafeNormalize(Vector2.Zero) * speed;
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            // 手掌旋转跟随速度方向
            if (Projectile.velocity.Length() > 1f) {
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation,
                    Projectile.velocity.ToRotation(), 0.2f);
            }

            // 寻找目标
            if (AttackCooldown <= 0) {
                targetNPC = FindTarget(player, 600f);

                if (targetNPC != null) {
                    State = MinionState.Targeting;
                    StateTimer = 0;
                    AttackCooldown = 0;
                }
            }
        }

        private void HandleTargetingState(Player player) {
            // 检查目标有效性
            if (targetNPC == null || !targetNPC.active || targetNPC.life <= 0) {
                State = MinionState.Idle;
                StateTimer = 0;
                return;
            }

            // 快速移动到攻击位置（目标上方）
            Vector2 attackPos = targetNPC.Center + new Vector2(0, -150);
            Vector2 toAttackPos = attackPos - Projectile.Center;

            if (toAttackPos.Length() > 50f) {
                Projectile.velocity = toAttackPos.SafeNormalize(Vector2.Zero) * 25f;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            else {
                // 到位，准备拍击
                State = MinionState.Slapping;
                StateTimer = 0;
                slapStartPos = Projectile.Center;
                slapTargetPos = targetNPC.Center;
                slapProgress = 0f;
                Projectile.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, Projectile.Center);
            }

            // 超时保护
            if (StateTimer > 60) {
                State = MinionState.Idle;
                StateTimer = 0;
            }
        }

        private void HandleSlappingState(Player player) {
            // 检查目标有效性
            if (targetNPC == null || !targetNPC.active) {
                State = MinionState.Retracting;
                StateTimer = 0;
                return;
            }

            // 更新拍击目标位置（追踪移动的敌人）
            if (slapProgress < 0.5f) {
                slapTargetPos = targetNPC.Center;
            }

            // 快速下拍
            slapProgress += 0.12f;
            float easeProgress = ACMUtils.QuadInOut(slapProgress);

            Projectile.Center = Vector2.Lerp(slapStartPos, slapTargetPos, easeProgress);
            Projectile.rotation = (slapTargetPos - slapStartPos).ToRotation();

            // 拍击产生粒子
            if (slapProgress > 0.4f && slapProgress < 0.8f && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.5f;
            }

            // 拍击瞬间产生冲击效果
            if (StateTimer == 8) {
                for (int i = 0; i < 15; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                    int dust = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height,
                        DustID.PurpleTorch, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }

                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
            }

            if (slapProgress >= 1f) {
                State = MinionState.Retracting;
                StateTimer = 0;
                AttackCooldown = 40;
                targetNPC = null;
            }
        }

        private void HandleRetractingState(Player player) {
            // 返回到玩家附近
            Vector2 returnPos = player.Center + new Vector2(0, -100);
            Vector2 toReturn = returnPos - Projectile.Center;

            if (toReturn.Length() > 50f) {
                Projectile.velocity = toReturn.SafeNormalize(Vector2.Zero) * 20f;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            else {
                State = MinionState.Idle;
                StateTimer = 0;
            }

            // 超时保护
            if (StateTimer > 40) {
                State = MinionState.Idle;
                StateTimer = 0;
            }
        }

        private NPC FindTarget(Player player, float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            // 优先选择玩家的鼠标目标
            if (player.HasMinionAttackTargetNPC) {
                NPC targeted = Main.npc[player.MinionAttackTargetNPC];
                if (targeted.active && targeted.CanBeChasedBy() && !targeted.friendly) {
                    float dist = Vector2.Distance(targeted.Center, Projectile.Center);
                    if (dist < maxDistance * 1.5f) // 鼠标目标范围更大
                    {
                        return targeted;
                    }
                }
            }

            // 寻找最近的敌人
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override bool? CanDamage() {
            // 只在拍击阶段造成伤害
            return State == MinionState.Slapping && slapProgress > 0.5f && slapProgress < 0.9f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = texture.Size() / 2f;

            // 根据速度方向决定翻转
            SpriteEffects effects = Projectile.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 半透明幽灵效果
            Color drawColor = new Color(180, 80, 255, 180);

            // 发光层（3层叠加）
            for (int i = 0; i < 3; i++) {
                Vector2 offset = new Vector2(
                    MathF.Cos(Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / 3f),
                    MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / 3f)) * 4f;

                Color glowColor = new Color(150, 50, 200, 0) * 0.5f;
                Main.EntitySpriteDraw(texture, Projectile.Center + offset - Main.screenPosition, null,
                    glowColor, Projectile.rotation, origin, Projectile.scale * 1.1f, effects);
            }

            // 主体
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                drawColor, Projectile.rotation, origin, Projectile.scale, effects);

            // 攻击时的冲击波效果
            if (State == MinionState.Slapping && slapProgress > 0.6f && slapProgress < 0.8f) {
                float shockwaveScale = MathHelper.Lerp(0.5f, 1.5f, (slapProgress - 0.6f) / 0.2f);
                Color shockColor = new Color(180, 80, 255, 0) * (1f - (slapProgress - 0.6f) / 0.2f);

                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                    shockColor, Projectile.rotation, origin, Projectile.scale * shockwaveScale, effects);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 消失时的粒子效果
            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 2f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(5, 5);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}

