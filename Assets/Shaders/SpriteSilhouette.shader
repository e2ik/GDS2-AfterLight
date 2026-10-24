Shader "Custom/Sprite Silhouette"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        [Enum(Solid, 0, Cutout, 1)] _Mode ("Mode", Float) = 0

        [Header(Solid)]
        _SilhouetteColor ("Silhouette Color", Color) = (0, 0, 0, 1)

        [Header(Cutout)]
        [NoScaleOffset] _CutoutTex ("Cutout Image", 2D) = "white" {}
        [Toggle] _WorldSpace ("World Space (off = Screen Space)", Float) = 0
        [Toggle] _FitToScreen ("Fit To Screen", Float) = 1
        [Toggle] _UseRect ("Match World Rect", Float) = 0
        _CutoutRect ("World Rect (min XY, size ZW)", Vector) = (0, 0, 1, 1)
        _CutoutUVRect ("Image UV Rect", Vector) = (0, 0, 1, 1)
        _CutoutScale ("Image Scale", Float) = 1
        _CutoutOffset ("Image Offset", Vector) = (0, 0, 0, 0)
        _CutoutScroll ("Image Scroll Speed", Vector) = (0, 0, 0, 0)
        _CutoutTint ("Image Tint", Color) = (1, 1, 1, 1)
        _PixelsPerUnit ("World Pixels Per Unit (0 = no snap)", Float) = 16

        [Header(Outline)]
        [Toggle] _Outline ("Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        [IntRange] _OutlineWidth ("Outline Width (sprite pixels)", Range(1, 4)) = 1
        [Toggle] _OutlineDiagonals ("Include Diagonals", Float) = 1
        [Toggle] _OutlineIgnoreWhite ("Skip Over White", Float) = 0
        _OutlineWhiteThreshold ("White Threshold", Range(0, 1)) = 0.95

        [Header(Alpha)]
        [Toggle] _HardEdge ("Hard Edge", Float) = 1
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
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
            float4 screenPos : TEXCOORD1;
            float2 worldPos : TEXCOORD2;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_CutoutTex);
        SAMPLER(sampler_CutoutTex);

        CBUFFER_START(UnityPerMaterial)
            float _Mode;
            half4 _SilhouetteColor;
            float _WorldSpace;
            float _FitToScreen;
            float _UseRect;
            float4 _CutoutRect;
            float4 _CutoutUVRect;
            float _CutoutScale;
            float4 _CutoutOffset;
            float4 _CutoutScroll;
            half4 _CutoutTint;
            float _PixelsPerUnit;
            float _Outline;
            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineDiagonals;
            float _OutlineIgnoreWhite;
            float _OutlineWhiteThreshold;
            float _HardEdge;
            float _AlphaCutoff;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS);
            output.positionCS = TransformWorldToHClip(positionWS);
            output.uv = input.uv;
            output.color = input.color;
            output.screenPos = ComputeScreenPos(output.positionCS);
            output.worldPos = positionWS.xy;
            return output;
        }

        float2 CutoutUV(Varyings input)
        {
            if (_UseRect > 0.5)
            {
                float2 local = saturate((input.worldPos - _CutoutRect.xy) / max(_CutoutRect.zw, 0.0001));
                return _CutoutUVRect.xy + local * _CutoutUVRect.zw;
            }

            float scale = max(0.0001, _CutoutScale);
            float2 scroll = _CutoutOffset.xy + _CutoutScroll.xy * _Time.y;

            if (_WorldSpace > 0.5)
            {
                float2 world = input.worldPos;
                if (_PixelsPerUnit > 0.0) world = (floor(world * _PixelsPerUnit) + 0.5) / _PixelsPerUnit;
                return world / scale + scroll;
            }

            float2 uv = input.screenPos.xy / input.screenPos.w - 0.5;
            if (_FitToScreen < 0.5) uv.x *= _ScreenParams.x / _ScreenParams.y;
            return uv / scale + 0.5 + scroll;
        }

        half SpriteAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
        }

        bool IsOutline(float2 uv, half cutoff)
        {
            uint width;
            uint height;
            _MainTex.GetDimensions(width, height);
            float2 texel = 1.0 / float2(max(width, 1u), max(height, 1u));
            int steps = (int)round(_OutlineWidth);
            bool diagonals = _OutlineDiagonals > 0.5;

            [unroll]
            for (int s = 1; s <= 4; s++)
            {
                if (s > steps) break;
                float2 d = texel * s;

                if (SpriteAlpha(uv + float2(d.x, 0)) >= cutoff) return true;
                if (SpriteAlpha(uv - float2(d.x, 0)) >= cutoff) return true;
                if (SpriteAlpha(uv + float2(0, d.y)) >= cutoff) return true;
                if (SpriteAlpha(uv - float2(0, d.y)) >= cutoff) return true;

                if (diagonals)
                {
                    if (SpriteAlpha(uv + d) >= cutoff) return true;
                    if (SpriteAlpha(uv - d) >= cutoff) return true;
                    if (SpriteAlpha(uv + float2(d.x, -d.y)) >= cutoff) return true;
                    if (SpriteAlpha(uv + float2(-d.x, d.y)) >= cutoff) return true;
                }
            }

            return false;
        }

        half4 frag(Varyings input) : SV_Target
        {
            half spriteAlpha = SpriteAlpha(input.uv);
            half cutoff = _HardEdge > 0.5 ? _AlphaCutoff : 0.001;

            if (spriteAlpha < cutoff)
            {
                if (_Outline > 0.5 && IsOutline(input.uv, cutoff))
                {
                    if (_OutlineIgnoreWhite > 0.5)
                    {
                        half3 under = SAMPLE_TEXTURE2D(_CutoutTex, sampler_CutoutTex, CutoutUV(input)).rgb;
                        if (min(under.r, min(under.g, under.b)) >= _OutlineWhiteThreshold) discard;
                    }

                    return half4(_OutlineColor.rgb, _OutlineColor.a * input.color.a);
                }

                discard;
            }

            half alpha = _HardEdge > 0.5 ? input.color.a : spriteAlpha * input.color.a;

            half4 result;
            if (_Mode > 0.5)
            {
                half4 image = SAMPLE_TEXTURE2D(_CutoutTex, sampler_CutoutTex, CutoutUV(input)) * _CutoutTint;
                result = half4(image.rgb, image.a * alpha);
            }
            else
            {
                result = half4(_SilhouetteColor.rgb, _SilhouetteColor.a * alpha);
            }

            return result;
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
