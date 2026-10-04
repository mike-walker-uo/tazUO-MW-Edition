float4x4 MatrixTransform;
float2 Viewport;
float2 TextureSize;
float4 SampleBounds;
float2 Footprint;
float Strength;
sampler WorldSampler : register(s0);
struct VS_INPUT { float4 Position : POSITION0; float3 Normal : NORMAL0; float3 TexCoord : TEXCOORD0; float3 Hue : TEXCOORD1; };
struct PS_INPUT { float4 Position : POSITION0; float2 UV : TEXCOORD0; };
PS_INPUT Vertex(VS_INPUT input)
{
    PS_INPUT output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Position.x -= 0.5f / Viewport.x;
    output.Position.y += 0.5f / Viewport.y;
    output.UV = input.TexCoord.xy;
    return output;
}
float3 ReadColor(float2 uv)
{
    // Never sample outside the world viewport or a cropped zoom region.
    return tex2D(WorldSampler, clamp(uv, SampleBounds.xy, SampleBounds.zw)).rgb;
}
float4 PixelArtPixel(PS_INPUT input) : COLOR0
{
    // Keep texel interiors crisp; interpolate only across their boundaries.
    float2 texel = input.UV * TextureSize - 0.5f;
    float2 fraction = frac(texel);
    float2 transition = saturate((fraction - 0.5f) / clamp(Footprint, 0.001f, 1.0f) + 0.5f);
    float2 uv = (floor(texel) + lerp(fraction, transition, Strength) + 0.5f) / TextureSize;
    return float4(ReadColor(uv), 1.0f);
}
float Luma(float3 color) { return dot(color, float3(0.299f, 0.587f, 0.114f)); }
float4 AntiAliasPixel(PS_INPUT input) : COLOR0
{
    // Directional screen-space FXAA, following Timothy Lottes' FXAA method:
    // https://developer.download.nvidia.com/assets/gamedev/files/sdk/11/FXAA_WhitePaper.pdf
    float2 texelStep = 1.0f / TextureSize;
    float3 center = ReadColor(input.UV);
    float nw = Luma(ReadColor(input.UV + float2(-texelStep.x, -texelStep.y)));
    float ne = Luma(ReadColor(input.UV + float2(texelStep.x, -texelStep.y)));
    float sw = Luma(ReadColor(input.UV + float2(-texelStep.x, texelStep.y)));
    float se = Luma(ReadColor(input.UV + float2(texelStep.x, texelStep.y)));
    float middle = Luma(center);
    float low = min(middle, min(min(nw, ne), min(sw, se)));
    float high = max(middle, max(max(nw, ne), max(sw, se)));
    if (high - low < max(0.03125f, high * 0.125f)) return float4(center, 1.0f);
    float2 direction = float2(-((nw + ne) - (sw + se)), (nw + sw) - (ne + se));
    float reduction = max((nw + ne + sw + se) * 0.03125f, 0.0078125f);
    direction = clamp(direction / (min(abs(direction.x), abs(direction.y)) + reduction), -8.0f, 8.0f) * texelStep;
    float3 inner = 0.5f * (ReadColor(input.UV - direction / 6.0f) + ReadColor(input.UV + direction / 6.0f));
    float3 outer = inner * 0.5f + 0.25f * (ReadColor(input.UV - direction * 0.5f) + ReadColor(input.UV + direction * 0.5f));
    float outerLuma = Luma(outer);
    float3 filtered = outerLuma < low || outerLuma > high ? inner : outer;
    return float4(lerp(center, filtered, Strength), 1.0f);
}
technique PixelArt { pass p0 { VertexShader = compile vs_3_0 Vertex(); PixelShader = compile ps_3_0 PixelArtPixel(); } }
technique AntiAlias { pass p0 { VertexShader = compile vs_3_0 Vertex(); PixelShader = compile ps_3_0 AntiAliasPixel(); } }
