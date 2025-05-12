using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    public abstract class CloneBoss : BasicWorm
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/Archosaur/" + Name;
        public override bool IsUseSpriteDirection => true;
        public Player Target
        {
            get
            {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }
        public override void SetDefaults()
        {
            //base.SetDefaults();
            NPC.height = 10;
            NPC.lifeMax = 100000;
            NPC.damage = 150;
            NPC.defense = 30;
            NPC.boss = true;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.knockBackResist = 0;
            SummonMax = 25;
        }
        
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            _ = TextureAssets.Npc[Type].Value;
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = new(NPC.spriteDirection == -1 ? 0 : tex.Width, 20);
            if (NPCWormType == WormType.Head) // 头部执行AI
            {
                origin.Y += 34;
                origin.X = NPC.spriteDirection == -1 ? (tex.Width / 4) : (tex.Width / 4 * 3);
            }
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, origin, NPC.scale, NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
    public class CloneBossHead : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Head;
        public override void ChangeSummonType()
        {
            SummonNPCType = ModContent.NPCType<CloneBossBody2>();
        }
        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 50;
        }

        public override void AI()
        {
            base.AI();
            NPC.dontTakeDamage = false; 

            if (NPCWormType == WormType.Head) // 头部执行AI
            {
                Vector2 vel = Target.Center - NPC.Center; // 目标方向
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X <= 0 ? -1 : 1;
                if (NPC.spriteDirection == -1)
                    NPC.rotation += MathHelper.Pi;
                if (vel.Length() > 300)
                {
                    Vector2 changeVel = vel.SafeNormalize(Vector2.UnitX); // 改变的速度
                    NPC.velocity = (NPC.velocity * 50 + changeVel * 32) / 51f; // 修改速度
                }
                else
                    NPC.velocity *= 1.01f;
            }
        }

        public override void OnKill()
        {
            // ai[3] 存的是宿主索引（SpawnClone 时已经赋值）
            int hostIdx = (int)NPC.ai[1];
            if (hostIdx >= 0 && Main.npc[hostIdx].active)
            {
                NPC host = Main.npc[hostIdx];
                int dmg = (int)(host.lifeMax * 0.15f);
                host.life -= dmg;
                if (host.life < 0) host.life = 0;

                CombatText.NewText(host.Hitbox, Microsoft.Xna.Framework.Color.OrangeRed, dmg);
            }
        }

    }
    public class CloneBossBody1 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType()
        {
            SummonNPCType = ModContent.NPCType<CloneBossBody2>();
        }
        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 15;
            NPC.height = 50;
        }
    }
    public class CloneBossBody2 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType()
        {
            SummonNPCType = ModContent.NPCType<CloneBossBody2>();
            if (SummonCount == SummonMax / 3 * 2 || SummonCount == 3)
                SummonNPCType = ModContent.NPCType<CloneBossBody1>();
            if(SummonCount > SummonMax - 3)
                SummonNPCType = ModContent.NPCType<CloneBossBody3>();
        }
        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 15;
        }
    }
    public class CloneBossBody3 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossBody4>();
        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
    public class CloneBossBody4 : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Body;
        public override void ChangeSummonType() => SummonNPCType = ModContent.NPCType<CloneBossTail>();
        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 20;
            SummonMax = 25;
            NPC.dontTakeDamage = false; 
        }
    }
    public class CloneBossTail : ArchosaurBoss
    {
        public override WormType NPCWormType => WormType.Tail;
        public override void SetDefaults()
        {
            base.SetDefaults();
            NPC.width = 20;
        }
    }
}