// ============================================================
// NetherDragonGate.fx — 幽冥龙·冥界之门 屏幕空间 SDF 贴花
// 三拍语言: 裂缝生长(uCrack, 紫→红 uCrackRed) → 空间破开(uOpen, 竖形
// 杏仁状虚空 + 鬼焰明沿 + 内域星渊旋涡) → 合拢/枯萎(uDissolve)
// 载体: 满屏共享噪声(s0), 世界定位经 ACMShaders.WorldDecalParams
// 建议混合: AlphaBlend (虚空暗核需要遮蔽背景, 形成"世界上的洞")
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声 (RGB 三通道独立)

float  uTime;         // 动画时间(秒)
float2 uCenter;       // 门心归一化屏幕坐标 0~1
float  uRadius;       // 门半高 (屏幕高度比例)
float  uIntensity;    // 整体强度 0~1
float  uAspect;       // 宽高比 width/height
float  uDir;          // 门轴朝向(rad): 龙将沿该方向轰出, 门面垂直于它
float  uCrack;        // 裂缝生长 0~1 (telegraph 第一拍)
float  uCrackRed;     // 裂缝致命化 0(幽紫)~1(纯红, 最后收口)
float  uOpen;         // 开裂度 0(闭)~1(全开)
float  uDissolve;     // 枯萎消散 0~1 (假门/收尾)
float4 uColorRim;     // 明沿鬼焰色 (鬼绿)
float4 uColorCrack;   // 裂缝预备色 (幽蓝紫)
float4 uColorDeep;    // 门内深渊底色 (暗紫)

float4 NetherGatePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  distN  = length(diff) / max(uRadius, 0.001);

    // 远场早退
    if (distN > 2.0)
        return float4(0, 0, 0, 0);

    // 旋到门局部空间: y=门长轴, x=门面法向(龙出方向)
    float ca = cos(uDir), sa = sin(uDir);
    float2 lp = float2(diff.x * ca + diff.y * sa, -diff.x * sa + diff.y * ca) / max(uRadius, 0.001);

    // —— 杏仁状门形 SDF: 竖缝随 uOpen 张开 ——
    float halfW = 0.05 + 0.50 * uOpen;              // 闭=细缝, 开=杏仁
    float2 q = float2(lp.x / halfW, lp.y);
    float d = length(q);                             // <1 = 门内

    // 噪声(门局部空间, 保证随门朝向稳定)
    float n1 = tex2D(uImage0, lp * 0.9 + float2(0.13, uTime * 0.03)).r;
    float n2 = tex2D(uImage0, lp * 2.2 + float2(-uTime * 0.05, 0.41)).g;
    float fbm = n1 * 0.6 + n2 * 0.4;

    // ============ 第一拍: 裂缝 (uCrack) ============
    float crackLayer = 0.0;
    float3 crackCol = lerp(uColorCrack.rgb, float3(0.98, 0.16, 0.22), uCrackRed);
    if (uCrack > 0.001)
    {
        // 中央主缝: 竖向细线, 长度随 uCrack 生长
        float seam = smoothstep(0.030, 0.004, abs(lp.x))
                   * smoothstep(uCrack * 1.15, uCrack * 0.55, abs(lp.y));

        // 分叉裂纹: 噪声等值脊线, 自中心向外随 uCrack 显影
        float ridge = 1.0 - smoothstep(0.0, 0.055, abs(n1 - 0.5));
        float reach = smoothstep(uCrack * 1.45, uCrack * 1.45 - 0.4, length(lp));
        float branches = ridge * reach * smoothstep(1.35, 0.2, length(lp));

        // 濒开脉冲: 越接近致命越急促
        float pulse = 0.72 + 0.28 * sin(uTime * (6.0 + uCrackRed * 9.0) + lp.y * 4.0);
        crackLayer = saturate(seam * 1.2 + branches * 0.85) * pulse * uCrack;
        crackLayer *= (1.0 - uOpen);                 // 门开后裂缝被虚空替代
    }

    // ============ 第二拍: 破开的门 (uOpen) ============
    float3 col = crackCol * crackLayer;
    float alpha = crackLayer * 0.85;

    if (uOpen > 0.001 && d < 1.55)
    {
        // 内域: 深渊旋涡 (极坐标扭转, 内快外慢) + 星点
        float ang = atan2(q.y, q.x);
        float twist = (1.4 - saturate(d)) * 3.0;
        float2 pol = float2(ang / 6.28318 + 0.5, d * 0.55);
        float sw1 = tex2D(uImage0, pol * float2(2.0, 2.6) + float2(uTime * 0.16 + twist * 0.1, -uTime * 0.12)).r;
        float sw2 = tex2D(uImage0, pol * float2(3.7, 1.5) + float2(-uTime * 0.11, uTime * 0.08 + twist * 0.05)).b;
        float swirl = sw1 * 0.6 + sw2 * 0.4;

        float stars = tex2D(uImage0, lp * 3.4 + float2(uTime * 0.01, 0.0)).b;
        stars = smoothstep(0.86, 0.985, stars);

        float dN = d + (fbm - 0.5) * 0.16;           // 噪声撕裂边界
        float core = smoothstep(1.0, 0.32, dN);      // 门内暗核

        // 明沿: 鬼焰呼吸 (开门瞬间最亮)
        float rim = exp(-abs(dN - 1.0) * 8.5);
        rim *= 0.8 + 0.4 * sin(uTime * 3.4 + ang * 3.0) + fbm * 0.5;

        float3 voidCol = uColorDeep.rgb * (0.10 + swirl * 0.22)
                       + uColorRim.rgb * stars * core * 0.9
                       + lerp(uColorDeep.rgb, uColorRim.rgb, 0.75) * swirl * swirl * 0.35;
        float3 rimCol = lerp(uColorRim.rgb, float3(1.0, 1.0, 0.95), 0.25 * uOpen);

        float voidA = core * uOpen * 0.94;
        float rimA  = saturate(rim) * uOpen;

        col   = col * (1.0 - voidA) + voidCol * voidA + rimCol * rimA;
        alpha = saturate(alpha * (1.0 - voidA) + voidA + rimA * uColorRim.a);
    }

    // ============ 枯萎消散 (假门/收尾) ============
    if (uDissolve > 0.001)
    {
        float dis = smoothstep(uDissolve, uDissolve + 0.25, n2);
        col *= dis;
        alpha *= dis;
    }

    alpha = saturate(alpha) * uIntensity;
    return float4(col * uIntensity, alpha);
}

technique Technique1
{
    pass NetherGatePass
    {
        PixelShader = compile ps_3_0 NetherGatePS();
    }
}
