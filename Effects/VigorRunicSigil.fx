// ============================================================
// 神威·断罪刃 — 断罪法阵 / 符环充能刻度盘
// 多重反向旋转符环 + 充能刻度扇区(玩家可直读下一波攻击规模)
// + 六芒辐条 + 展开动画 + 引爆白闪
// 完全程序化, 载体为 SoftGlow(径向渐变, 提供柔边)
// 用途: 入场开庭法阵 / 符环齐射充能 / 符印落点 / 天罚充能 / 死亡失控阵
// ============================================================

sampler uTexture : register(s0); // SoftGlow 径向渐变载体

float  uTime;          // 动画时间(秒)
float  uProgress;      // 展开进度 0~1 (由内向外旋出)
float  uIntensity;     // 整体强度 0~1
float  uCharge;        // 充能进度 0~1 (刻度扇区依次点亮)
float  uSegments;      // 刻度格数 (符环球数, 如 12)
float  uFlash;         // 引爆/宣判白闪 0~1
float  uSpin;          // 自旋相位 (实例区分/失控抖动)
float4 uColorPrimary;  // 主色 (符金)
float4 uColorSecondary;// 辅色 (符蓝)

// 廉价 hash — 符文刻痕的伪随机长短
float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    return frac(p * (p + p));
}

float4 RunicSigilPS(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.005 || uProgress < 0.01)
        return float4(0, 0, 0, 0);

    float2 centered = uv - 0.5;
    float dist = length(centered);
    float normDist = dist / 0.48;          // 1.0 = quad 满半径
    float angle = atan2(centered.y, centered.x);
    float angNorm = angle / 6.28318 + 0.5; // 0~1

    // —— 展开裁剪: 法阵由内向外旋出, 展开前沿有一圈亮环 ——
    float unfold = uProgress;
    if (normDist > unfold * 1.06 + 0.02)
        return float4(0, 0, 0, 0);
    float front = smoothstep(unfold - 0.10, unfold - 0.02, normDist)
                * (1.0 - smoothstep(unfold, unfold + 0.04, normDist));

    float spinA = uTime * 0.35 + uSpin;        // 外环正转
    float spinB = -uTime * 0.55 - uSpin * 0.7; // 中环反转

    // —— 外边界环 (r≈0.94): 细环 + 符文刻痕 ——
    float ringOuter = smoothstep(0.90, 0.935, normDist) * (1.0 - smoothstep(0.955, 0.99, normDist));
    float tickId = floor(angNorm * 36.0 + spinA * 5.729); // 36 刻痕随环转
    float tickFrac = frac(angNorm * 36.0 + spinA * 5.729);
    float tickLen = 0.22 + hash11(tickId) * 0.55;          // 伪随机长短 = "符文"感
    float runeTick = step(tickFrac, tickLen) * smoothstep(0.86, 0.90, normDist)
                   * (1.0 - smoothstep(0.935, 0.955, normDist));

    // —— 充能刻度扇区 (r 0.76~0.88): 依次点亮, 玩家直读充能程度 ——
    float segBand = smoothstep(0.755, 0.775, normDist) * (1.0 - smoothstep(0.865, 0.885, normDist));
    float segCount = max(uSegments, 1.0);
    float segPos = angNorm * segCount;                 // 第几格 (连续)
    float segIdx = floor(segPos);
    float segFrac = frac(segPos);
    float segGap = smoothstep(0.05, 0.14, segFrac) * (1.0 - smoothstep(0.86, 0.95, segFrac)); // 格间留缝
    float lit = step((segIdx + 0.5) / segCount, uCharge + 0.001);   // 该格是否点亮
    // 正在充能的当前格闪烁
    float curSeg = step(abs(segIdx + 0.5 - uCharge * segCount), 0.5);
    float segPulse = lit + curSeg * (0.3 + 0.3 * sin(uTime * 14.0));
    float chargeSeg = segBand * segGap * segPulse;
    float dimSeg = segBand * segGap * 0.10; // 未点亮格的暗底

    // —— 中环 (r≈0.60): 反向旋转细符纹 ——
    float ringMid = smoothstep(0.575, 0.60, normDist) * (1.0 - smoothstep(0.625, 0.65, normDist));
    float glyphId = floor(angNorm * 20.0 + spinB * 3.183);
    float glyphFrac = frac(angNorm * 20.0 + spinB * 3.183);
    float glyph = step(glyphFrac, 0.30 + hash11(glyphId + 7.0) * 0.4);
    float midRunes = ringMid * (0.35 + glyph * 0.65);

    // —— 六芒辐条: 尖锐角向亮线, 随充能变亮 ——
    float spokes = pow(abs(cos(angle * 3.0 + spinA * 0.5)), 18.0);
    spokes *= smoothstep(0.72, 0.30, normDist) * smoothstep(0.10, 0.22, normDist);
    spokes *= 0.35 + uCharge * 0.65;

    // —— 内核: 反向旋转菱形符印 ——
    float ca = cos(spinB * 1.4);
    float sa = sin(spinB * 1.4);
    float2 rot = float2(centered.x * ca - centered.y * sa, centered.x * sa + centered.y * ca);
    float diamond = (abs(rot.x) + abs(rot.y)) / 0.48;
    float coreRing = smoothstep(0.13, 0.16, diamond) * (1.0 - smoothstep(0.19, 0.23, diamond));
    float coreGlow = pow(saturate(1.0 - normDist * 4.5), 3.0) * (0.5 + uCharge * 0.8);

    // —— 合成 ——
    float shape = max(ringOuter * 0.85, runeTick * 0.9);
    shape = max(shape, chargeSeg);
    shape = max(shape, dimSeg);
    shape = max(shape, midRunes * 0.7);
    shape = max(shape, spokes);
    shape = max(shape, coreRing * 0.8);
    shape = max(shape, coreGlow);
    shape = max(shape, front * 1.2);

    // 颜色: 主金, 点亮刻度与内核偏白热; 中环偏辅蓝
    float3 col = uColorPrimary.rgb;
    col = lerp(col, uColorSecondary.rgb, midRunes * 0.8);
    float hot = saturate(chargeSeg + coreGlow * uCharge + front);
    col = lerp(col, float3(1.0, 0.97, 0.85), hot * 0.55);

    // 引爆白闪: 整阵向白金过曝
    col = lerp(col, float3(1.0, 0.99, 0.92), saturate(uFlash));
    shape += uFlash * pow(saturate(1.0 - normDist), 1.5) * 1.5;

    // 呼吸 + 载体柔边
    float breath = 0.92 + 0.08 * sin(uTime * 2.6 + uSpin);
    float soft = pow(saturate(tex2D(uTexture, uv).r * 2.2), 0.35); // SoftGlow 拉平后当柔边遮罩

    float alpha = saturate(shape * breath) * uIntensity * soft;
    return float4(col * alpha, alpha); // 预乘输出, Additive 友好
}

technique Technique1
{
    pass RunicSigilPass
    {
        PixelShader = compile ps_3_0 RunicSigilPS();
    }
}
