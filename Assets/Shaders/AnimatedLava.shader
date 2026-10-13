Shader "Survivor/AnimatedLava"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0.03, 0, 0, 1)
        _HotColor ("Hot Color", Color) = (1, 0.08, 0.005, 1)
        _EmissionStrength ("Emission Strength", Range(0, 12)) = 3
        _Scale ("Pattern Scale", Range(0.02, 2)) = 0.2
        _Speed ("Flow Speed", Range(0, 3)) = 0.35
        _Contrast ("Contrast", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DarkColor;
                float4 _HotColor;
                float _EmissionStrength;
                float _Scale;
                float _Speed;
                float _Contrast;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _Speed;
                float2 coordinates = float2(
                    input.positionWS.x + input.positionWS.y * 0.32,
                    input.positionWS.z - input.positionWS.y * 0.18);
                coordinates *= _Scale;

                float broad = ValueNoise(coordinates + float2(time * 0.35, -time));
                float detail = ValueNoise(coordinates * 3.1 + float2(-time, time * 0.55));
                float veins = abs(sin((broad * 5.5 + detail * 2.2 + coordinates.x * 0.2) * 3.14159));
                float heat = pow(saturate(1.0 - veins), _Contrast);
                heat = saturate(heat + detail * 0.24);

                float3 color = lerp(_DarkColor.rgb, _HotColor.rgb, heat);
                return half4(color * (1.0 + heat * _EmissionStrength), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
        }
    }
}
