// ============================================================
// 敖广·浪墙 — 屏幕空间 SDF 巨浪 (整面浪墙 + 穿越缺口)
// 供 TsunamiWall(横扫) / AoGuangSkyDeluge(天倾竖落) / 相变浪墙复用
// 数学: p 投影到行进方向 uDir 得 s(厚度轴), 投影到垂直方向得 t(墙面轴)
//   |s| < uHalfThick 为浪体; s 靠近 +uHalfThick(前沿) 出浪头泡沫+致命亮线
//   |t - uGapCenter| < uGapHalf 为穿越缺口, 缺口边缘 Safe 翠玉描边
// 喂可平铺噪声(s0), 经 ACMShaders.DrawScreenSpaceDecal 全屏绘制 (AlphaBlend)
// ============================================================

sampler uNoise : register(s0); // 可平铺三通道噪声

float  uTime;        // 动画时间 (秒)
float  uIntensity;   // 整体强度 0~1 (成型/消散)
float  uAspect;      // 宽高比 width/height
float2 uDir;         // 行进方向单位向量 (屏幕空间, y 向下)
float2 uLinePoint;   // 浪墙中心线上一点 (归一化屏幕 UV)
float  uHalfThick;   // 浪体半厚 (屏幕高度比例)
float  uGapCenter;   // 缺口中心在墙面轴上的坐标 (相对 uLinePoint, 屏幕高度比例)
float  uGapHalf;     // 缺口半宽 (0 = 无缺口)
float  uWarnOnly;    // 1 = 预警模式 (半透明幕布, 不画浪体细节)
float2 uHalfDir;     // 半场遮罩方向 (指向危险半场; (0,0)=不启用) — 天倾竖落用
float4 uColorDeep;   // 浪体深水色
float4 uColorCrest;  // 浪头亮色
float4 uColorSafe;   // 缺口安全描边色
float4 uColorLethal; // 前沿致命亮线色

float4 TidalWallPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 origin = float2(uLinePoint.x * uAspect, uLinePoint.y);
    float2 dirN   = normalize(uDir + 0.0001);
    float2 perp   = float2(-dirN.y, dirN.x);

    float2 rel = pos - origin;
    float s = dot(rel, dirN);   // 厚度轴: + 为前沿方向
    float t = dot(rel, perp);   // 墙面轴

    // —— 预警幕布模式: 危险半场整面淡红呼吸, 无浪体 ——
    if (uWarnOnly > 0.5) {
        // s < 0 为被标记的危险区 (uLinePoint 即分界线, uDir 指向安全侧)
        float inside = smoothstep(0.015, -0.015, s);
        float breathe = 0.75 + 0.25 * sin(uTime * 6.0);
        float2 wUV = pos * 1.6 + float2(0, -uTime * 0.12);
        float wn = tex2D(uNoise, wUV).r;
        float a = inside * uIntensity * (0.10 + wn * 0.06) * breathe;
        // 分界线亮边
        float borderGlow = exp(-abs(s) * 90.0) * uIntensity * 0.8 * breathe;
        float3 col = uColorLethal.rgb * (a + borderGlow);
        return float4(col, a * 0.9 + borderGlow * 0.6);
    }

    // 浪头前沿噪声起伏 (浪尖参差)
    float crestNoise = tex2D(uNoise, float2(t * 1.4 + uTime * 0.20, uTime * 0.08)).r;
    float frontEdge = uHalfThick + (crestNoise - 0.5) * uHalfThick * 0.5;

    // 早退: 远离浪体
    if (s > frontEdge + 0.06 || s < -uHalfThick * 1.4)
        return float4(0, 0, 0, 0);

    // —— 浪体掩码: 后缘柔和拖尾, 前缘锐利 ——
    float bodyMask = smoothstep(-uHalfThick * 1.3, -uHalfThick * 0.5, s)
                   * smoothstep(frontEdge, frontEdge - 0.02, s);

    // —— 缺口: 墙面轴上的洞, 边缘噪声轻微摆动 ——
    float gapMask = 1.0;
    float safeRim = 0.0;
    if (uGapHalf > 0.001) {
        float gWobble = (tex2D(uNoise, float2(uTime * 0.11, t * 0.7)).g - 0.5) * 0.012;
        float gd = abs(t - uGapCenter + gWobble);
        gapMask = smoothstep(uGapHalf * 0.55, uGapHalf, gd);              // 洞内=0
        safeRim = exp(-abs(gd - uGapHalf) * 110.0) * (0.7 + 0.3 * sin(uTime * 5.0));
    }

    // —— 半场遮罩: 浪体只存在于危险半场, 分界处 Safe 描边 ——
    float halfRim = 0.0;
    if (dot(uHalfDir, uHalfDir) > 0.01) {
        float2 halfDirA = normalize(float2(uHalfDir.x, uHalfDir.y));
        float h = dot(rel, halfDirA);
        gapMask *= smoothstep(-0.006, 0.03, h);
        halfRim = exp(-abs(h) * 110.0) * (0.7 + 0.3 * sin(uTime * 5.0));
        safeRim = max(safeRim, halfRim);
    }

    float wall = bodyMask * gapMask;

    // —— 浪体内部: 深浅水流层 (沿厚度轴翻卷) ——
    float2 fUV1 = float2(t * 1.8 - uTime * 0.25, s * 5.0 + uTime * 0.35);
    float2 fUV2 = float2(t * 3.1 + uTime * 0.14, s * 8.0 - uTime * 0.5);
    float flow = tex2D(uNoise, fUV1).b * 0.6 + tex2D(uNoise, fUV2).g * 0.5;

    // —— 浪头泡沫带: 前沿附近被噪声打碎 ——
    float crestBand = smoothstep(frontEdge - uHalfThick * 0.55, frontEdge, s);
    float foam = crestBand * smoothstep(0.42, 0.72, flow);

    // —— 前沿致命亮线 (红=致命, 明示伤害边界) ——
    float lethalLine = exp(-abs(s - frontEdge) * 130.0) * gapMask;

    // —— 合成 ——
    float3 col = uColorDeep.rgb * (0.55 + flow * 0.5);
    col = lerp(col, uColorCrest.rgb, foam * 0.9 + crestBand * 0.25);
    col += uColorLethal.rgb * lethalLine * 1.2;
    col += uColorSafe.rgb * safeRim * bodyMask;

    float alpha = wall * uIntensity * (uColorDeep.a * 0.75 + foam * 0.35);
    alpha += lethalLine * uIntensity * 0.85;
    alpha += safeRim * bodyMask * uIntensity * 0.7;
    alpha = saturate(alpha);

    return float4(col * alpha, alpha); // 预乘输出, AlphaBlend 下不糊底
}

technique Technique1
{
    pass TidalWallPass
    {
        PixelShader = compile ps_3_0 TidalWallPS();
    }
}
