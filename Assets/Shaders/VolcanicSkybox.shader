Shader "Survivor/VolcanicSkybox"
{
    Properties
    {
        _TopColor ("Smoke Top", Color) = (0.035, 0.012, 0.01, 1)
        _HorizonColor ("Burning Horizon", Color) = (0.48, 0.12, 0.045, 1)
        _BottomColor ("Lava Below", Color) = (0.9, 0.19, 0.025, 1)
        _HazeColor ("Haze", Color) = (0.72, 0.22, 0.09, 1)
        _HazeStrength ("Haze Strength", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
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
                float3 direction : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _HorizonColor;
                float4 _BottomColor;
                float4 _HazeColor;
                float _HazeStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = normalize(input.direction).y;
                float upperBlend = smoothstep(-0.08, 0.62, height);
                float lowerBlend = smoothstep(-0.72, 0.02, height);
                float3 lower = lerp(_BottomColor.rgb, _HorizonColor.rgb, lowerBlend);
                float3 color = lerp(lower, _TopColor.rgb, upperBlend);

                float horizonBand = exp(-pow((height + 0.02) * 3.2, 2.0));
                color = lerp(color, _HazeColor.rgb, horizonBand * _HazeStrength);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
