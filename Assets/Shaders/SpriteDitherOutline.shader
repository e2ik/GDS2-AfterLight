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

        half4 frag(Varyings input) : SV_Target
        {
            float2 uvDx = ddx(input.uv);
            float2 uvDy = ddy(input.uv);

            if (SampleAlphaLod(input.uv) > 0.5) return half4(1, 1, 1, 1);

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
