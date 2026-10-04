float4x4 MatrixTransform;
float2 Viewport;
float2 SourceCenter;
float2 SourceRadius;
sampler ScreenSampler : register(s0);
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
float4 Pixel(PS_INPUT input) : COLOR0
{
    float2 offset = input.UV * 2.0f - 1.0f;
    clip(1.0f - dot(offset, offset));
    float3 color = tex2D(ScreenSampler, SourceCenter + offset * SourceRadius).rgb;
    return float4(color, 1.0f);
}
technique Magnify { pass p0 { VertexShader = compile vs_3_0 Vertex(); PixelShader = compile ps_3_0 Pixel(); } }
