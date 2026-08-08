float4x4 MatrixTransform;
float4x4 WorldMatrix;
float2 Viewport;
float Time;
float MotionAmount;

sampler WaterSampler : register(s0);

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float3 TexCoord : TEXCOORD0;
    float3 Hue : TEXCOORD1;
};

struct PS_INPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float Alpha : TEXCOORD1;
};

PS_INPUT WaterVertex(VS_INPUT input)
{
    PS_INPUT output;
    output.Position = mul(mul(input.Position, WorldMatrix), MatrixTransform);
    output.Position.x -= 0.5f / Viewport.x;
    output.Position.y += 0.5f / Viewport.y;
    output.TexCoord = input.TexCoord.xy;
    output.Alpha = input.Hue.z;
    return output;
}

float4 WaterPixel(PS_INPUT input) : COLOR0
{
    float2 uv = input.TexCoord;
    float phaseA = uv.y * 15.0f + uv.x * 4.0f + Time * 0.12f;
    float phaseB = uv.x * 12.0f - uv.y * 5.0f - Time * 0.08f;
    float2 displacementA;
    displacementA.x = (sin(phaseA) + sin(phaseB * 0.73f) * 0.45f) * MotionAmount;
    displacementA.y = (cos(phaseB) + cos(phaseA * 0.61f) * 0.35f) * MotionAmount * 0.55f;

    float2 uvB = uv * 1.618034f + float2(0.173f, 0.419f);
    float phaseC = uvB.x * 9.0f + uvB.y * 3.0f - Time * 0.071f;
    float phaseD = uvB.y * 11.0f - uvB.x * 2.0f + Time * 0.053f;
    float2 displacementB;
    displacementB.x = (cos(phaseC) + sin(phaseD) * 0.32f) * MotionAmount * 0.72f;
    displacementB.y = (sin(phaseD) + cos(phaseC) * 0.28f) * MotionAmount * 0.43f;

    float4 broad = tex2D(WaterSampler, uv + displacementA);
    float4 detail = tex2D(WaterSampler, uvB + displacementB);
    float4 color = lerp(broad, detail, 0.28f);
    return color * input.Alpha;
}

technique WaterTechnique
{
    pass p0
    {
        VertexShader = compile vs_3_0 WaterVertex();
        PixelShader = compile ps_3_0 WaterPixel();
    }
}
