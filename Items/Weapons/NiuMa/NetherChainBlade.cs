using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.NiuMa
{
    /// <summary>
    /// 牛马双件 (冥链刃/勾魂索) 共用魂链 VFX 层: 配色唯一事实来源 + 悬垂链曲线 +
    /// 锁链分节贴图铺设 + NetherChainSoulLink.fx 条带绘制 (丢着色器自动退化共享 ribbon)。
    /// 牛头执锁 → 青蓝冥焰 (NetherGrudge 主题); 马面执勾 → 幽紫魂色 (AbyssPurple 主题)。
    /// </summary>
    internal static class NiuMaSoulChainVFX
    {
        // ===== 牛头·锁 (冥链刃) =====
        public static readonly Color OxCore = new(150, 230, 255);
        public static readonly Color OxBloom = new(90, 200, 240);
        public static readonly Color OxDeep = new(20, 70, 130);
        // ===== 马面·勾 (勾魂索) =====
        public static readonly Color HorseCore = new(210, 165, 255);
        public static readonly Color HorseBloom = new(150, 110, 240);
        public static readonly Color HorseDeep = new(60, 30, 110);

        /// <summary>
        /// 悬垂链曲线 (二次贝塞尔, 垂度 slack 像素) 采样进复用数组 — 锁链"松则下坠、紧则绷直"的物理感。
        /// </summary>
        public static void BuildSagCurve(Vector2 a, Vector2 b, float slack, Vector2[] output) {
            Vector2 mid = (a + b) * 0.5f + new Vector2(0f, slack);
            int n = output.Length;
            for (int i = 0; i < n; i++) {
                float t = i / (float)(n - 1);
                float u = 1f - t;
                output[i] = u * u * a + 2f * u * t * mid + t * t * b;
            }
        }

        /// <summary>沿点列分节铺原版锁链贴图 (每节独立旋转, 链节实体感)。须在默认批阶段调用。</summary>
        public static void DrawChainLinks(Vector2[] pts, Color color, float scale = 0.85f) {
            if (Main.dedServ || pts == null || pts.Length < 2)
                return;
            Texture2D tex = TextureAssets.Chains[0].Value;
            const int linkH = 14;
            Rectangle frame = new(0, 0, tex.Width, linkH);
            Vector2 origin = new(tex.Width / 2f, linkH / 2f);
            float step = linkH * scale;
            float carry = 0f;

            for (int i = 0; i < pts.Length - 1; i++) {
                Vector2 seg = pts[i + 1] - pts[i];
                float len = seg.Length();
                if (len < 0.1f)
                    continue;
                Vector2 dir = seg / len;
                float rot = seg.ToRotation() + MathHelper.PiOver2;
                float d = carry;
                while (d < len) {
                    Vector2 pos = pts[i] + dir * d;
                    Color lit = Color.Lerp(Lighting.GetColor(pos.ToTileCoordinates()), color, 0.55f);
                    Main.spriteBatch.Draw(tex, pos - Main.screenPosition, frame, lit, rot, origin, scale, SpriteEffects.None, 0f);
                    d += step;
                }
                carry = d - len;
            }
        }

        /// <summary>
        /// 魂链条带 (NetherChainSoulLink.fx): 链节明暗 + 魂火流动 + 勾魂行波。
        /// 须在有活动批的阶段调用 (PreDraw 等); 服务端/丢着色器退化为共享双层 ribbon。
        /// </summary>
        /// <param name="pulsePos">行波沿链位置 0~1 (负值关闭)。</param>
        public static void DrawSoulChainStrip(Vector2[] worldPts, float halfWidth, Color core, Color edge,
            float intensity, float pulsePos = -1f, float pulseGlow = 1.1f, float flowSpeed = 1.1f) {
            if (Main.dedServ || worldPts == null || worldPts.Length < 2 || intensity <= 0.01f)
                return;

            Effect fx = WeaponVFX.GetEffect("NetherChainSoulLink");
            if (fx == null) {
                WeaponVFX.DrawRibbonTrail(worldPts, halfWidth * 2f, edge, core);
                return;
            }

            // 链总长 → 链节周期数
            float totalLen = 0f;
            Vector2[] pts = new Vector2[worldPts.Length];
            for (int i = 0; i < worldPts.Length; i++) {
                pts[i] = worldPts[i] - Main.screenPosition;
                if (i > 0)
                    totalLen += Vector2.Distance(worldPts[i], worldPts[i - 1]);
            }

            var verts = ACMUtils.BuildRibbonStrip(pts, _ => halfWidth, _ => Color.White, 0f, 2);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uLinkCount"]?.SetValue(MathHelper.Clamp(totalLen / 22f, 3f, 40f));
            fx.Parameters["uPulsePos"]?.SetValue(pulsePos);
            fx.Parameters["uPulseGlow"]?.SetValue(pulseGlow);
            fx.Parameters["uFlowSpeed"]?.SetValue(flowSpeed);
            fx.Parameters["uEndFade"]?.SetValue(0.07f);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Texture2D noise = ACMShaders.NoiseTexture;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }

    /// <summary>
    /// 冥链刃 — 牛头马面掉落链刃 (牛头执锁的差役幻想)。
    /// 锁一个 (命中 A 上魂锁标记) → 链两个 (命中 B 生成 A↔B 魂链, 周期链脉冲) →
    /// 第三击回打链端点触发【锁魂对撞】(×2.2 伤害 + 两端互拽 + 链爆)。
    /// </summary>
    public class NetherChainBlade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 58;
            Item.DamageType = DamageClass.Melee;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item1; // 基础掷声走广播; 链响层在 Shoot 叠加
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<NetherChainBladeProjectile>();
            Item.shootSpeed = 28f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<NetherChainBladeProjectile>()] < 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // 出手叠加高频链响层 (低频掷击由 UseSound 广播)
            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.35f, Pitch = 0.25f }, player.Center);
            return true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.ChainKnife;
    }

    /// <summary>
    /// 魂锁标记 (每 NPC 实例): 冥链刃命中后留下的锁定印。owner 客户端记录并消费
    /// (命中判定本就发生在 owner 端, 决策一致); 视觉为环绕的青蓝链光 (各端按本地数据绘制)。
    /// </summary>
    public class NetherSoulMarkNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>标记持续帧 (10s 无后续锁链则消散)。</summary>
        public const int Duration = 600;

        public int MarkTimer;
        public int MarkOwner = -1;

        public override void PostAI(NPC npc) {
            if (MarkTimer > 0 && --MarkTimer <= 0)
                MarkOwner = -1;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (MarkTimer <= 0 || Main.dedServ)
                return;
            Texture2D glow = ACMAsset.LightShot;
            if (glow == null)
                return;

            // 环绕的三点链光 (即将消散时闪烁)
            float t = (float)Main.GlobalTimeWrappedHourly * 3.4f;
            float fade = MarkTimer < 60 ? 0.4f + 0.6f * MathF.Abs(MathF.Sin(MarkTimer * 0.3f)) : 1f;
            float radius = MathF.Max(npc.width, npc.height) * 0.62f + 6f;
            for (int i = 0; i < 3; i++) {
                float ang = t + MathHelper.TwoPi * i / 3f;
                Vector2 pos = npc.Center + ang.ToRotationVector2() * radius - screenPos;
                Color c = NiuMaSoulChainVFX.OxBloom * (0.7f * fade);
                c.A = 0;
                spriteBatch.Draw(glow, pos, null, c, ang + MathHelper.PiOver2, glow.Size() * 0.5f, new Vector2(0.42f, 0.2f), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 冥链刃弹幕: 掷出 (1 帧 set 28px/f) → 7 帧全速 → hard-brake 顿挫 → 6 帧悬停勾魂窗口 →
    /// 二次方加速回收。链条悬垂物理感 + 魂链条带着色器。
    /// 命中逻辑: 无链 → 标记/成链; 命中链端点 → 锁魂对撞 (×2.2 + 引爆)。
    /// </summary>
    public class NetherChainBladeProjectile : ModProjectile
    {
        private const float MaxRange = 430f;
        private const float LinkRange = 620f;  // 成链最大间距 (超距标记改为转移)
        private const int FullSpeedTime = 7;   // 全速帧
        private const int BrakeTime = 8;       // 刹链帧
        private const int HoverTime = 6;       // 悬停勾魂窗口

        private Player Owner => Main.player[Projectile.owner];

        private ref float FlightTimer => ref Projectile.ai[0];
        private ref float State => ref Projectile.ai[1]; // 0=掷出 1=悬停 2=回收
        private ref float SpinPhase => ref Projectile.localAI[0];

        // 对撞标记: ModifyHitNPC 判定后交给 OnHitNPC 消费 (同帧, 仅 owner 端)
        private bool _detonateHit;

        // 复用链曲线缓冲 (仅绘制路径, 客户端单线程)
        private static readonly Vector2[] _chainCurve = new Vector2[14];

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;
            Owner.ChangeDir(Projectile.Center.X > Owner.Center.X ? 1 : -1);

            // 刃旋转: 悬停期加速旋转 (勾魂窗口的"锁定"读法)
            float spinRate = State == 1f ? 0.75f : 0.4f;
            SpinPhase += 0.1f;
            Projectile.rotation += spinRate * (Projectile.velocity.X == 0f ? Owner.direction : Math.Sign(Projectile.velocity.X));

            FlightTimer++;
            float distToOwner = Vector2.Distance(Projectile.Center, Owner.Center);

            switch (State) {
                case 0f: // 掷出: 全速 → hard-brake 顿挫 ("链到尽头")
                    if (FlightTimer > FullSpeedTime) {
                        Projectile.velocity *= 0.86f;
                        if (FlightTimer == FullSpeedTime + BrakeTime / 2)
                            SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
                    }
                    if (FlightTimer >= FullSpeedTime + BrakeTime || distToOwner > MaxRange) {
                        State = 1f;
                        FlightTimer = 0f;
                        Projectile.netUpdate = true;
                    }
                    break;

                case 1f: // 悬停勾魂窗口
                    Projectile.velocity *= 0.82f;
                    if (FlightTimer >= HoverTime) {
                        State = 2f;
                        FlightTimer = 0f;
                        Projectile.tileCollide = false;
                        Projectile.netUpdate = true;
                    }
                    break;

                default: // 回收: 越拉越快 (二次方渐增)
                    Projectile.tileCollide = false;
                    Vector2 toOwner = Owner.Center - Projectile.Center;
                    if (distToOwner < 26f) {
                        // 到手: 收链反馈
                        SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.65f, Pitch = -0.1f }, Owner.Center);
                        WeaponVFX.AddScreenShake(Owner.Center, 1f);
                        Projectile.Kill();
                        return;
                    }
                    float pull = MathHelper.Clamp(FlightTimer / 22f, 0f, 1f);
                    float speed = MathHelper.Lerp(12f, 30f, pull * pull);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.Zero) * speed, 0.3f);
                    break;
            }

            // 冥焰拖尾尘 (节流)
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1f, 1f), 70, default, 1.05f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);
        }

        // ===== 命中: 锁魂三段循环 =====

        private NetherSoulLink FindOwnLink() {
            int type = ModContent.ProjectileType<NetherSoulLink>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && p.owner == Projectile.owner)
                    return p.ModProjectile as NetherSoulLink;
            }
            return null;
        }

        private NPC FindMarkedNpc(int exclude, Vector2 nearPos) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.whoAmI == exclude || !npc.CanBeChasedBy(Projectile))
                    continue;
                var mark = npc.GetGlobalNPC<NetherSoulMarkNPC>();
                if (mark.MarkTimer > 0 && mark.MarkOwner == Projectile.owner
                    && Vector2.Distance(npc.Center, nearPos) <= LinkRange)
                    return npc;
            }
            return null;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            _detonateHit = false;
            NetherSoulLink link = FindOwnLink();
            if (link != null && link.IsEndpoint(target.whoAmI)) {
                // 锁魂对撞: 回打链端点 ×2.2
                modifiers.FinalDamage *= 2.2f;
                _detonateHit = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中反馈栈基础层
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.2f);
                d.noGravity = true;
            }

            if (Projectile.owner != Main.myPlayer)
                return;

            // 命中即开始回收 (勾到东西就收链)
            if (State < 2f) {
                State = 2f;
                FlightTimer = 6f;
                Projectile.netUpdate = true;
            }

            NetherSoulLink link = FindOwnLink();

            // —— 第三击: 锁魂对撞 ——
            if (_detonateHit && link != null) {
                _detonateHit = false;
                link.TriggerDetonate();
                return;
            }

            if (link != null) {
                // 已有魂链时的普通命中: 小规模反馈
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.NetherGrudge, 0.8f, Projectile.owner);
                return;
            }

            var mark = target.GetGlobalNPC<NetherSoulMarkNPC>();
            NPC partner = FindMarkedNpc(target.whoAmI, target.Center);

            if (partner != null) {
                // —— 第二击: 成链 A↔B ——
                var partnerMark = partner.GetGlobalNPC<NetherSoulMarkNPC>();
                partnerMark.MarkTimer = 0;
                partnerMark.MarkOwner = -1;
                mark.MarkTimer = 0;
                mark.MarkOwner = -1;

                int linkDamage = Math.Max(1, (int)(Projectile.damage * 0.35f));
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target), Vector2.Lerp(partner.Center, target.Center, 0.5f),
                    Vector2.Zero, ModContent.ProjectileType<NetherSoulLink>(), linkDamage, 2f,
                    Projectile.owner, partner.whoAmI, target.whoAmI);

                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.6f, Pitch = -0.15f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = 0.3f }, target.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.NetherGrudge, 1.1f, Projectile.owner);
            }
            else if (mark.MarkTimer <= 0 || mark.MarkOwner != Projectile.owner) {
                // —— 第一击: 上魂锁标记 ——
                mark.MarkTimer = NetherSoulMarkNPC.Duration;
                mark.MarkOwner = Projectile.owner;
                SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.NetherGrudge, 0.9f, Projectile.owner);
            }
            else {
                // 重复锁同一目标: 刷新标记
                mark.MarkTimer = NetherSoulMarkNPC.Duration;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            // 撞墙: 火星 + 反弹一点然后收链
            Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            Projectile.velocity = oldVelocity * -0.2f;
            State = 2f;
            FlightTimer = 0f;
            Projectile.netUpdate = true;
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                    (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero).RotatedByRandom(0.6) * Main.rand.NextFloat(2f, 5f),
                    50, default, 1.05f);
                d.noGravity = true;
            }
        }

        // ===== 绘制: 悬垂锁链 + 魂链条带 + 刃体 =====

        public override bool PreDraw(ref Color lightColor) {
            DrawPlayerChain();
            DrawBlade(lightColor);
            return false;
        }

        private void DrawPlayerChain() {
            Vector2 hand = Owner.MountedCenter + new Vector2(Owner.direction * 6f, -2f);
            // 垂度: 速度低/悬停时链下坠, 绷紧时拉直
            float speed = Projectile.velocity.Length();
            float slackT = MathHelper.Clamp(1f - speed / 22f, 0f, 1f);
            float dist = Vector2.Distance(hand, Projectile.Center);
            float slack = MathHelper.Lerp(2f, 40f, slackT) * MathHelper.Clamp(dist / MaxRange, 0.25f, 1f);

            NiuMaSoulChainVFX.BuildSagCurve(hand, Projectile.Center, slack, _chainCurve);

            // 魂链条带 (着色器) 垫底, 锁链贴图分节铺在上面
            float intensity = State == 1f ? 0.85f : 0.6f;
            NiuMaSoulChainVFX.DrawSoulChainStrip(_chainCurve, 7f,
                NiuMaSoulChainVFX.OxCore, NiuMaSoulChainVFX.OxDeep, intensity,
                pulsePos: State == 2f ? 1f - MathHelper.Clamp(FlightTimer / 22f, 0f, 1f) : -1f, pulseGlow: 0.9f);
            NiuMaSoulChainVFX.DrawChainLinks(_chainCurve, new Color(120, 150, 200, 190), 0.85f);
        }

        private void DrawBlade(Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.ChainKnife].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // 悬停勾魂窗口: 刃体柔光渐亮 (锁定读法)
            if (State == 1f)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.6f + FlightTimer / HoverTime * 0.4f, NiuMaSoulChainVFX.OxBloom * 0.7f);

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            Color glow = NiuMaSoulChainVFX.OxBloom * (0.4f + MathF.Sin(SpinPhase * 2f) * 0.12f);
            glow.A = 0;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glow,
                Projectile.rotation, origin, Projectile.scale * 1.14f, SpriteEffects.None, 0);
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ChainKnife;
    }

    /// <summary>
    /// 魂链 (A↔B 从属弹幕): 两敌之间的持续锁链, 沿链线判定伤害 (走正常命中管线, 吃玩家加成),
    /// 每 40 帧一次链脉冲行波 + 把命中者向链心拽。冥链刃回打端点时触发锁魂对撞。
    /// ai[0]=端点A ai[1]=端点B。
    /// </summary>
    public class NetherSoulLink : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int LifeTime = 480;
        private const int PulseInterval = 40;

        private int EndA => (int)Projectile.ai[0];
        private int EndB => (int)Projectile.ai[1];
        private ref float PulseTimer => ref Projectile.localAI[0];

        private static readonly Vector2[] _linkCurve = new Vector2[16];

        public bool IsEndpoint(int npcWhoAmI) => npcWhoAmI == EndA || npcWhoAmI == EndB;

        private NPC GetEnd(int who) {
            if (who < 0 || who >= Main.maxNPCs)
                return null;
            NPC npc = Main.npc[who];
            return npc.active && npc.life > 0 ? npc : null;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
            Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.NetherSoulLink.DisplayName",
                () => "Nether Soul Link");
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = PulseInterval;
        }

        public override void AI() {
            NPC a = GetEnd(EndA);
            NPC b = GetEnd(EndB);
            if (a == null || b == null) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Vector2.Lerp(a.Center, b.Center, 0.5f);

            PulseTimer += 1f / PulseInterval;
            if (PulseTimer >= 1f) {
                PulseTimer = 0f;
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.3f, Pitch = 0.1f + Main.rand.NextFloat(0.1f) }, Projectile.Center);
            }

            // 链上冥焰尘 (节流)
            if (Main.rand.NextBool(2)) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(a.Center, b.Center, t) + Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, Main.rand.NextVector2Circular(1.2f, 1.2f), 80, default, 1.0f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.25f, 0.5f, 0.7f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC a = GetEnd(EndA);
            NPC b = GetEnd(EndB);
            if (a == null || b == null)
                return false;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                a.Center, b.Center, 22f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            // 链脉冲把命中者向链心拽 (击退方向反转为朝向链中点)
            modifiers.HitDirectionOverride = Math.Sign(Projectile.Center.X - target.Center.X) >= 0 ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnPulseDust(target.Center);
        }

        /// <summary>锁魂对撞 (冥链刃第三击触发, 仅 owner 端调用): 两端互拽 + 链心爆 + 链销毁。</summary>
        public void TriggerDetonate() {
            if (Projectile.owner != Main.myPlayer)
                return;
            NPC a = GetEnd(EndA);
            NPC b = GetEnd(EndB);
            Vector2 mid = Projectile.Center;

            // 两端向链心猛拽 (kbResist 缩放; 与旧实现同风险面的 owner 端冲量)
            if (a != null && b != null) {
                Vector2 dir = (b.Center - a.Center).SafeNormalize(Vector2.Zero);
                a.velocity += dir * 9f * (1f - a.knockBackResist * 0.85f);
                b.velocity -= dir * 9f * (1f - b.knockBackResist * 0.85f);
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.2f }, mid);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.35f }, mid);
            WeaponVFX.AddScreenShake(mid, 3f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), mid,
                ACMWeaponBurst.NetherGrudge, 1.9f, Projectile.owner);

            for (int i = 0; i < 18; i++) {
                Dust d = Dust.NewDustPerfect(mid, DustID.Shadowflame,
                    Main.rand.NextVector2CircularEdge(7f, 7f) * Main.rand.NextFloat(0.4f, 1f), 40, default, 1.6f);
                d.noGravity = true;
            }

            Projectile.Kill();
        }

        private static void SpawnPulseDust(Vector2 pos) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, Main.rand.NextVector2Circular(3f, 3f), 50, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC a = GetEnd(EndA);
            NPC b = GetEnd(EndB);
            if (a == null || b == null)
                return false;

            // 微垂链 (两怪之间绷得比较紧)
            NiuMaSoulChainVFX.BuildSagCurve(a.Center, b.Center, 14f, _linkCurve);

            float fadeIn = MathHelper.Clamp((LifeTime - Projectile.timeLeft) / 12f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float intensity = 0.9f * fadeIn * fadeOut;

            NiuMaSoulChainVFX.DrawSoulChainStrip(_linkCurve, 9f,
                NiuMaSoulChainVFX.OxCore, NiuMaSoulChainVFX.OxDeep, intensity,
                pulsePos: PulseTimer, pulseGlow: 1.5f, flowSpeed: 1.4f);
            NiuMaSoulChainVFX.DrawChainLinks(_linkCurve, new Color(130, 165, 215, 200) * fadeOut, 0.9f);

            // 两端锁点柔光
            WeaponVFX.DrawGlowBurst(a.Center, 0.5f, NiuMaSoulChainVFX.OxBloom * (0.45f * intensity));
            WeaponVFX.DrawGlowBurst(b.Center, 0.5f, NiuMaSoulChainVFX.OxBloom * (0.45f * intensity));
            return false;
        }
    }
}
