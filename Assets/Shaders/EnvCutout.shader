// Environment material used while a building hides the soldier: lit like Simple Lit (main light + SH ambient),
// but a screen-space circle around the soldier is cut out (with a thin bright rim), so the player stays visible
// through the wall while the rest of the building stays solid. CameraRig feeds the globals.
Shader "Game/EnvCutout"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _RimColor ("Rim Color", Color) = (1,0.9,0.6,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RimColor;
            CBUFFER_END
            // x, y: viewport position of the soldier; z: radius (fraction of screen height); w: rim width
            float4 _CutCircle;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 sp = i.screenPos.xy / i.screenPos.w;
                float2 d = sp - _CutCircle.xy;
                d.x *= _ScreenParams.x / _ScreenParams.y;
                float r = length(d);
                clip(r - _CutCircle.z);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                Light l = GetMainLight();
                half3 lit = albedo.rgb * (SampleSH(n) + l.color * saturate(dot(n, l.direction)));
                half rim = 1.0 - saturate((r - _CutCircle.z) / max(_CutCircle.w, 1e-4));
                lit = lerp(lit, _RimColor.rgb, rim * 0.85);
                return half4(lit, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RimColor;
            CBUFFER_END
            float4 _CutCircle;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; };
            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                float2 d = i.screenPos.xy / i.screenPos.w - _CutCircle.xy;
                d.x *= _ScreenParams.x / _ScreenParams.y;
                clip(length(d) - _CutCircle.z);
                return 0;
            }
            ENDHLSL
        }
    }
}
