Shader "Custom/Sprite Star Glow"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle] _PixelSnap ("Pixel Snap", Float) = 1

        [Header(Edge Glow)]
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.95, 0.6, 1)
        [IntRange] _GlowRadius ("Glow Radius (sprite pixels)", Range(1, 12)) = 4
        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 1
        _GlowFalloff ("Glow Falloff", Range(0.1, 4)) = 1.5
        [IntRange] _GlowSteps ("Glow Steps (0 = smooth)", Range(0, 8)) = 3

        [Header(Pulse)]
        _PulseSpeed ("Pulses Per Second", Range(0, 4)) = 0.75
        _PulseRadiusMin ("Radius At Low", Range(0, 1)) = 0.4
        _PulseIntensityMin ("Intensity At Low", Range(0, 1)) = 0.5
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

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _AlphaCutoff;
            float _PixelSnap;
            half4 _GlowColor;
            float _GlowRadius;
            float _GlowIntensity;
            float _GlowFalloff;
            float _GlowSteps;
            float _PulseSpeed;
            float _PulseRadiusMin;
            float _PulseIntensityMin;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.color = input.color;
            return output;
        }

        float2 TextureSize()
        {
            uint width;
            uint height;
            _MainTex.GetDimensions(width, height);
            return float2(max(width, 1u), max(height, 1u));
        }

        half SpriteAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
        }

        float EdgeDistance(float2 uv, float2 texel, float maxRadius)
        {
            int radius = (int)min(maxRadius, 12.0);
            float best = 999.0;
            float bestSq = (maxRadius + 0.5) * (maxRadius + 0.5);

            [loop]
            for (int y = -radius; y <= radius; y++)
            {
                [loop]
                for (int x = -radius; x <= radius; x++)
                {
                    float distSq = (float)(x * x + y * y);
                    if (distSq == 0.0 || distSq >= bestSq) continue;

                    if (SpriteAlpha(uv + float2(x, y) * texel) >= _AlphaCutoff)
                    {
                        bestSq = distSq;
                        best = sqrt(distSq);
                    }
                }
            }

            return best;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float2 size = TextureSize();
            float2 texel = 1.0 / size;
            float2 uv = _PixelSnap > 0.5 ? (floor(input.uv * size) + 0.5) * texel : input.uv;

            float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed * TWO_PI);
            half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

            if (sprite.a >= _AlphaCutoff)
            {
                return sprite * _Color * input.color;
            }

            float glowRadius = _GlowRadius * lerp(_PulseRadiusMin, 1.0, pulse);
            float edge = EdgeDistance(uv, texel, glowRadius);
            if (edge > glowRadius) discard;

            float glow = saturate(1.0 - (edge - 1.0) / max(glowRadius, 0.0001));
            glow = pow(glow, _GlowFalloff);
            if (_GlowSteps > 0.5) glow = ceil(glow * _GlowSteps) / _GlowSteps;
            glow *= _GlowIntensity * lerp(_PulseIntensityMin, 1.0, pulse);

            half alpha = saturate(_GlowColor.a * glow) * input.color.a * _Color.a;
            if (alpha <= 0.001) discard;
            return half4(_GlowColor.rgb, alpha);
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
