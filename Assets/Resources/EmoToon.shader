Shader "EmoRelief/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Smoothness ("Gloss", Range(0,1)) = 0.5
        _OutlineWidth ("Outline Width (px)", Float) = 2.5
        _OutlineColor ("Outline Color", Color) = (0.07,0.02,0.08,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            float _OutlineWidth;
            half4 _OutlineColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Toon"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light = GetMainLight();
                float ndl = dot(normal, light.direction);
                // Two hard tone bands plus glossy vinyl highlight and rim.
                half shade = lerp(0.58, 1.0, smoothstep(-0.04, 0.04, ndl)) + smoothstep(0.62, 0.66, ndl) * 0.08;
                half3 ambient = SampleSH(normal);
                half3 color = albedo.rgb * shade * saturate(light.color * 0.75 + ambient * 0.55 + 0.15);
                float3 halfway = normalize(light.direction + view);
                half spec = smoothstep(0.86, 0.9, pow(saturate(dot(normal, halfway)), lerp(8.0, 96.0, _Smoothness))) * _Smoothness * 0.7;
                half rim = smoothstep(0.62, 0.7, 1.0 - saturate(dot(normal, view))) * 0.18;
                return half4(color + spec + rim * albedo.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4 clip = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 direction = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float len = length(direction);
                direction = len > 1e-5 ? direction / len : float2(0, 0);
                clip.xy += direction * _OutlineWidth * 2.0 / _ScreenParams.xy * clip.w;
                output.positionCS = clip;
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    FallBack Off
}
