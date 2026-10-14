Shader "Survivor/DashAfterimage"
{
    Properties
    {
        [HDR] _Tint ("Crimson glow", Color) = (1.7, 0.28, 0.35, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.34
        _Age ("Fade age", Range(0, 1)) = 0
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
                float _Opacity;
                float _Age;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            float Hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
            float Cloud(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell+float3(1,0,0)), f.x),
                    lerp(Hash(cell+float3(0,1,0)), Hash(cell+float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(cell+float3(0,0,1)), Hash(cell+float3(1,0,1)), f.x),
                    lerp(Hash(cell+float3(0,1,1)), Hash(cell+float3(1,1,1)), f.x), f.y), f.z);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                float rim = pow(1.0 - saturate(abs(dot(normalize(input.normalWS), view))), 1.4);
                float cloud = Cloud(input.positionWS * 5.0 + float3(0, _Age * 1.2, 0));
                float fade = (1.0 - _Age) * (1.0 - _Age);
                float haze = smoothstep(_Age * 0.7, 1.0, cloud);
                float3 color = lerp(_Tint.rgb, float3(2.2, 1.7, 1.7), rim * .75);
                return half4(color, _Opacity * fade * (.35 + .65 * rim) * (.4 + .6 * haze));
            }
            ENDHLSL
        }
    }
}
