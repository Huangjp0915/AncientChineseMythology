// ============================================================
// CelestialDragonCloudSea.fx — 天御金龙·云海层 (屏幕空间 decal)
// 由 CelestialScreenSystem.PostDrawTiles 满屏绘制 (s0=共享噪声)
// 三层漂流 FBM 云卷 + 上金下暗天光照明 + 最多 3 个"破云 punch"
// (俯冲穿透点云被冲开的展开涟漪, age 0→1 扩散消散)
// 实体之下绘制, 不遮挡弹幕; 不占全屏后处理名额 (同 ArenaRunic 用法)
// ============================================================

sampler uNoiseTex : register(s0); // 共享三通道 FBM 噪声 (LinearWrap)

float  uTime;        // 秒
float  uIntensity;   // 云海总强度 0~1
float  uAspect;      // 宽高比 w/h
float  uCloudLevel;  // 云海上缘 (屏幕 UV y, 0=顶 1=底)
float2 uScroll;      // 世界锚定偏移 (screenPosition * 微系数)
float4 uPunch0;      // xy=屏幕UV 中心, z=age 0~1, w=强度
float4 uPunch1;
float4 uPunch2;
float4 uColorLit;    // 受光暖金
float4 uColorShadow; // 底部暗云

struct VSOutput {
    float4 position : SV_POSITION;
    float4 color    : COLOR0;
    float2 texCoord : TEXCOORD0;
};

// 单个 punch 对 (density, rim) 的贡献: 环形擦除 + 环沿亮边
void ApplyPunch(float4 punch, float2 p, inout float erase, inout float rim)
{
    if (punch.w < 0.01)
        return;
    float2 c = float2(punch.x * uAspect, punch.y);
    float d = distance(p, c);
    float radius = 0.04 + punch.z * 0.38;            // 随 age 扩散
    float strength = punch.w * (1.0 - punch.z);      // 随 age 消散
    // 环内擦除 (云被冲开)
    erase = max(erase, smoothstep(radius + 0.06, radius - 0.10, d) * strength);
    // 环沿亮边 (冲开的云被天光照亮)
    float ring = saturate(1.0 - abs(d - radius) / 0.05);
    rim = max(rim, ring * ring * strength);
}

float4 PS_CloudSea(VSOutput input) : COLOR0
{
    if (uIntensity < 0.01)
        return float4(0, 0, 0, 0);

    float2 uv = input.texCoord.xy;
    float2 p = float2(uv.x * uAspect, uv.y);

    // 三层不同尺度/速度的漂流云
    float2 q = p * 0.9 + uScroll;
    float n = tex2D(uNoiseTex, q * 0.35 + float2(uTime * 0.010, 0.0)).r * 0.55
            + tex2D(uNoiseTex, q * 0.80 + float2(-uTime * 0.017, 0.13)).g * 0.30
            + tex2D(uNoiseTex, q * 1.90 + float2(uTime * 0.030, 0.41)).b * 0.15;

    // 高度权重: 云海集中于 uCloudLevel 之下, 顶部只余薄云
    float band = smoothstep(uCloudLevel - 0.06, uCloudLevel + 0.30, uv.y);
    float thin = (1.0 - smoothstep(0.0, 0.5, uv.y)) * 0.16;
    float density = saturate(n * 1.45 - 0.50) * saturate(band + thin);

    // punch: 破云涟漪
    float erase = 0.0;
    float rim = 0.0;
    ApplyPunch(uPunch0, p, erase, rim);
    ApplyPunch(uPunch1, p, erase, rim);
    ApplyPunch(uPunch2, p, erase, rim);
    density *= saturate(1.0 - erase);

    // 天光照明: 越靠上/越薄越受金光, 底部沉暗
    float lit = saturate(1.15 - uv.y * 1.25 + n * 0.5);
    float3 col = lerp(uColorShadow.rgb, uColorLit.rgb, lit);
    col += uColorLit.rgb * rim * 0.9; // 冲开环沿被照亮

    float alpha = density * uIntensity * lerp(uColorShadow.a, uColorLit.a, lit);
    alpha = saturate(alpha + rim * density * 0.4 * uIntensity);

    return float4(col * alpha, alpha); // 预乘输出, NonPremultiplied/AlphaBlend 皆可读
}

technique Technique1
{
    pass CloudSeaPass
    {
        PixelShader = compile ps_3_0 PS_CloudSea();
    }
}
