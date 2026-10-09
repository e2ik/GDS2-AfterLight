Shader "Custom/Sprite Dither Outline"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Tint", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 0.5
        _Falloff ("Falloff", Range(0, 1)) = 0.5
        _Rings ("Rings", Range(1, 8)) = 3
        _PixelSize ("Pixel Size (art pixels)", Range(0.25, 4)) = 1
        _Checker ("Checker Contrast", Range(0, 1)) = 1
        [Toggle] _LineEnabled ("Line Enabled", Float) = 0
        _LineColor ("Line Color", Color) = (0, 0, 0, 1)
        _LineStrength ("Line Strength", Range(0, 1)) = 1
        [Toggle] _LineDiagonals ("Line Diagonals", Float) = 1
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

        Blend DstColor Zero
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
            half4 _OutlineColor;
            half _Strength;
            half _Falloff;
            float _Rings;
            float _PixelSize;
            half _Checker;
            half _LineEnabled;
            half4 _LineColor;
            half _LineStrength;
            half _LineDiagonals;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color;
            return output;
        }

        half SampleAlphaLod(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
        }

        half SampleNeighbour(float2 center, float2 offset)
        {
            float2 uv = center + _MainTex_TexelSize.xy * offset;
            if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
            return SampleAlphaLod(uv);
        }

        bool IsLinePixel(float2 uv)
        {
            float2 center = (floor(uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;

            half sides = max(max(SampleNeighbour(center, float2(-1, 0)), SampleNeighbour(center, float2(1, 0))),
                             max(SampleNeighbour(center, float2(0, -1)), SampleNeighbour(center, float2(0, 1))));
            if (sides > 0.5) return true;
            if (_LineDiagonals < 0.5) return false;

            half corners = max(max(SampleNeighbour(center, float2(-1, -1)), SampleNeighbour(center, float2(1, -1))),
                               max(SampleNeighbour(center, float2(-1, 1)), SampleNeighbour(center, float2(1, 1))));
            return corners > 0.5;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float2 uvDx = ddx(input.uv);
            float2 uvDy = ddy(input.uv);

            if (SampleAlphaLod(input.uv) > 0.5) return half4(1, 1, 1, 1);

            if (_LineEnabled > 0.5 && IsLinePixel(input.uv))
            {
                half lineStrength = _LineStrength * _LineColor.a * input.color.a;
                return half4(lerp(half3(1, 1, 1), _LineColor.rgb, lineStrength), 1);
            }

            float uvPerScreenPixel = max(1e-6, length(float2(uvDx.x, uvDy.x)));
            float screenPixelsPerArtPixel = 1.0 / (uvPerScreenPixel * _MainTex_TexelSize.z);
            float cellSize = max(1.0, round(screenPixelsPerArtPixel * max(0.25, _PixelSize)));

            float2 cell = floor(input.positionCS.xy / cellSize);
            float2 centerOffset = (cell + 0.5) * cellSize - input.positionCS.xy;
            float2 uv = input.uv + uvDx * centerOffset.x + uvDy * centerOffset.y;
            float2 cellStepX = uvDx * cellSize;
            float2 cellStepY = uvDy * cellSize;

            int radius = (int)clamp(round(_Rings), 1.0, 8.0);
            float radiusSq = (radius + 0.5) * (radius + 0.5);
            float minDistSq = 1e6;

            [loop]
            for (int y = -radius; y <= radius; y++)
            {
                [loop]
                for (int x = -radius; x <= radius; x++)
                {
                    float distSq = x * x + y * y;
                    if (distSq > radiusSq || distSq >= minDistSq) continue;

                    float2 sampleUV = uv + cellStepX * x + cellStepY * y;
                    if (SampleAlphaLod(sampleUV) > 0.5) minDistSq = distSq;
                }
            }

            if (minDistSq > radiusSq) return half4(1, 1, 1, 1);

            float dist = max(1.0, sqrt(minDistSq));
            float edgeFade = saturate((radius + 0.5 - dist) / 0.5);
            half level = _Strength * pow(max(_Falloff, 0.0001), dist - 1.0) * edgeFade;

            bool checker = fmod(cell.x + cell.y, 2.0) < 0.5;
            half lowLevel = level * lerp(1.0, _Falloff, _Checker);
            half strength = (checker ? level : lowLevel) * _OutlineColor.a * input.color.a;

            half3 multiplier = lerp(half3(1, 1, 1), _OutlineColor.rgb, strength) * (1 - strength);
            return half4(multiplier, 1);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
