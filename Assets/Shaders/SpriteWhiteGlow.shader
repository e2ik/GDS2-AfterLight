Shader "Custom/Sprite White Glow"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MatchColor ("Match Color", Color) = (1, 1, 1, 1)
        _Tolerance ("Tolerance", Range(0, 1)) = 0.1
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2
        _CheckerLow ("Checker Low", Range(0, 1)) = 0.5
        _CheckerSize ("Checker Size (art pixels)", Range(1, 4)) = 1
        _GlowFade ("Glow Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _MatchColor;
            half _Tolerance;
            half4 _GlowColor;
            half _Intensity;
            half _CheckerLow;
            float _CheckerSize;
            half _GlowFade;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color;
            return output;
        }

        half4 frag(Varyings input) : SV_Target
        {
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            half difference = distance(tex.rgb, _MatchColor.rgb);

            if (tex.a <= 0.5 || difference > _Tolerance) return half4(0, 0, 0, 0);

            float cellSize = max(1.0, round(_CheckerSize));
            float2 cell = floor(floor(input.uv * _MainTex_TexelSize.zw) / cellSize);
            bool checker = fmod(abs(cell.x + cell.y), 2.0) < 0.5;
            half coverage = checker ? 1.0 : _CheckerLow;

            return half4(_GlowColor.rgb * _Intensity, coverage * _GlowFade * input.color.a);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
