Shader "Metin2/Character Preview Unlit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [Toggle] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.34
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "CharacterPreview"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _AlphaClip;
                half _Cutoff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.5h)
                    clip(color.a - _Cutoff);

                // Several original Metin2 body textures store a mask in alpha rather
                // than transparency (notably the female Shaman novice texture). The
                // preview is composited through a transparent RenderTexture, so passing
                // that mask through makes the body disappear in the UI. Surviving hair
                // pixels and every opaque body pixel must write full coverage.
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }
    }
}
