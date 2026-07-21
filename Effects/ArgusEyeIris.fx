// ============================================================
// Argus 程序化星瞳 — 光环巨眼 / 星瞳哨兵 / 万目投影 共用
// 杏仁眼睑开合 + 星云虹膜 + 环纹 + 睫状星芒 + 竖瞳化 + 锁定警戒环
// 顶点色: rgb = 主题色调 (紫→红 = 锁定充能), a = 主透明度
// quad 旋转即凝视朝向 (瞳孔沿本地 +X 偏移)
// 自包含程序噪声; 建议 Additive 绘制 (瞳孔以"吞光"呈现暗核)
// ============================================================

sampler uTexture : register(s0); // SoftGlow 柔和径向底

float uTime;       // 动画时间(秒)
float uOpen;       // 睁眼度 0~1 (眼睑开合)
float uSlit;       // 竖瞳化 0~1 (三阶段)
float uPupilShift; // 瞳孔沿本地+X偏移量 (0~0.12, 凝视视差)
float uNova;       // 锁定/爆发增辉 0~1

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float fbm3(float2 p)
{
    return valueNoise(p) * 0.50
         + valueNoise(p * 2.13 + 1.7) * 0.30
         + valueNoise(p * 4.27 + 3.1) * 0.20;
}

float4 EyeIrisPS(float4 sampleColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    if (sampleColor.a < 0.01 || uOpen < 0.01)
        return float4(0, 0, 0, 0);

    float2 c = uv - 0.5;

    // 眼睑: 杏仁形开合 (uOpen=1 全开; 收合 = 上下眼睑压扁)
    float lidH = 0.34 * uOpen * pow(max(sin(3.14159 * uv.x), 0.0), 0.65);
    float lid = smoothstep(lidH, lidH - 0.06, abs(c.y));
    if (lid <= 0.002)
        return float4(0, 0, 0, 0);

    // 瞳孔中心偏移 (quad 旋转把 +X 对准凝视方向)
    float2 pc = c - float2(uPupilShift, 0.0);
    float d = length(pc);
    // 竖瞳: 本地X压缩形成纵向裂瞳
    float pd = length(float2(pc.x * (1.0 + uSlit * 5.0), pc.y * (1.0 - uSlit * 0.25)));

    float irisR = 0.215;

    // 虹膜内部: 星云 FBM + 旋转环纹
    float2 nUV = pc * 6.0 + float2(uTime * 0.05, -uTime * 0.04);
    float neb = fbm3(nUV);
    float irisMask = smoothstep(irisR, irisR - 0.025, d);
    float rings = 0.5 + 0.5 * sin(d * 70.0 - uTime * 2.4 + neb * 5.0);
    rings *= irisMask;

    // 瞳孔暗核 (加性绘制下 = 不发光的洞)
    float pupil = smoothstep(0.10, 0.055, pd);

    // 虹膜缘亮环
    float rim = 1.0 - smoothstep(0.0, 0.030 + uNova * 0.02, abs(d - irisR));

    // 睫状星芒 (虹膜外沿放射细线)
    float ang = atan2(c.y, c.x + 0.0001);
    float lash = pow(abs(cos(ang * 7.0 + uTime * 0.25)), 28.0);
    lash *= smoothstep(irisR + 0.16, irisR + 0.03, d) * smoothstep(irisR - 0.02, irisR + 0.05, d);

    // 巩膜辉光底 (SoftGlow 径向衰减)
    float glowBase = tex2D(uTexture, uv).r;

    float3 tint = sampleColor.rgb;
    float3 col = tint * glowBase * 0.30;
    col += tint * (0.55 + neb * 0.85) * irisMask;
    col += tint * rings * 0.30;
    col += lerp(tint, float3(1.0, 1.0, 1.0), 0.75) * rim * (0.9 + uNova * 1.3);
    col += lerp(tint, float3(1.0, 1.0, 1.0), 0.5) * lash * 0.55;
    col *= 1.0 - pupil * 0.92;

    // 锁定充能: 外圈警戒环收缩呼吸 + 全体增辉
    float novaR = irisR * (1.45 + 0.15 * sin(uTime * 6.0));
    float novaRing = 1.0 - smoothstep(0.0, 0.05, abs(d - novaR));
    col += tint * novaRing * uNova * 0.9;
    col *= 1.0 + uNova * 0.6;

    float alpha = lid * sampleColor.a;
    alpha *= saturate(glowBase * 0.5 + irisMask + rim + lash * 0.6);

    return float4(col * alpha, alpha);
}

technique Technique1
{
    pass EyeIrisPass
    {
        PixelShader = compile ps_3_0 EyeIrisPS();
    }
}
