// ============================================================
// 大椿「生命冲击环」— 噪声扰动边缘的金绿生命波 + 叶脉放射拖尾
// 用途: 季语宣告光环 / 换阶段爆发 / 死亡终爆 (加性绘制, s0=共享三通道FBM噪声)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (LinearWrap)

float uTime;         // 动画时间 (秒)
float2 uCenter;      // 环心归一化屏幕坐标 (0~1)
float uRadius;       // 当前环半径 (屏幕高度比例)
float uIntensity;    // 整体强度 (0~1)
float uAspect;       // 屏幕宽高比
float4 uColorCore;   // 前锋主色 (亮)
float4 uColorEdge;   // 拖尾辉色 (暗)
float uThickness;    // 环厚 (屏幕高度比例)
float uProgress;     // 0~1 生命进度 (驱动前锋锐度与整体衰减)

float4 LifeburstPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // 宽高比校正
    float2 pos = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff = pos - center;
    float dist = length(diff);

    // 快速剔除: 远离环带直接透明
    if (dist > uRadius + uThickness * 2.0 || dist < uRadius - uThickness * 4.0)
        return float4(0, 0, 0, 0);

    float angNorm = atan2(diff.y, diff.x) / 6.28318 + 0.5;

    // 边缘噪声扰动 — 生命波的不规则轮廓 (非机械正圆)
    float n = tex2D(uImage0, float2(angNorm * 3.0, uTime * 0.11)).r;
    float wobble = (n - 0.5) * uThickness * 1.7;
    float d = dist - (uRadius + wobble);

    // 前锋硬 (向外锐截), 尾部软 (向内拖出长尾)
    float front = 1.0 - smoothstep(0.0, uThickness * 0.35, d);
    float tail = smoothstep(-uThickness * 2.6, 0.0, d);
    float ring = front * tail;

    // 叶脉放射条纹 — 拖尾内的角向纹理, 强化"生命力向外奔涌"
    float veins = tex2D(uImage0, float2(angNorm * 14.0, dist * 2.2 - uTime * 0.32)).g;
    veins = smoothstep(0.42, 0.85, veins);
    float tailZone = tail * (1.0 - front);
    ring = saturate(ring + tailZone * veins * 0.55);

    // progress 衰减 (smoothstep 收尾) + 淡入前 12%
    float fadeOut = 1.0 - smoothstep(0.55, 1.0, uProgress);
    float fadeIn = smoothstep(0.0, 0.12, uProgress);
    float a = ring * uIntensity * fadeOut * fadeIn;

    // 着色: 前锋取主色, 尾部取辉色; 叶脉略提亮; 前锋早期白热
    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, saturate(front * 0.75 + veins * 0.3));
    col += front * (1.0 - uProgress) * 0.35;

    // 加性合成 (返回预乘色, alpha 置 0)
    return float4(col * a, 0);
}

technique Technique1
{
    pass LifeburstPass
    {
        PixelShader = compile ps_3_0 LifeburstPS();
    }
}
