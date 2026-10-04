float4x4 MatrixTransform;
float2 Viewport;
float UseLights;
float AltLights;
sampler WorldSampler : register(s0);
texture LightTexture;
sampler LightSampler : register(s1) = sampler_state { Texture = <LightTexture>; MinFilter = Linear; MagFilter = Linear; MipFilter = None; AddressU = Clamp; AddressV = Clamp; };
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
    float4 color = tex2D(WorldSampler, input.UV);
    if (UseLights > 0.5f)
    {
        float3 light = tex2D(LightSampler, input.UV).rgb;
        light = lerp(light / 12.92f, pow(max(0.0f, (light + 0.055f) / 1.055f), 2.4f), step(0.04045f, light));
        color.rgb = AltLights > 0.5f ? color.rgb + light * 0.5f : color.rgb * light;
    }
    // World colors were decoded before blending. Encode once, before UI/text drawing.
    color.rgb = lerp(color.rgb * 12.92f, 1.055f * pow(max(0.0f, color.rgb), 1.0f / 2.4f) - 0.055f, step(0.0031308f, color.rgb));
    color.a = 1.0f;
    return color;
}
technique Composite { pass p0 { VertexShader = compile vs_3_0 Vertex(); PixelShader = compile ps_3_0 Pixel(); } }
