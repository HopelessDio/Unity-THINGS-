Shader "Survivor/DashWisp"
{
    Properties
    {
        [HDR] _Tint ("Ivory tint", Color) = (1.3, 1.2, 1.05, 1)
        _ParticleMode ("Cloud mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _ParticleMode;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float streak = pow(saturate(1.0 - abs(p.y)), 2.0);
                float cloud = pow(saturate(1.0 - dot(p,p)), 1.4);
                float softness = lerp(streak, cloud, saturate(_ParticleMode));
                return half4(_Tint.rgb * input.color.rgb, _Tint.a * input.color.a * softness);
            }
            ENDHLSL
        }
    }
}
