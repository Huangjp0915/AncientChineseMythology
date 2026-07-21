// ============================================================
// 赢勾·黄泉裂隙着色器 — 屏幕空间旋涡贴花
// 内暗核(吞噬世界) + 鬼火旋臂 + 明沿呼吸 + 塌缩控制
// 载体: 满屏噪声贴图 (s0), 由 DrawScreenSpaceDecal(Standalone) 驱动
// 用途: 入场开隙 / 双刃与面具的传送闪点 / 死亡收束裂隙
// 建议混合: NonPremultiplied (暗核压暗 + 亮沿提亮同 pass 完成)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径 (屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float4 uColorPrimary;    // 鬼火主色 (青绿)
float4 uColorSecondary;  // 深渊辅色 (幽紫/暗赤)
float  uCollapse;        // 0=全开 → 1=咬合 (死亡收束末段)
float  uSwirl;           // 旋臂强度/转速倍率 (0.5~2)

float4 NetherRiftPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;

    // 塌缩收拢有效半径; 咬合时沿变亮
    float effRadius = max(uRadius * (1.0 - uCollapse * 0.85), 0.004);
    float dist = length(diff) / effRadius;

    // 远场早退
    if (dist > 2.2)
        return float4(0, 0, 0, 0);

    float angle = atan2(diff.y, diff.x);

    // 极坐标域扭曲: 旋臂随半径扭转, 内快外慢
    float twist = (1.6 - dist) * 2.6 * uSwirl;
    float2 polarUV = float2(angle / 6.28318 + 0.5, dist * 0.5);
    float2 nUV1 = polarUV * float2(2.0, 3.0) + float2(uTime * 0.22 + twist * 0.12, -uTime * 0.16);
    float2 nUV2 = polarUV * float2(4.0, 1.7) + float2(-uTime * 0.15, uTime * 0.1 + twist * 0.06);
    float n1 = tex2D(uImage0, nUV1).r;
    float n2 = tex2D(uImage0, nUV2).g;
    float fbm = n1 * 0.62 + n2 * 0.38;

    float dN = dist + (fbm - 0.5) * 0.32; // 噪声撕裂边界

    // 内暗核: 中心吞黑
    float core = smoothstep(0.95, 0.25, dN);

    // 明沿: 边界一圈鬼火, 呼吸 + 咬合增亮
    float rim = exp(-abs(dN - 0.95) * 9.0);
    rim *= 0.75 + 0.35 * sin(uTime * 3.2 + angle * 2.0);
    rim *= 1.0 + uCollapse * 1.6;

    // 旋臂: 三条鬼火螺旋自沿口甩出
    float armPhase = angle * 3.0 - dist * 5.5 + uTime * 2.4 * uSwirl + fbm * 2.2;
    float arms = pow(saturate(sin(armPhase) * 0.5 + 0.5), 4.0);
    arms *= smoothstep(1.9, 0.85, dist) * smoothstep(0.3, 0.75, dist);

    // 组色: 暗核压向深渊色, 沿口与旋臂用鬼火色
    float3 darkCol = uColorSecondary.rgb * 0.22;
    float glowMask = saturate(rim + arms * 0.8);
    float3 glowCol = lerp(uColorSecondary.rgb, uColorPrimary.rgb, saturate(rim * 1.2)) * (1.1 + fbm * 0.5);

    float3 col = lerp(darkCol, glowCol, glowMask);
    float alpha = saturate(core * 0.88 + glowMask * 0.9) * uIntensity;

    return float4(col * sampleColor.rgb, alpha * sampleColor.a);
}

technique Technique1
{
    pass NetherRiftPass
    {
        PixelShader = compile ps_3_0 NetherRiftPS();
    }
}
