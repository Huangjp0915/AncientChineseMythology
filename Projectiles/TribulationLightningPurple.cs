using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace AncientChineseMythology.Projectiles
{
    public class TribulationLightningPurple : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private int timerCount = 0;
        private Vector2 position;
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.hostile = true; //敌方伤害
            Projectile.width = 10; //弹幕宽度
            Projectile.height = 10; //弹幕高度
            Projectile.friendly = false; //友方弹幕
            Projectile.tileCollide = true; //不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Default; //伤害类型
            Projectile.penetrate = 6; //穿透
            Projectile.ignoreWater = true; //无视液体
            Projectile.timeLeft = 10; //存在时间，单位为帧
            Projectile.alpha = 30; //透明度
            Projectile.light = 0.25f; //发光亮度
            Projectile.usesLocalNPCImmunity = true; //独立无敌帧
            Projectile.localNPCHitCooldown = 10; //独立无敌帧时间
            base.SetDefaults();
        }

        private readonly List<LightTreePurple> trees = new();

        [Obsolete]
        public override void AI() {
            timerCount++;
            if (timerCount <= 0) return;
            timerCount = 0;                       //若想冷却就把判定改成 >= n

            //取劫云实例
            if (Projectile.ai[0] < 0 || Projectile.ai[0] >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }
            NPC boss = Main.npc[(int)Projectile.ai[0]];
            if (!boss.active) {
                Projectile.Kill();
                return;
            }

            //把闪电根节点放在劫云中心
            Vector2 origin = boss.Center;

            //搜索劫云 600px 范围内的所有存活玩家
            const float StrikeRadius = 600f;
            trees.Clear();
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player plr = Main.player[i];
                if (!plr.active || plr.dead) continue;
                if (Vector2.Distance(plr.Center, boss.Center) > StrikeRadius) continue;

                //每名玩家生成一棵树状闪电
                LightTreePurple t = new LightTreePurple(Main.rand, origin, Projectile.originalDamage, plr);
                t.Generate(plr.Center);
                trees.Add(t);
            }

            //没有玩家就不生成闪电
            if (trees.Count == 0) return;

            //固定投射物位置供 PostDraw 使用
            Projectile.position = origin;
        }
        public override void PostDraw(Color lightColor) {
            foreach (var t in trees)
                t.Draw(Main.spriteBatch, Projectile.Center - Main.screenPosition,
                    Projectile.velocity, Vector2.Zero);
        }
        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
        private NPC FindClosestNPC(Vector2 position, float maxRange) {
            NPC closestNPC = null;
            float closestDistance = maxRange;

            //遍历所有活跃的 NPC
            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.friendly && npc.CanBeChasedBy()) {
                    float distanceToNPC = Vector2.Distance(position, npc.Center);

                    //如果 NPC 在范围内且距离更近，更新目标
                    if (distanceToNPC < closestDistance) {
                        closestDistance = distanceToNPC;
                        closestNPC = npc;
                    }
                }
            }

            return closestNPC; //返回最近的敌人，如果没有找到则返回 null
        }
    }
    public class LightTreePurple
    {
        private int cnt;
        private class Node
        {
            public float rad, size, length;
            public List<Node> children;
            public Node(float rad, float size, float length) {
                this.rad = rad;
                this.size = size;
                this.length = length;
                this.children = new List<Node>();
            }
        };

        private Node root;
        private UnifiedRandom random;
        private Vector2 position;
        private float Damage;
        private bool isHit = false;
        private Player owner;

        public LightTreePurple(UnifiedRandom random, Vector2 position, float damage, Player owner) {
            cnt = 0;
            root = null;
            this.random = random;
            this.position = position;
            this.Damage = damage;
            this.owner = owner;
        }

        [Obsolete]
        private void ApplyDamage(Vector2 position, float damage) {
            foreach (Player plr in Main.player) {
                if (!plr.active || plr.dead) continue;

                if (Vector2.Distance(position, plr.Center) >= 40f) continue;
                //屏幕震动
                PunchCameraModifier mod = new(plr.Center,
                    Main.rand.NextVector2Circular(1f, 1f), .6f, .6f, 1, 1f);
                Main.instance.CameraModifiers.Add(mod);

                //对玩家造成伤害
                int hitDir = (plr.Center.X > position.X) ? 1 : -1;

                //使用 ByCustomReason —— 绝不会访问投射物名称表
                PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                    $"{plr.name} 被劫云的天雷劈成了灰烬。");

                plr.Hurt(reason, (int)damage, hitDir);

                CreateExplosionParticles(plr.Center);
            }
        }

        private void CreateExplosionParticles(Vector2 center) {
            int particleCount = 10; //粒子数量
            for (int i = 0; i < particleCount; i++) {
                //随机生成粒子的扩散方向
                Vector2 velocity = Main.rand.NextVector2Circular(12f, 12f); //粒子速度范围

                //创建粒子
                Dust dust = Dust.NewDustPerfect(center, DustID.PurpleTorch, velocity, 150, new Color(200, 80, 255), 1.4f);
                dust.noGravity = true; //禁用重力
                dust.fadeIn = 1f;      //设置淡入效果
                dust.scale = 1.2f;     //设置粒子大小
            }
        }

        private float rand() {
            double u = -2 * Math.Log(random.NextDouble());
            double v = 2 * Math.PI * random.NextDouble();
            return (float)Math.Max(0, Math.Sqrt(u) * Math.Cos(v) * 0.3 + 0.5);
        }

        private float rand(float range) {
            return random.NextFloatDirection() * range;
        }

        [Obsolete]
        public void Generate(Vector2 targetPosition) {
            //计算从初始位置到目标位置的方向
            Vector2 directionToTarget = targetPosition - position;

            //初始化根节点
            root = new Node(0, 1f, rand() * 50f);

            //构建树结构
            root = _build(root, directionToTarget, Main.rand.NextBool(), position);
        }

        [Obsolete]
        private Node _build(Node node, Vector2 directionToTarget, bool isMain, Vector2 currentPosition) {
            cnt++;
            //终止条件：节点位置与目标位置小于一定值时终止
            if (node.size < 0.1f || node.length < 2) return node;

            float r = isMain ? MathHelper.Pi / 12f : MathHelper.Pi / 6f;
            //计算朝向目标的方向
            float targetAngle = directionToTarget.ToRotation();
            //为每个节点生成一个小的随机偏角
            float randomAngleOffset = rand(r);
            Node main = new Node(targetAngle + randomAngleOffset, node.size * 0.96f, node.length);
            Vector2 newPosition = currentPosition + (targetAngle + randomAngleOffset).ToRotationVector2() * node.length;

            //检测节点是否与目标NPC碰撞
            ApplyDamage(newPosition, Damage); //对范围内的所有NPC造成伤害

            if (Main.rand.NextBool(10))
                //在节点处生成 Terra 粒子
                CreateTerraParticle(newPosition);

            node.children.Add(_build(main, directionToTarget, isMain, newPosition));

            //只有较小的几率出分支
            if (rand() > 0.98f) {
                //生成分支时，方向与主干方向相同，但大小变化很大
                float branchRandomAngleOffset = rand(MathHelper.Pi / 3f);
                Node child = new Node(targetAngle + branchRandomAngleOffset, node.size * 0.2f, node.length);
                Vector2 branchPosition = currentPosition + (targetAngle + branchRandomAngleOffset).ToRotationVector2() * node.length;

                //检测分支节点是否与目标NPC碰撞
                ApplyDamage(branchPosition, Damage); //对范围内的所有NPC造成伤害

                if (Main.rand.NextBool(10))
                    //在分支节点处生成 Terra 粒子
                    CreateTerraParticle(branchPosition);

                node.children.Add(_build(child, directionToTarget, false, branchPosition));
            }

            return node;
        }

        private void CreateTerraParticle(Vector2 position) {
            //创建 Terra 粒子
            Dust dust = Dust.NewDustPerfect(position, DustID.PurpleTorch, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f)), 100, new Color(200, 80, 255), 1f);
            dust.noGravity = true; //禁用重力
            dust.fadeIn = 1f;      //设置淡入效果
            dust.scale = 1f;     //设置粒子大小
        }


        public void Draw(SpriteBatch sb, Vector2 pos, Vector2 vel, Vector2 mousePos) {
            _draw(sb, pos, vel, root, mousePos);
        }

        private void _draw(SpriteBatch sb, Vector2 pos, Vector2 vel, Node node, Vector2 mousePos) {
            //计算从当前节点到鼠标的方向
            Vector2 directionToMouse = mousePos - pos + new Vector2(mousePos.X + 10000, -mousePos.Y);
            directionToMouse.Normalize(); //标准化方向向量

            //计算当前节点的方向向量
            Vector2 unit = (directionToMouse.ToRotation() + node.rad).ToRotationVector2();

            //绘制当前节点的线段
            for (float i = 0; i <= node.length; i += 0.04f) {
                sb.Draw(TextureAssets.BlackTile.Value, pos + unit * i + new Vector2(-30, 0), new Rectangle(0, 0, 1, 2), new Color(180, 70, 255), 0,
                    new Vector2(0.5f, 0.5f), Math.Max(node.size * 7, 0.2f), SpriteEffects.None, 0f);
            }

            //递归绘制子节点
            foreach (var child in node.children) {
                _draw(sb, pos + unit * node.length, unit, child, mousePos);
            }
        }
    }
}
