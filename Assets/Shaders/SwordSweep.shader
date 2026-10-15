Shader "Survivor/SwordSweep"
{
    Properties
    {
        [HDR] _Tint ("White ivory core", Color) = (2.2, 2.05, 1.8, 1)
        [HDR] _EdgeTint ("Cool cloudy edge", Color) = (0.8, 0.85, 1.3, 1)
        _Reveal ("Sweep progress", Range(0,1)) = 1
        _Fade ("Opacity", Range(0,1)) = 1
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
                float4 _Tint, _EdgeTint;
                float _Reveal, _Fade;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                float cloud=Noise(uv*float2(17,4)+float2(-_Time.y*2,0));
                cloud=cloud*.7+Noise(uv*float2(37,9)-_Time.y)*.3;
                float radial=sin(saturate(uv.y)*PI);
                float taper=pow(saturate(sin(uv.x*PI)),.4);
                float head=1-smoothstep(_Reveal-.035,_Reveal+.035,uv.x);
                float tail=saturate(1-(_Reveal-uv.x)/.85);
                float mist=smoothstep(.12,.66,radial-(1-cloud)*.55);
                float ribbon=exp(-pow((uv.y-.86)/.035,2));
                float alpha=saturate((mist*.5+ribbon*.95)*taper*head*tail*_Fade);
                float3 color=lerp(_EdgeTint.rgb,_Tint.rgb,saturate(ribbon+radial*.08));
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
