// ============================================================
// 幽冥妖狐 · 雾中狐眼 (NetherKitsuneEye) — 局部 quad SDF telegraph
// 一个 quad 绘出一对杏仁狐眼: 两圆交集成眼形 + 圆角矩形竖瞳;
// uOpen 眨眼 (真身读数线索), uPupil 瞳缩 (1 圆瞳 -> 0 竖线 = 扑袭倒计时),
// uFlash 扑袭白闪。加性绘制, 载体纹理仅作 SpriteBatch 占位。
// ============================================================

sampler uImage0 : register(s0); // 占位载体 (不采样)

float uTime;
float uOpen;   // 0=闭眼 1=全开
float uPupil;  // 1=圆瞳 0=竖线瞳
float uGlow;   // 辉光强度 0~1.2
float uFlash;  // 扑袭白闪 0~1
float4 uColor; // 虹膜色
float uSeed;   // 每对眼相位差

// 单眼 SDF: 杏仁形 = 上下两圆的交集 (d<0 在眼内)
float EyeShape(float2 p, float open)
{
    p.y /= max(open, 0.03); // 眨眼纵向压扁
    float r = 0.62;
    float off = r - 0.16;
    float d1 = length(p - float2(0, off)) - r;
    float d2 = length(p + float2(0, off)) - r;
    return max(d1, d2);
}

float4 EyePS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // quad 平分双眼: 眼心 x=0.26 / 0.74
    float side = coords.x < 0.5 ? 0.0 : 1.0;
    float2 c = float2(lerp(0.26, 0.74, side), 0.5);
    float2 p = (coords - c) * float2(4.6, 3.4);

    float open = saturate(uOpen);
    float d = EyeShape(p, open);

    // 眼内亮面 + 边缘指数泛光
    float inner = smoothstep(0.03, -0.12, d);
    float halo = exp(-max(d, 0.0) * 5.5) * 0.55;

    // 竖瞳: 圆角矩形 SDF (瞳=暗, 收缩时变细线)
    float pw = lerp(0.055, 0.30, saturate(uPupil));
    float2 q = abs(p) - float2(pw, 0.34);
    float dp = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
    float pupilMask = smoothstep(-0.02, 0.06, dp);

    // 妖异闪烁
    float flick = 0.88 + 0.12 * sin(uTime * 7.0 + uSeed * 13.7);

    float3 iris = uColor.rgb;
    float3 col = iris * (inner * pupilMask * 1.7 + halo);
    col += float3(1.0, 1.0, 1.0) * inner * uFlash * 1.5; // 扑袭白闪

    float vis = uGlow * flick * smoothstep(0.02, 0.15, open) * sampleColor.a;
    return float4(col * vis, 0.0); // 加性: rgb 即能量
}

technique Technique1
{
    pass EyePass
    {
        PixelShader = compile ps_3_0 EyePS();
    }
}
