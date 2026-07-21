// ============================================================
// 干将莫邪·双剑弧光 — 扇环斩击 TriangleStrip 图元着色器 (刀剑系列旗舰专属)
// uv.x = 沿弧 0~1 (起手→收尾), uv.y = 径向 0(内)~1(外)
// 扫描头白热 + 尾迹衰减 + 噪声撕裂外缘 + 芯/缘双色
// 仅 PS, 变换走 SpriteBatch 外部矩阵 (同 BeamGrad); s0 = 共享噪声
// ============================================================

sampler uNoise : register(s0); // 流动噪声 (ACMShaders.NoiseTexture)

float  uTime;       // 动画时间(秒)
float  uIntensity;  // 整体强度 0~1
float  uProgress;   // 扫描头位置 0~1 (挥砍进度)
float4 uColorCore;  // 芯色 (rgb, a=芯部权重)
float4 uColorEdge;  // 缘色 (rgb, a=缘部权重)
float  uNoiseScale; // 噪声密度 (建议 2~4)
float  uTailLen;    // 尾迹长度 (uv.x 单位, 建议 0.35~0.6)

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float3 texCoord : TEXCOORD0;
};

float4 PS_TwinArc(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;

    // 扫描头之前不可见 (斩到哪亮到哪)
    float behind = uProgress - uv.x;
    if (behind < 0.0)
        return float4(0, 0, 0, 0);

    // 尾迹衰减: 离扫描头越远越淡
    float tail = saturate(behind / max(uTailLen, 0.001));
    float tailFade = pow(1.0 - tail, 1.6);

    // 扫描头白热线
    float head = smoothstep(0.06, 0.0, behind);

    // 噪声 (沿弧流动)
    float n = tex2D(uNoise, float2(uv.x * uNoiseScale - uTime * 0.7, uv.y * 0.8 + uTime * 0.15)).r;

    // 径向能量: 集中在外缘 (刃尖), 内侧渐隐; 外缘被噪声撕裂
    float radial = uv.y;
    float tear = (n - 0.5) * 0.18 * (0.4 + 0.6 * tail); // 越靠尾迹撕裂越明显
    float outerCut = 1.0 - smoothstep(0.85 + tear, 1.0 + tear, radial);
    float innerFade = smoothstep(0.0, 0.45, radial);
    float body = innerFade * outerCut;

    // 芯/缘配色: 外缘亮芯色, 内侧缘色; 扫描头附加白热
    float coreProfile = pow(saturate(radial), 2.0);
    float3 col = lerp(uColorEdge.rgb, uColorCore.rgb, coreProfile);
    col *= 0.75 + 0.5 * n;                        // 噪声明暗呼吸
    col += uColorCore.rgb * head * 1.6;           // 扫描头过曝
    col += float3(1.0, 1.0, 1.0) * head * 0.8;    // 白热提亮

    // 弧两端收口
    float ends = smoothstep(0.0, 0.05, uv.x) * smoothstep(1.0, 0.97, uv.x);

    float alpha = body * tailFade * ends * uIntensity;
    alpha *= lerp(uColorEdge.a, uColorCore.a, coreProfile);
    alpha += head * body * ends * uIntensity * 0.5;

    col *= input.color.rgb;
    alpha *= input.color.a;

    return float4(saturate(col), saturate(alpha));
}

technique Technique1
{
    pass TwinArcPass
    {
        PixelShader = compile ps_3_0 PS_TwinArc();
    }
}
