// ============================================================
// 天庭监察者 — 演算法阵 (OverseerCalcRing)
// "静止悬浮演算": 外 60 刻度环 + 反转 36 刻度内环 + 雷达指针余辉
// + 旋转正 n 边形线框(n 随阶段 3/4/6) + 十字准线 + 数据符文闪点
// uSpin 由 CPU 积分(蓄力 t³ 加速 / 骤停即停), uCollapse 骤停熄灭+收缩
// 屏幕空间 decal, Additive 绘制; 喂共享可平铺噪声(s0)
// ============================================================

sampler uImage0 : register(s0); // 可平铺噪声

float  uTime;            // 动画时间(秒)
float2 uCenter;          // 中心归一化屏幕坐标 0~1
float  uRadius;          // 半径 (屏幕高度比例)
float  uIntensity;       // 整体强度 0~1
float  uAspect;          // 宽高比
float  uSpin;            // 主自转角 (CPU 积分, 弧度)
float  uCharge;          // 演算进度 0~1 (亮度/密度)
float  uCollapse;        // 骤停坍缩 0~1 (熄灭 + 半径收缩)
float  uSides;           // 内接正多边形边数 (3/4/6)
float4 uColorPrimary;    // 机关金
float4 uColorSecondary;  // 玉色

#define TAU 6.28318530

// 环带窗口: 以 c 为中心、w 为半宽的软带
float ringBand(float x, float c, float w)
{
    return smoothstep(c - w, c - w * 0.25, x) * (1.0 - smoothstep(c + w * 0.25, c + w, x));
}

float4 OverseerCalcRingPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 pos    = float2(coords.x * uAspect, coords.y);
    float2 center = float2(uCenter.x * uAspect, uCenter.y);
    float2 diff   = pos - center;
    float  dist   = length(diff);

    float effR = max(uRadius * (1.0 - 0.55 * uCollapse), 0.001);
    float nd = dist / effR;
    if (nd > 1.45)
        return float4(0, 0, 0, 0);

    float angle = atan2(diff.y, diff.x);

    // ---------- 外 60 刻度环 (随 uSpin 正转) ----------
    float a1 = (angle - uSpin) / TAU;
    float tick60 = step(0.80, frac(a1 * 60.0));
    float outerRing = ringBand(nd, 1.0, 0.055);
    float outer = tick60 * outerRing;
    // 外环基线 (细)
    outer += ringBand(nd, 1.0, 0.012) * 0.55;

    // ---------- 反转 36 刻度内环 ----------
    float a2 = (angle + uSpin * 1.6) / TAU;
    float tick36 = step(0.74, frac(a2 * 36.0));
    float inner = tick36 * ringBand(nd, 0.72, 0.045);
    inner += ringBand(nd, 0.72, 0.010) * 0.4;

    // ---------- 雷达指针 + 拖尾余辉 ----------
    float rel = frac((angle - uSpin) / TAU);          // 指针后方 0→1
    float sweep = pow(1.0 - rel, 7.0);                // 指针处最亮, 余辉快速衰减
    float sweepMask = smoothstep(1.02, 0.16, nd) * step(nd, 1.0);
    float radar = sweep * sweepMask * 0.55;
    // 指针线本体
    float pointerLine = pow(saturate(1.0 - rel * 30.0), 2.0) * sweepMask;
    radar += pointerLine * 0.8;

    // ---------- 正 n 边形线框 (半速反向旋转) ----------
    float n = max(uSides, 3.0);
    float kk = 3.14159265 / n;
    float am = fmod(abs(angle + uSpin * 0.5) + kk, 2.0 * kk) - kk;
    float rPoly = 0.46 * cos(kk) / max(cos(am), 0.001);   // 归一化多边形半径
    float poly = 1.0 - smoothstep(0.0, 0.028, abs(nd - rPoly));
    poly *= step(nd, 1.0);

    // ---------- 十字准线 (慢旋) ----------
    float ca = angle - uSpin * 0.25;
    float crossLine = max(
        1.0 - smoothstep(0.004, 0.030, abs(sin(ca))),
        1.0 - smoothstep(0.004, 0.030, abs(cos(ca))));
    crossLine *= smoothstep(1.0, 0.25, nd) * 0.30;

    // ---------- 数据符文闪点 (外环外带) ----------
    float aN = frac((angle) / TAU + 0.5);
    float2 runeUV = float2(aN * 14.0 + uSpin * 0.35, nd * 2.0);
    float rune = tex2D(uImage0, runeUV).r;
    float glyph = step(0.78, rune) * ringBand(nd, 1.16, 0.075);

    // ---------- 合成 ----------
    float density = 0.30 + 0.70 * saturate(uCharge);
    float shape = outer * 1.00
                + inner * 0.85
                + radar * (0.35 + 0.65 * uCharge)
                + poly * 0.95
                + crossLine
                + glyph * 0.8;

    float fade = (1.0 - uCollapse * 0.88);
    float alpha = saturate(shape * density * uIntensity * fade);

    // 色: 玉→金随演算进度升温, 指针/闪点偏白热
    float3 baseCol = lerp(uColorSecondary.rgb, uColorPrimary.rgb, saturate(0.25 + uCharge * 0.75));
    float3 hot = float3(1.0, 0.97, 0.88);
    float3 col = lerp(baseCol, hot, saturate(pointerLine + glyph * 0.6) * 0.6);

    // Additive 输出: 预乘
    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass OverseerCalcRingPass
    {
        PixelShader = compile ps_3_0 OverseerCalcRingPS();
    }
}
