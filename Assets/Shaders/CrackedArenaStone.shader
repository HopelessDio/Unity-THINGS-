Shader "Survivor/CrackedArenaStone"
{
    Properties
    {
        _BaseColor ("Stone Color", Color) = (0.2, 0.22, 0.26, 1)
        _VariationColor ("Stone Variation", Color) = (0.34, 0.36, 0.4, 1)
        _CrackColor ("Crack Color", Color) = (0.012, 0.014, 0.02, 1)
        _CrackGlowColor ("Crack Heat Color", Color) = (1, 0.12, 0.01, 1)
        _CrackGlow ("Crack Heat", Range(0, 2)) = 0.08
        _Scale ("Stone Cell Scale", Range(0.05, 2)) = 0.3
        _CrackWidth ("Crack Width", Range(0.005, 0.2)) = 0.045
        _Roughness ("Roughness", Range(0, 1)) = 0.82
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _VariationColor;
                float4 _CrackColor;
                float4 _CrackGlowColor;
                float _CrackGlow;
                float _Scale;
                float _CrackWidth;
                float _Roughness;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return frac(float2(n, n * 17.37 + 0.23));
            }

            float CellularEdge(float2 p, out float cellShade)
            {
                float2 baseCell = floor(p);
                float2 local = frac(p);
                float nearest = 10.0;
                float second = 10.0;
                float shade = 0.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = float2(x, y);
                        float2 randomPoint = Hash22(baseCell + cell);
                        float distanceToPoint = length(cell + randomPoint - local);
                        if (distanceToPoint < nearest)
                        {
                            second = nearest;
                            nearest = distanceToPoint;
                            shade = Hash21(baseCell + cell + 19.7);
                        }
                        else if (distanceToPoint < second)
                        {
                            second = distanceToPoint;
                        }
                    }
                }
                cellShade = shade;
                return second - nearest;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                float3 absoluteNormal = abs(normal);
                float2 projection = absoluteNormal.y > max(absoluteNormal.x, absoluteNormal.z)
                    ? input.positionWS.xz
                    : (absoluteNormal.x > absoluteNormal.z ? input.positionWS.zy : input.positionWS.xy);

                float cellShade;
                float edgeDistance = CellularEdge(projection * _Scale, cellShade);
                float crack = 1.0 - smoothstep(_CrackWidth, _CrackWidth * 2.25, edgeDistance);
                float3 stone = lerp(_BaseColor.rgb, _VariationColor.rgb, cellShade * 0.72);
                float3 albedo = lerp(stone, _CrackColor.rgb, crack * 0.92);

                Light mainLight = GetMainLight(input.shadowCoord);
                float diffuse = saturate(dot(normal, mainLight.direction));
                float3 illumination = 0.38 + mainLight.color * diffuse * mainLight.shadowAttenuation;

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint index = 0u; index < lightCount; index++)
                {
                    Light light = GetAdditionalLight(index, input.positionWS);
                    float amount = saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                    illumination += light.color * amount;
                }
                #endif

                // A very restrained warm rim inside the deepest cracks reflects the
                // lava below without turning the whole combat floor into a hazard.
                float deepCrack = smoothstep(0.58, 0.96, crack);
                float3 finalColor = albedo * illumination;
                finalColor += _CrackGlowColor.rgb * deepCrack * _CrackGlow;
                finalColor = MixFog(finalColor, ComputeFogFactor(input.positionCS.z));
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
