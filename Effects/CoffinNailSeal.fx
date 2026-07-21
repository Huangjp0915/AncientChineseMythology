// ============================================================
// 七星封棺印记着色器 — 棺材钉 CoffinNail 旗舰峰值 (屏幕空间 decal)
// 中式棺形 SDF (上宽下窄六边) + 合盖扫线 (uProgress) + 北斗七星逐颗点亮 (uStars)
// + 咒纹噪声涌动 + 起爆白闪 (uFlash)
// 载体: 满屏噪声贴图 (s0), 由 ACMShaders.DrawScreenSpaceDecal (Additive) 驱动
// uCenter/uRadius 经 ACMShaders.WorldDecalParams 换算 (屏幕 UV / 高度比例)
// ============================================================

sampler uNoiseTex : register(s0); // 共享可平铺噪声

float  uTime;      // 动画时间(秒)
float  uIntensity; // 整体强度 0~1
float2 uCenter;    // 印记中心 (屏幕 UV)
float  uRadius;    // 半径 (占屏幕高度比例; 棺高的一半)
float  uAspect;    // 屏幕宽高比
float  uProgress;  // 合盖进度 0~1 (顶部向下扫线)
float  uStars;     // 已点亮星数 0~7 (小数=当前星点亮渐变)
float  uFlash;     // 起爆白闪 0~1
float4 uColorMain; // 主色 (致命红)
float4 uColorRim;  // 边缘色 (骨白)

// 中式棺材轮廓 SDF (局部坐标 p: x∈[-1,1] 宽向, y∈[-1,1] 高向, y=-1 顶/大头)
// 由上下两段梯形拼合: 顶宽 0.62, 肩宽(y=-0.25) 0.78, 底宽 0.38
float CoffinSDF(float2 p)
{
    float w;
    if (p.y < -0.25)
        w = lerp(0.62, 0.78, (p.y + 1.0) / 0.75);   // 顶 → 肩 渐宽
    else
        w = lerp(0.78, 0.38, (p.y + 0.25) / 1.25);  // 肩 → 底 收窄
    float dx = abs(p.x) - w;
    float dy = abs(p.y) - 1.0;
    return max(dx, dy); // 负值在棺形内
}

// 北斗七星在棺盖上的排布 (勺形, 局部坐标)
static const float2 STAR_POS[7] = {
    float2(-0.42, -0.62), // 天枢 (勺口)
    float2(-0.18, -0.50),
    float2( 0.04, -0.42),
    float2( 0.22, -0.28), // 天权 (勺底转折)
    float2( 0.34, -0.02),
    float2( 0.30,  0.30),
    float2( 0.16,  0.58)  // 摇光 (柄尾)
};

float4 PS_CoffinSeal(float2 coords : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    // 屏幕 UV → 以印记为中心的局部坐标 (等比, y 半径=uRadius)
    float2 d = coords - uCenter;
    d.x *= uAspect;
    float2 p = d / max(uRadius, 0.001); // p.y ∈ [-1,1] 覆盖棺高

    if (abs(p.x) > 1.6 || abs(p.y) > 1.6)
        return float4(0, 0, 0, 0);

    float sdf = CoffinSDF(p);

    // 咒纹噪声 (缓慢涌动)
    float2 n1 = p * 0.9 + float2(uTime * 0.05, -uTime * 0.03);
    float noise = tex2D(uNoiseTex, n1).r;

    // ---- 1) 棺形轮廓线: 内缘描边 + 噪声呼吸 ----
    float outline = 1.0 - smoothstep(0.0, 0.045, abs(sdf));
    outline *= 0.75 + 0.25 * sin(uTime * 4.0 + noise * 6.283);

    // ---- 2) 合盖扫线: 顶部向下推进的横亮线 + 已合部分微亮填充 ----
    float lidY = lerp(-1.15, 1.05, saturate(uProgress)); // 扫线当前高度
    float sweep = exp(-pow((p.y - lidY) * 9.0, 2.0));    // 扫线亮带
    float closed = step(p.y, lidY);                       // 已合区域
    float inside = step(sdf, 0.0);
    float fill = inside * closed * (0.10 + 0.08 * noise); // 已合区微亮咒纹
    sweep *= inside + outline * 0.5;

    // ---- 3) 北斗七星逐颗点亮 ----
    float stars = 0.0;
    for (int i = 0; i < 7; i++) {
        float lit = saturate(uStars - (float)i); // 第 i 颗点亮度 0~1
        if (lit <= 0.0) continue;
        float2 sp = p - STAR_POS[i];
        float sd = length(sp);
        // 星核 + 十字光芒
        float core = exp(-sd * sd * 260.0) * 1.6;
        float cross1 = exp(-abs(sp.x) * 34.0) * exp(-abs(sp.y) * 8.0);
        float cross2 = exp(-abs(sp.y) * 34.0) * exp(-abs(sp.x) * 8.0);
        float tw = 0.85 + 0.15 * sin(uTime * 9.0 + (float)i * 1.7);
        stars += (core + (cross1 + cross2) * 0.4) * lit * tw;
    }

    // ---- 4) 星连线 (点亮进度沿勺形逐段亮起) ----
    float lines = 0.0;
    for (int j = 0; j < 6; j++) {
        float seg = saturate(uStars - (float)(j + 1)); // 第 j 段亮度
        if (seg <= 0.0) continue;
        float2 a = STAR_POS[j];
        float2 b = STAR_POS[j + 1];
        float2 ab = b - a;
        float t = saturate(dot(p - a, ab) / dot(ab, ab));
        float ld = length(p - (a + ab * t));
        lines += exp(-ld * ld * 900.0) * seg * 0.55;
    }

    // ---- 组合 ----
    float3 col = uColorMain.rgb * (outline * 0.9 + fill + sweep * 0.8)
               + uColorRim.rgb * (stars + lines);
    // 起爆白闪: 全印记向白热爆冲
    col += (uColorRim.rgb * 0.4 + 0.6) * uFlash * (inside * 0.8 + outline);

    float alpha = (outline * 0.85 + fill + sweep * 0.7 + stars * 0.9 + lines + uFlash * inside * 0.8);
    alpha = saturate(alpha) * uIntensity;

    return float4(saturate(col) * alpha, 0.0); // 加法混合: rgb 预乘, a=0
}

technique Technique1
{
    pass CoffinSealPass
    {
        PixelShader = compile ps_3_0 PS_CoffinSeal();
    }
}
