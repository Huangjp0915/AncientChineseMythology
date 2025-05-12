using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs
{
    /// <summary>
    /// 摘要：NPC的width是虫子距离上一节的距离
    /// </summary>
    public abstract class BasicWorm : ModNPC
    {
        public enum WormType : byte
        {
            Head,
            Body,
            Tail
        }
        public override void SetDefaults() // 子类保留这个的Base以方便调用父类的修改AIStyle与AIType
        {
            NPC.aiStyle = -1; // 这是为了避免NPC的AI类型与原版有所冲突
        }
        /// <summary>
        /// 启用SPDir翻转
        /// </summary>
        public virtual bool IsUseSpriteDirection => false;
        public abstract WormType NPCWormType { get; }
        /// <summary>
        /// 上一个Worm
        /// </summary>
        public int FatherWorm = -1;
        public NPC FatherNPC
        {
            get
            {
                if(FatherWorm != -1)
                    return Main.npc[FatherWorm];
                return null;
            }
        }

        /// <summary>
        /// 时候生成了下一节NPC
        /// </summary>
        public bool IsSummonNPC;
        /// <summary>
        /// 召唤NPC的Type,请在SetDefaults中设置,也可以根据SummonCount在ChangeSummonType修改
        /// </summary>
        public int SummonNPCType;
        /// <summary>
        /// 召唤NPC的时间
        /// </summary>
        public int SummonTime;
        /// <summary>
        /// 召唤上限,请在SetDefaults中设置
        /// </summary>
        public int SummonMax;
        /// <summary>
        /// 召唤次数
        /// </summary>
        public int SummonCount;
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(FatherWorm);
            #region 通过这个传输Bool
            BitsByte bitsByte = new BitsByte();
            bitsByte[0] = IsSummonNPC;
            writer.Write(bitsByte);
            #endregion
        }
        public override void ReceiveExtraAI(BinaryReader reader)
        {
            FatherWorm = reader.ReadInt32();
            #region 通过这个传输Bool
            BitsByte bitsByte = reader.ReadByte();
            IsSummonNPC = bitsByte[0];
            #endregion
        }
        public override void OnSpawn(IEntitySource source)
        {
            if(source is EntitySource_Parent parent && parent.Entity is NPC npc)
            {
                FatherWorm = npc.whoAmI;
                SummonTime = 5;
                if(npc.ModNPC is BasicWorm basicWorm)
                {
                    SummonMax = basicWorm.SummonMax;
                    SummonCount = basicWorm.SummonCount + 1;
                }

                while (npc.ModNPC is BasicWorm basicWorm1 && basicWorm1.FatherWorm != -1) // 找到头部
                {
                    npc = Main.npc[basicWorm1.FatherWorm]; // 迭代NPC
                }
                if(npc.active && npc.ModNPC is BasicWorm)
                    NPC.realLife = npc.whoAmI;
            }
        }
        /// <summary>
        /// 有事没事不要重写这个，这个是根据NPCWormType来确定NPC的AI
        /// </summary>
        public override void PostAI()
        {
            switch (NPCWormType)
            {
                case WormType.Head:
                    if(!IsSummonNPC)
                    {
                        ChangeSummonType();
                        IsSummonNPC = true;
                        NPC.netUpdate = true;
                        NPC.NewNPCDirect(NPC.GetSource_FromThis(), NPC.position, SummonNPCType, NPC.whoAmI);
                    }
                    break;
                case WormType.Body:
                case WormType.Tail:
                    if(SummonTime > 0)
                        SummonTime--;
                    if (!IsSummonNPC && SummonTime <= 0 && SummonMax >= SummonCount)
                    {
                        ChangeSummonType();
                        IsSummonNPC = true;
                        NPC.netUpdate = true;
                        NPC.NewNPCDirect(NPC.GetSource_FromThis(), NPC.position, SummonNPCType, NPC.whoAmI);
                    }
                    if (FatherNPC != null)
                        ChangePos();
                    if (FatherNPC?.active == false)
                        NPC.active = false;
                    NPC.rotation = NPC.velocity.ToRotation();
                    if (IsUseSpriteDirection)
                    {
                        if (NPCWormType == WormType.Tail && FatherNPC != null)
                            NPC.spriteDirection = FatherNPC.spriteDirection;
                        else
                            NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1; // 自动修改朝向
                    }
                    if (NPC.spriteDirection == -1)
                        NPC.rotation += MathHelper.Pi;
                    break;
            }
        }
        /// <summary>
        /// 可以重写这个方法来改变位置算法
        /// </summary>
        public virtual void ChangePos()
        {
            NPC.velocity = Vector2.Lerp(NPC.rotation.ToRotationVector2() * NPC.spriteDirection, FatherNPC.rotation.ToRotationVector2() * FatherNPC.spriteDirection, 0.5f);
            NPC.Center = FatherNPC.Center - NPC.velocity * (NPC.width + FatherNPC.width) / 2;
        }
        /// <summary>
        /// 修改生成的NPC类型
        /// </summary>
        public virtual void ChangeSummonType()
        {

        }
    }
}
