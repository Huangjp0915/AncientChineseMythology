using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs
{
    ///<summary>
    ///摘要：NPC的width是虫子距离上一节的距离
    ///</summary>
    public abstract class BasicWorm : ModNPC
    {
        public enum WormType : byte
        {
            Head,
            Body,
            Tail
        }
        public override void SetDefaults() //子类保留这个的Base以方便调用父类的修改AIStyle与AIType
        {
            NPC.aiStyle = -1; //这是为了避免NPC的AI类型与原版有所冲突
        }
        ///<summary>
        ///启用SPDir翻转
        ///</summary>
        public virtual bool IsUseSpriteDirection => false;
        public abstract WormType NPCWormType { get; }
        ///<summary>
        ///上一个Worm
        ///</summary>
        public int FatherWorm = -1;
        public NPC FatherNPC {
            get {
                if (FatherWorm != -1)
                    return Main.npc[FatherWorm];
                return null;
            }
        }

        ///<summary>
        ///时候生成了下一节NPC
        ///</summary>
        public bool IsSummonNPC;
        ///<summary>
        ///召唤NPC的Type,请在SetDefaults中设置,也可以根据SummonCount在ChangeSummonType修改
        ///</summary>
        public int SummonNPCType;
        ///<summary>
        ///召唤NPC的时间
        ///</summary>
        public int SummonTime;
        ///<summary>
        ///召唤上限,请在SetDefaults中设置
        ///</summary>
        public int SummonMax;
        ///<summary>
        ///召唤次数
        ///</summary>
        public int SummonCount;
        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(FatherWorm);
            #region 通过这个传输Bool
            BitsByte bitsByte = new BitsByte();
            bitsByte[0] = IsSummonNPC;
            writer.Write(bitsByte);
            #endregion
        }
        public override void ReceiveExtraAI(BinaryReader reader) {
            FatherWorm = reader.ReadInt32();
            #region 通过这个传输Bool
            BitsByte bitsByte = reader.ReadByte();
            IsSummonNPC = bitsByte[0];
            #endregion
        }
        public override void OnSpawn(IEntitySource source) {
            if (source is EntitySource_Parent parent && parent.Entity is NPC npc) {
                FatherWorm = npc.whoAmI;
                SummonTime = 5;
                if (npc.ModNPC is BasicWorm basicWorm) {
                    SummonMax = basicWorm.SummonMax;
                    SummonCount = basicWorm.SummonCount + 1;
                }

                while (npc.ModNPC is BasicWorm basicWorm1 && basicWorm1.FatherWorm != -1) //找到头部
                {
                    npc = Main.npc[basicWorm1.FatherWorm]; //迭代NPC
                }
                if (npc.active && npc.ModNPC is BasicWorm)
                    NPC.realLife = npc.whoAmI;
            }
        }
        ///<summary>
        ///有事没事不要重写这个，这个是根据NPCWormType来确定NPC的AI
        ///</summary>
        public override void PostAI() {
            switch (NPCWormType) {
                case WormType.Head:
                    if (!IsSummonNPC) {
                        ChangeSummonType();
                        IsSummonNPC = true;
                        NPC.netUpdate = true;
                        NPC.NewNPCDirect(NPC.GetSource_FromThis(), NPC.position, SummonNPCType, NPC.whoAmI);
                    }
                    break;
                case WormType.Body:
                case WormType.Tail:
                    if (SummonTime > 0)
                        SummonTime--;
                    if (!IsSummonNPC && SummonTime <= 0 && SummonMax >= SummonCount) {
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
                    if (IsUseSpriteDirection) {
                        if (NPCWormType == WormType.Tail && FatherNPC != null)
                            NPC.spriteDirection = FatherNPC.spriteDirection;
                        else
                            NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1; //自动修改朝向
                    }
                    if (NPC.spriteDirection == -1)
                        NPC.rotation += MathHelper.Pi;
                    break;
            }
        }
        ///<summary>
        ///可以重写这个方法来改变位置算法
        ///</summary>
        public virtual void ChangePos() {
            // 计算到父节点的向量
            Vector2 directionToParent = FatherNPC.Center - NPC.Center;
            float distanceToParent = directionToParent.Length();
            
            // 目标距离（父节点宽度 + 自身宽度）/ 2
            float targetDistance = (FatherNPC.width + NPC.width) / 2f;
            
            // 如果距离不为0，归一化方向向量
            if (distanceToParent > 0.1f)
            {
                directionToParent.Normalize();
                
                // 计算目标位置
                Vector2 targetPosition = FatherNPC.Center - directionToParent * targetDistance;
                
                // 平滑移动到目标位置（使用更强的插值来减少延迟）
                float smoothFactor = 0.5f; // 可调整，越大越紧密跟随
                NPC.Center = Vector2.Lerp(NPC.Center, targetPosition, smoothFactor);
                
                // 更新速度向量（指向移动方向）
                NPC.velocity = targetPosition - NPC.Center;
                
                // 如果速度太小，使用父节点的方向
                if (NPC.velocity.LengthSquared() < 0.01f)
                {
                    NPC.velocity = -directionToParent * 0.1f;
                }
            }
            else
            {
                // 距离太近，直接使用父节点的速度方向
                NPC.velocity = FatherNPC.velocity;
            }
            
            // 限制速度，避免过快移动
            float maxSpeed = 30f;
            if (NPC.velocity.LengthSquared() > maxSpeed * maxSpeed)
            {
                NPC.velocity.Normalize();
                NPC.velocity *= maxSpeed;
            }
            
            // 取整坐标以减少抖动
            NPC.Center = new Vector2((int)NPC.Center.X, (int)NPC.Center.Y);
        }
        ///<summary>
        ///修改生成的NPC类型
        ///</summary>
        public virtual void ChangeSummonType() {

        }
    }
}
