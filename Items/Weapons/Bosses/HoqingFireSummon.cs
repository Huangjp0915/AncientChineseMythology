using AncientChineseMythology.NPCs.Boss.Hoqings;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Bosses
{
    public class HoqingFireSummon : ModItem
    {
        public override void SetDefaults() {
            Item.width = 60;
            Item.height = 60;
            Item.damage = 136;
            Item.mana = 10;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 2f;
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<HoqingFireSummonProj>();
            Item.shootSpeed = 10f;
            Item.DamageType = DamageClass.Summon;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 1, 60, 5);
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] == 0 || player.altFunctionUse == 2;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                foreach (var proj in Main.ActiveProjectiles) {
                    if (proj.type != type) {
                        continue;
                    }
                    proj.Kill();
                    proj.netUpdate = true;
                }
                return false;
            }
            for (int i = 0; i < 6; i++) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0, i);
            }
            return false;
        }
    }

    public class HoqingFireSummonProj : BaseHeldProj
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Hoqings/GhostFire";
        private int frame;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.timeLeft = 2;
            //轨道参数
            float orbitRadius = 100f + 20f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.5f + Projectile.ai[1]); //动态半径变化
            float orbitSpeed = 1.2f;     //转速
            float verticalRange = 30f;   //上下活动范围
            float wobbleStrength = 10f;  //左右/上下扰动
            float twistFrequency = 0.3f;

            float time = Main.GlobalTimeWrappedHourly;
            float angleOffset = Projectile.ai[1]; //唯一轨迹偏移
            float baseAngle = time * orbitSpeed + angleOffset;

            //基础旋转轨迹（绕Boss）
            Vector2 orbitPos = baseAngle.ToRotationVector2() * orbitRadius;

            //加入飘忽扰动（上下/左右浮动、微随机）
            float floatX = (float)Math.Sin(time * 2f + angleOffset * 2f) * wobbleStrength;
            float floatY = (float)Math.Cos(time * 1.5f + angleOffset * 3f) * verticalRange;

            Vector2 floatOffset = new Vector2(floatX, floatY);

            //模拟Z轴偏移（远近感）
            float scaleZ = 1.0f + 0.1f * (float)Math.Sin(time * twistFrequency + angleOffset);
            Projectile.scale = scaleZ;

            //最终目标位置 = Boss中心 + 旋转偏移 + 漂浮扰动
            Vector2 targetPos = Owner.GetPlayerStabilityCenter() + orbitPos + floatOffset;

            //平滑漂移过去，营造灵异感
            float inertia = 20f;
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 1f / inertia);

            //禁止旋转，强制朝向
            Projectile.rotation = 0f;

            Projectile.position += Owner.velocity;

            if (++Projectile.ai[2] > 60 + Projectile.ai[1] * 10) {
                Projectile.ai[2] = 0;

                if (Projectile.IsOwnedByLocalPlayer()) {
                    NPC target = Projectile.Center.FindClosestNPC(1800);
                    if (target != null) {
                        SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.2f }, Projectile.Center);
                        Vector2 ver = Projectile.Center.To(target.Center).UnitVector() * 13;
                        int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, ver
                            , ModContent.ProjectileType<GhostFireProj>(), Projectile.damage, 2);
                        Main.projectile[proj].friendly = true;
                    }
                }
            }

            //帧动画更新
            VaultUtils.ClockFrame(ref frame, 5, 3);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = VaultUtils.GetRectangle(tex, Projectile.frame, 4);
            Vector2 origin = rect.Size() / 2f;

            Color baseColor = Color.Lerp(Color.LimeGreen, Color.Cyan, 0.5f);
            float scale = Projectile.scale;

            //绘制残影（幽光拖尾）
            float alpha = 0.4f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float fade = alpha * (1f - i / (float)Projectile.oldPos.Length);
                Main.spriteBatch.Draw(tex, pos, rect, baseColor * fade, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            //主体 + 发光外层
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

            //外层发光（更大的，半透明）
            Main.spriteBatch.Draw(tex, drawPos, rect, baseColor * 0.3f, Projectile.rotation, origin, scale * 1.4f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
