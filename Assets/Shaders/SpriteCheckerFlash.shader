Shader "Custom/Sprite Checker Flash"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2
        _Amount ("Amount", Range(0, 1)) = 0
        _CheckerLow ("Checker Low Opacity", Range(0, 1)) = 0.4
        _PixelSize ("Pixel Size (art pixels)", Range(0.25, 4)) = 1
        [Toggle] _EdgeOnly ("Edge Only", Float) = 0
        _EdgeWidth ("Edge Width (art pixels)", Range(1, 4)) = 1
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
            half4 _FlashColor;
            half _Intensity;
            half _Amount;
            half _CheckerLow;
            float _PixelSize;
            half _EdgeOnly;
            float _EdgeWidth;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color;
            return output;
        }

        half SampleNeighbourAlpha(float2 uv)
        {
            if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
            return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
        }

        bool IsEdge(float2 uv)
        {
            float2 texel = _MainTex_TexelSize.xy;
            int width = clamp((int)round(_EdgeWidth), 1, 4);

            [loop]
            for (int i = 1; i <= width; i++)
            {
                float2 dx = float2(texel.x * i, 0.0);
                float2 dy = float2(0.0, texel.y * i);

                if (SampleNeighbourAlpha(uv + dx) <= 0.01) return true;
                if (SampleNeighbourAlpha(uv - dx) <= 0.01) return true;
                if (SampleNeighbourAlpha(uv + dy) <= 0.01) return true;
                if (SampleNeighbourAlpha(uv - dy) <= 0.01) return true;
            }

            return false;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float2 uvDx = ddx(input.uv);
            float2 uvDy = ddy(input.uv);

            half spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
            if (spriteAlpha <= 0.01 || _Amount <= 0.0001) discard;

            if (_EdgeOnly > 0.5)
            {
                float2 texelUV = (floor(input.uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
                if (!IsEdge(texelUV)) discard;
            }

            float uvPerScreenPixel = max(1e-6, length(float2(uvDx.x, uvDy.x)));
            float screenPixelsPerArtPixel = 1.0 / (uvPerScreenPixel * _MainTex_TexelSize.z);
            float cellSize = max(1.0, round(screenPixelsPerArtPixel * max(0.25, _PixelSize)));
            float2 cell = floor(input.positionCS.xy / cellSize);

            bool checker = fmod(cell.x + cell.y, 2.0) < 0.5;
            half opacity = (checker ? 1.0 : _CheckerLow) * _Amount * spriteAlpha * input.color.a;

            return half4(_FlashColor.rgb * _Intensity, opacity);
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
