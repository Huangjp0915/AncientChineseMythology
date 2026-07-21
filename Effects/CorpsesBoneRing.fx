// ============================================================
// 尸骸·白骨预警/冲击 — 屏幕空间多模式 decal (不读 screenTarget, 不占全屏名额)
// uMode 0 = 落点光柱 (崩掌拍落 / 骨雨 Marker 的致命预警)
// uMode 1 = 扩张骨裂冲击环 (拍落 / 合掌 impact 反馈)
// uMode 2 = 轴线束 (合掌夹击 / 白骨横扫 / 旋冢收口的轴向预警)
// 调用: ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive)
// s0 = ACMShaders.NoiseTexture (满屏载体, LinearWrap)
// ============================================================

sampler uNoise : register(s0);

float uTime;        // 秒
float2 uCenter;     // 中心 (屏幕 uv)
float uRadius;      // 半宽/环半径 (占屏高比例)
float uIntensity;   // 0~1
float uAspect;      // width/height
float uMode;        // 0/1/2
float uProgress;    // 0~1 渐强(预警) / 扩张进度(冲击环)
float2 uDir;        // 轴向单位向量 (mode 2)
float uHalfLen;     // 轴线半长 (占屏高比例, mode 2)
float4 uColorMain;  // 主色 (预警=Lethal, 冲击=骨白)
float4 uColorEdge;  // 缘色 (鬼绿/幽紫)

// —— 落点光柱: 底部亮核 + 向上渐隐柱体 + 噪声蚀边 ——
float4 Pillar(float2 p, float2 uv)
{
    float w = uRadius * (0.30 + 0.70 * uProgress);
    float ax = abs(p.x);
    // 柱体横向: 核线 + 软边
    float body = smoothstep(w, w * 0.15, ax);
    float core = smoothstep(w * 0.30, 0.0, ax);
    // 纵向: 底部(0)最亮, 向上 1.1 屏高内渐隐; 底部以下快速截止
    float up = saturate(-p.y / 1.10);
    float vert = (p.y <= 0.02) ? (1.0 - up) * (1.0 - up) : saturate(1.0 - p.y / 0.06);
    // 噪声蚀边 (向上流动, 骨灰质感)
    float n = tex2D(uNoise, float2(uv.x * 3.0, uv.y * 2.2 + uTime * 0.35)).r;
    body *= 0.55 + 0.45 * n;
    // 底部落点亮斑
    float spot = smoothstep(w * 2.2, 0.0, length(p * float2(1.0, 2.4)));

    float a = (body * 0.55 + core * 0.85) * vert + spot * 0.9;
    float3 col = uColorMain.rgb * (core * vert + spot) + lerp(uColorEdge.rgb, uColorMain.rgb, 0.5) * body * vert;
    a *= uIntensity * (0.35 + 0.65 * uProgress);
    return float4(col * a, a);
}

// —— 扩张骨裂冲击环: 锋利前缘 + 内侧拖影 + 角向骨裂调制 ——
float4 Ring(float2 p, float2 uv)
{
    float r = length(p);
    float ring = uRadius * max(uProgress, 0.02);
    float d = r - ring;
    // 前缘窄带
    float band = smoothstep(0.030, 0.0, abs(d));
    // 内侧拖影 (环后余波)
    float wake = (d < 0.0) ? exp(d * 16.0) * 0.5 : 0.0;
    // 角向骨裂: 噪声沿极角调制, 环不是完美圆
    float ang = atan2(p.y, p.x);
    float n = tex2D(uNoise, float2(ang * 0.6366 + 3.0, ring * 2.0 - uTime * 0.15)).g;
    band *= 0.55 + 0.45 * n;

    float fade = 1.0 - uProgress;           // 扩张中渐灭
    float a = (band + wake) * uIntensity * fade * fade;
    float3 col = uColorMain.rgb * band + uColorEdge.rgb * wake;
    return float4(col * a, a);
}

// —— 轴线束: 过中心沿 uDir 的双向束, 两端软截止 + 流动噪声 ——
float4 Axis(float2 p, float2 uv)
{
    float along = dot(p, uDir);
    float perp = dot(p, float2(-uDir.y, uDir.x));
    float w = uRadius * (1.10 - 0.55 * uProgress); // 越临近合拢束越窄越亮
    float band = smoothstep(w, w * 0.18, abs(perp));
    float core = smoothstep(w * 0.22, 0.0, abs(perp));
    float cap = smoothstep(uHalfLen, uHalfLen * 0.80, abs(along));
    // 沿轴流动噪声 (向中心汇聚感)
    float n = tex2D(uNoise, float2(along * 1.8 - uTime * 0.5 * sign(along), perp * 4.0)).b;
    band *= 0.50 + 0.50 * n;

    float pulse = 0.80 + 0.20 * sin(uTime * 10.0 + uProgress * 18.0);
    float a = (band * 0.5 + core * 0.9) * cap * uIntensity * (0.30 + 0.70 * uProgress) * pulse;
    float3 col = lerp(uColorEdge.rgb, uColorMain.rgb, core) ;
    return float4(col * a, a);
}

float4 BoneRingPS(float2 uv : TEXCOORD0) : COLOR0
{
    if (uIntensity < 0.004)
        return float4(0, 0, 0, 0);

    float2 p = (uv - uCenter) * float2(uAspect, 1.0);

    if (uMode < 0.5)
        return Pillar(p, uv);
    else if (uMode < 1.5)
        return Ring(p, uv);
    return Axis(p, uv);
}

technique BoneRing
{
    pass P0
    {
        PixelShader = compile ps_3_0 BoneRingPS();
    }
}
