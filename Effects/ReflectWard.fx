// ============================================================
// 折射护盾着色器 — 六边形/玉环折射护罩 + 面板亮起
// 罩内对场景做轻折射 + 边缘菲涅尔亮边 + 六边面板格亮起脉冲
// 玄武玉璧绝防(首发); 毗沙门金护罩换色复用
// 喂 Main.screenTarget(s0) 做折射 + 可平铺噪声(s1)
// ============================================================

sampler uImage0 : register(s0); // 场景渲染目标
sampler uNoise  : register(s1); // 可平铺噪声

float  uTime;        // 动画时间(秒)
float2 uCenter;      // 护罩中心归一化屏幕坐标 0~1
float  uRadius;      // 护罩半径 (屏幕高度比例)
float  uIntensity;   // 整体强度 0~1
float  uAspect;      // 宽高比 width/height
float4 uColor;       // 护罩色 (rgb, a=面板亮度)
float  uHexScale;    // 六边面板密度 (建议 6~16)
float  uRefract;     // 折射强度 (建议 0~1)
float  uFlash;       // 受击面板亮起脉冲 0~1

// 六边形网格距离场: 返回到最近 hex 中心的归一化距离与 cell id
float2 hexDist(float2 p)
{
    const float2 s = float2(1.0, 1.7320508); // (1, sqrt3)
    float2 hC = floor(float2(p.x / s.x, p.y / s.y)) ;
    // 两套偏移取最近
    float2 a = p - float2(0.5, 0.5) * s - float2(floor(p.x / s.x), floor(p.y / s.y)) * s;
    float2 b = p - float2(1.0, 1.0) * s - float2(floor((p.x - 0.5) / s.x), floor((p.y - 0.5) / s.y)) * s;
    float da = length(a);
    float db = length(b);
    return da < db ? float2(da, dot(hC, float2(13.1, 7.7))) : float2(db, dot(hC, float2(17.3, 3.3)) + 1.0);
}

float4 ReflectWardPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return tex2D(uImage0, coords);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);
    float  normDist = dist / max(uRadius, 0.001);

    if (normDist > 1.25)
        return tex2D(uImage0, coords);

    float2 radialDir = normalize(diff + 0.0001);

    // 罩内折射: 沿径向用噪声扰动采样场景
    float n = tex2D(uNoise, coords * 3.0 + float2(uTime * 0.02, -uTime * 0.015)).r;
    float refractFall = smoothstep(1.0, 0.0, normDist);
    float2 uvOffset = radialDir * (n - 0.5) * 0.03 * uRefract * refractFall;
    uvOffset.x /= uAspect;
    float2 sUV = clamp(coords + uvOffset, 0.001, 0.999);
    float4 scene = tex2D(uImage0, sUV);

    // 六边面板格
    float2 hp = (coords * float2(uAspect, 1.0)) * max(uHexScale, 0.001);
    float2 hd = hexDist(hp);
    float panelEdge = smoothstep(0.45, 0.5, hd.x); // 接近 cell 边界变亮
    float panelSeed = frac(sin(hd.y) * 43758.5453);
    float panelPulse = 0.5 + 0.5 * sin(uTime * 2.0 + panelSeed * 6.28318);

    // 仅在罩内显示面板, 边缘最亮
    float shellMask = smoothstep(1.05, 0.85, normDist);          // 罩面
    float rim = smoothstep(0.75, 1.0, normDist) * smoothstep(1.1, 0.95, normDist); // 菲涅尔边

    float panel = panelEdge * shellMask * (0.35 + 0.4 * panelPulse);
    panel += (1.0 - panelEdge) * shellMask * 0.06 * panelPulse;  // 面板内淡色

    // 受击亮起: 全面板一闪
    panel += shellMask * uFlash * (0.4 + 0.6 * panelPulse);

    float3 wardCol = uColor.rgb;
    float3 col = scene.rgb;
    col = lerp(col, wardCol, saturate((panel + rim) * uColor.a) * uIntensity);
    col += wardCol * rim * 0.6 * uIntensity;          // 边缘加法亮边
    col += wardCol * panel * 0.5 * uIntensity;         // 面板加法辉

    return float4(saturate(col), scene.a);
}

technique Technique1
{
    pass ReflectWardPass
    {
        PixelShader = compile ps_3_0 ReflectWardPS();
    }
}
