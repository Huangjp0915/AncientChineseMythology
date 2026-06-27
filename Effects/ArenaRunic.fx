// ============================================================
// 通用地纹/法阵/牢笼着色器 — 屏幕空间 SDF + 噪声
// 由投射物载体全屏绘制噪声贴图驱动; 复用 DazhengArenaCircle 思路并泛化
//   uMode: 0=地纹法阵(环+符文+落点) 1=牢笼罩(prison-overlay: 笼栏+穹顶)
// 换 uColorPrimary/uColorSecondary + uRuneFreq 即得不同主题结界
// 喂可平铺噪声贴图(s0)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB三通道独立)

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径 (屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float4 uColorPrimary;    // 主色
float4 uColorSecondary;  // 辅色
float  uRuneFreq;        // 符文/纹路频率 (建议 6~16)
float  uMode;            // 0=法阵地纹 1=牢笼罩
float  uShape;           // 边界形状 0=圆形(默认) 1=矩形/方形 (用 chebyshev 距离, 方形竞技场如旱魃 800 半笼)

float4 ArenaRunicPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    // 形状: 圆=欧氏距离; 矩形=chebyshev(max) 距离 → 等距线为正方形, 方形竞技场边界正确
    float  dist   = (uShape > 0.5) ? max(abs(diff.x), abs(diff.y)) : length(diff);

    float breath = sin(uTime * 1.5) * 0.005 * uRadius;
    float effRadius = uRadius + breath;
    float normDist = dist / max(effRadius, 0.001);

    bool prison = uMode > 0.5;

    // 法阵模式: 只算圆环带, 大片早退; 牢笼模式: 覆盖整个内部到边界
    if (!prison && (normDist > 1.6 || normDist < 0.4))
        return float4(0, 0, 0, 0);
    if (prison && normDist > 1.7)
        return float4(0, 0, 0, 0);

    float angle   = atan2(diff.y, diff.x);
    float angNorm = angle / 6.28318 + 0.5;
    float freq    = max(uRuneFreq, 0.001);

    // 多八度噪声
    float2 n1UV = coords * 3.0 + float2(uTime * 0.04, uTime * 0.03);
    float2 n2UV = coords * 5.5 + float2(-uTime * 0.05, uTime * 0.04);
    float2 n3UV = coords * 1.8 + float2(uTime * 0.02, -uTime * 0.03);
    float n1 = tex2D(uImage0, n1UV).r;
    float n2 = tex2D(uImage0, n2UV).g;
    float n3 = tex2D(uImage0, n3UV).b;
    float fbm = n1 * 0.5 + n2 * 0.3 + n3 * 0.2;

    float warp = (fbm - 0.5) * 0.13;
    float dN = normDist + warp;

    // 主边界环
    float th = 0.06;
    float ringIn  = smoothstep(1.0 - th * 2.5, 1.0 - th * 0.3, dN);
    float ringOut = 1.0 - smoothstep(1.0 + th * 0.3, 1.0 + th * 2.5, dN);
    float ring = ringIn * ringOut;

    float shape = ring;
    float3 baseColor = lerp(uColorPrimary.rgb, uColorSecondary.rgb, smoothstep(0.38, 0.72, fbm));

    if (!prison)
    {
        // —— 法阵地纹: 环绕符文 + 内向根须 + 落点闪斑 ——
        float2 rUV = float2(angNorm * freq + uTime * 0.06, normDist * 3.0 - uTime * 0.04);
        float rune = tex2D(uImage0, rUV).r;
        float runeShape = smoothstep(0.45, 0.80, rune);
        float runeMask = smoothstep(1.0 + th * 4.5, 1.0, dN) * smoothstep(1.0 - th * 4.5, 1.0, dN);
        runeShape *= runeMask;

        // 内圈符文环(同心)
        float innerRing = 0.5 + 0.5 * sin(normDist * freq * 1.5 - uTime * 1.2);
        innerRing = pow(innerRing, 6.0) * smoothstep(0.95, 0.5, normDist) * smoothstep(0.4, 0.55, normDist);

        // 角向辐条(法阵格)
        float spokes = pow(abs(cos(angle * floor(freq * 0.5))), 12.0);
        spokes *= smoothstep(0.95, 0.45, normDist) * 0.5;

        float2 sparkleUV = float2(angNorm * freq * 2.0 + uTime * 0.12, normDist * 12.0);
        float sparkle = tex2D(uImage0, sparkleUV).r;
        sparkle = smoothstep(0.86, 0.96, sparkle) * ring * 0.35;

        shape = max(ring, max(runeShape * 0.6, max(innerRing, max(spokes, sparkle))));
        baseColor += sparkle * float3(0.15, 0.12, 0.0);
    }
    else
    {
        // —— 牢笼罩: 内部竖向笼栏 + 穹顶网格, 半透封锁感 ——
        float bars = abs(sin(angNorm * freq * 3.14159));
        bars = smoothstep(0.78, 0.97, bars);
        float barMask = smoothstep(1.05, 0.2, normDist); // 笼内可见
        float cage = bars * barMask * 0.5;

        // 穹顶网格(同心 + 角向)
        float dome = 0.5 + 0.5 * sin(normDist * freq * 2.0 - uTime * 0.8);
        dome = pow(dome, 8.0) * smoothstep(1.05, 0.3, normDist) * 0.4;

        // 内部弥漫压迫色(很淡)
        float fill = smoothstep(1.05, 0.0, normDist) * 0.10;

        shape = max(ring, max(cage, max(dome, fill)));
    }

    float pulse = sin(uTime * 2.2 + angle * 3.0) * 0.1 + 0.9;
    float edgeWarn = smoothstep(0.78, 1.0, normDist) * 0.07;
    float outerGlow = saturate(1.0 - smoothstep(1.0, 1.22, normDist))
                    * smoothstep(1.0, 1.015, normDist) * 0.10;

    float alpha = saturate((shape * pulse + edgeWarn + outerGlow) * uIntensity);
    return float4(baseColor, alpha);
}

technique Technique1
{
    pass ArenaRunicPass
    {
        PixelShader = compile ps_3_0 ArenaRunicPS();
    }
}
