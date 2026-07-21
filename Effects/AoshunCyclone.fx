// ============================================================
// 敖顺龙卷着色器 — 局部四边形绘制（龙卷弹幕 PreDraw, Immediate 批）
// 程序化龙卷漏斗: 柱面明暗卷动 + 双向滚动云带 + 边缘湍流 + 内部电闪
// 四边形 UV: x=横向(0左 1右), y=纵向(0顶 1底/地面)
// ============================================================

sampler uImage0 : register(s0); // 占位像素 (颜色完全程序化)
sampler uNoise  : register(s1); // 共享可平铺三通道 FBM 噪声

float uTime;      // 动画时间 (秒)
float uIntensity; // 整体强度 0~1 (生成/消散渐变)
float uSpin;      // 自旋速度系数 (默认 1)
float uSeed;      // 每实例相位种子
float4 uColorInner; // 云体内侧色 (风暴灰)
float4 uColorRim;   // 边缘/高光色 (雷青)

float4 CyclonePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float x = coords.x * 2.0 - 1.0;   // -1 ~ 1 横向
    float y = coords.y;               // 0 顶 ~ 1 底

    // 漏斗轮廓: 底窄顶宽 (龙卷形), 加噪声起伏
    float profile = lerp(0.30, 1.0, pow(1.0 - y, 1.35));
    float edgeN = tex2D(uNoise, float2(y * 2.2 - uTime * 0.7 * uSpin + uSeed, uSeed * 3.1)).r;
    profile *= 0.86 + edgeN * 0.28;

    float r = abs(x) / max(profile, 0.02); // 0 中轴 ~ 1 轮廓边
    if (r > 1.15)
        return float4(0, 0, 0, 0);

    // ==========================================
    //  柱面卷动 — 用 x/profile 当圆柱展开角, 两层反向滚动云带
    // ==========================================
    float band1 = tex2D(uNoise, float2(r * 0.5 + uTime * 0.85 * uSpin + uSeed, y * 3.0 - uTime * 0.55)).g;
    float band2 = tex2D(uNoise, float2(-r * 0.4 - uTime * 1.30 * uSpin + uSeed * 1.7, y * 5.0 + uTime * 0.35)).b;
    float cloud = band1 * 0.62 + band2 * 0.48;

    // 柱面光照: 中轴亮暗交替模拟圆柱体积 (侧缘压暗)
    float shade = 1.0 - r * r * 0.72;
    // 螺旋条纹: 沿 y 的斜向明暗
    float spiral = 0.5 + 0.5 * sin((y * 9.0 - x * 3.5 - uTime * 5.2 * uSpin + uSeed * 6.28) * 2.0);
    cloud *= 0.72 + spiral * 0.42;

    // ==========================================
    //  边缘湍流撕裂 — 轮廓处密度衰减带噪声
    // ==========================================
    float rimNoise = tex2D(uNoise, float2(y * 4.0 + uTime * 0.9, r * 1.6 + uSeed)).r;
    float body = smoothstep(1.12, 0.78 + rimNoise * 0.18, r);

    float alpha = body * cloud * uIntensity;

    // ==========================================
    //  内部电闪 — 高频窄脉冲随机点亮
    // ==========================================
    float bolt = tex2D(uNoise, float2(uSeed * 5.3, floor(uTime * 9.0) * 0.13)).g;
    float flash = smoothstep(0.78, 0.95, bolt) * (1.0 - r) * uIntensity;

    float3 col = lerp(uColorInner.rgb, uColorRim.rgb, saturate(r * 0.85 + cloud * 0.35)) * shade;
    col += uColorRim.rgb * flash * 1.6;
    // 底部接地扬尘微亮
    col += uColorRim.rgb * smoothstep(0.85, 1.0, y) * 0.22 * uIntensity;

    return float4(col * alpha, alpha) * sampleColor.a;
}

technique Technique1
{
    pass CyclonePass
    {
        PixelShader = compile ps_3_0 CyclonePS();
    }
}
