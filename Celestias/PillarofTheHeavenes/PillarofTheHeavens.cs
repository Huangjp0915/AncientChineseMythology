using InnoVault.Actors;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天柱系统入口 - 四方神圣天柱
    /// 
    /// 系统组成：
    /// - HeavenPillarActor: 天柱实体，继承自InnoVault的Actor基类
    /// - HeavenPillarSystem: 天柱管理系统，负责生成、持久化和状态管理
    /// - HeavenlyEffect: 天庭视觉效果，当玩家靠近天柱时触发
    /// 
    /// 触发条件：
    /// - 击败月球领主后，四根天柱将降临在地图的一块区域
    /// - 天柱在地图上常态存在，可保存和加载
    /// 
    /// 纹理说明：
    /// - 纹理文件为 PillarofTheHeavens.png
    /// - 横向四帧排列，每帧代表一根不同样式的天柱（东南西北四方）
    /// </summary>
    internal class PillarofTheHeavens
    {
        // 此类作为文档入口，实际功能由以下类实现：
        // - HeavenPillarActor.cs: 天柱实体
        // - HeavenPillarSystem.cs: 系统管理
        // - HeavenlyEffect.cs: 视觉效果
    }
}
