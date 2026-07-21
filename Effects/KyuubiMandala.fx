// ============================================================
// 九尾狐 · 九曜狐火法阵着色器 — 屏幕空间地纹 (ArenaRunic 同用法)
// 载体: 全屏噪声贴图 (DrawScreenSpaceDecal), uCenter/uRadius 走屏幕 UV
// 九瓣玫瑰线 + 九边形界墙基线 + 旋转符环 + 花瓣脉冲
// 用于: 狐火曼陀罗 set-piece 地纹 / 入场·转阶段·终结技·死亡的法阵节拍
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径 (屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比 width/height
float4 uColorPrimary;    // 主色 (狐火金 / 紫红)
float4 uColorSecondary;  // 辅色
float  uRotation;        // 整阵旋转角 (与曼陀罗墙同步)
float  uGapIndex;        // 安全缺口边索引 0~8 (-1 = 无缺口)
float  uPetalPulse;      // 花瓣脉冲 0~1 (脉冲瞬间盛放)

static const float TwoPi = 6.28318530;
static const float Sector = 0.69813170; // 2π/9

float4 KyuubiMandalaPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);

    float normDist = dist / max(uRadius, 0.001);
    if (normDist > 1.45)
        return float4(0, 0, 0, 0);

    // 阵内极角 (随整阵旋转)
    float angle = atan2(diff.y, diff.x) - uRotation;

    // 共享 FBM (扭曲 + 符文采样源)
    float2 n1UV = coords * 2.6 + float2(uTime * 0.05, -uTime * 0.03);
    float2 n2UV = coords * 5.0 + float2(-uTime * 0.04, uTime * 0.05);
    float fbm = tex2D(uImage0, n1UV).r * 0.6 + tex2D(uImage0, n2UV).g * 0.4;
    float dN = normDist + (fbm - 0.5) * 0.05;

    // —— 九边形界墙基线: 扇区折叠得多边形距离 ——
    float aSec = fmod(angle + TwoPi * 2.0, Sector);          // 扇区内角 0~Sector
    float aMid = aSec - Sector * 0.5;
    float polyDist = normDist * cos(aMid) / cos(Sector * 0.5); // 九边形归一距离
    float wall = smoothstep(0.045, 0.012, abs(polyDist - 1.0));

    // 缺口边削弱 (与边墙投射物的安全缝对齐)
    float sectorIdx = floor(fmod(angle + TwoPi * 2.0, TwoPi) / Sector);
    if (uGapIndex >= 0.0 && abs(sectorIdx - uGapIndex) < 0.5)
        wall *= 0.15;

    // —— 九瓣玫瑰线: r = |cos(4.5θ)| 内阵花纹 ——
    float rose = abs(cos(angle * 4.5));
    float roseR = 0.30 + rose * 0.42;
    float petalTh = 0.028 + uPetalPulse * 0.02;
    float petal = smoothstep(petalTh, petalTh * 0.3, abs(dN - roseR));
    petal *= 0.55 + uPetalPulse * 0.75;

    // —— 双旋转符环 (角向噪声符文, 内外反向转) ——
    float runeA = tex2D(uImage0, float2(angle / TwoPi * 6.0 + uTime * 0.05, 0.31)).r;
    float ringA = smoothstep(0.026, 0.008, abs(dN - 0.62)) * smoothstep(0.42, 0.72, runeA);
    float runeB = tex2D(uImage0, float2(-angle / TwoPi * 4.0 + uTime * 0.04, 0.77)).g;
    float ringB = smoothstep(0.022, 0.007, abs(dN - 0.335)) * smoothstep(0.40, 0.70, runeB);

    // —— 九辐条: 指向九顶点的细线 ——
    float spoke = pow(abs(cos(angle * 4.5 + Sector * 2.25)), 48.0);
    spoke *= smoothstep(1.02, 0.25, normDist) * smoothstep(0.10, 0.28, normDist) * 0.5;

    // —— 中心狐眼: 小同心环 ——
    float eye = smoothstep(0.020, 0.006, abs(dN - 0.085)) * 0.8;

    float shape = max(wall, max(petal, max(ringA, max(ringB, max(spoke, eye)))));

    // 花瓣脉冲时整阵提亮 + 微呼吸
    float breath = 0.9 + 0.1 * sin(uTime * 2.4 + normDist * 5.0);
    shape *= breath * (1.0 + uPetalPulse * 0.5);

    float3 col = lerp(uColorSecondary.rgb, uColorPrimary.rgb, saturate(shape * 1.2));
    // 界墙用主色高亮
    col = lerp(col, uColorPrimary.rgb, wall * 0.6);

    float a = saturate(shape * uIntensity);
    return float4(col * a, a) * sampleColor;
}

technique Technique1
{
    pass KyuubiMandalaPass
    {
        PixelShader = compile ps_3_0 KyuubiMandalaPS();
    }
}
