Shader "Custom/Pixel Aurora"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Sprite Texture (mask)", 2D) = "white" {}
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        [Toggle] _UseCustomTime ("Use Custom Time", Float) = 0
        _CustomTime ("Custom Time", Float) = 0
        _FrameRate ("Animation Frame Rate (0 = smooth)", Float) = 12
        _FadeInDuration ("Fade In Duration (seconds)", Float) = 0

        [Header(Colors)]
        [HDR] _BottomColor ("Bottom Color", Color) = (0.3, 1, 0.55, 1)
        [HDR] _MidColor ("Middle Color", Color) = (0.2, 0.85, 0.8, 1)
        [HDR] _TopColor ("Top Color", Color) = (0.6, 0.3, 1, 1)
        _MidPoint ("Middle Color Position", Range(0.05, 0.95)) = 0.35
        _Intensity ("Intensity", Range(0, 2)) = 0.85

        [Header(Curtain)]
        _BaseHeight ("Base Height (pixels from center)", Float) = -10
        _CurtainHeight ("Curtain Height (pixels)", Range(4, 256)) = 48
        _BottomSoftness ("Bottom Softness (pixels)", Range(0.5, 16)) = 2
        _WaveAmplitude ("Wave Amplitude (pixels)", Range(0, 64)) = 12
        _WaveLength ("Wave Length (pixels)", Range(8, 512)) = 140
        _WaveSpeed ("Wave Speed", Float) = 0.25
        _DriftScale ("Drift Noise Scale (pixels)", Range(4, 256)) = 60
        _DriftSpeed ("Drift Speed", Float) = 0.15

        [Header(Rays)]
        _RayWidth ("Ray Width (pixels)", Range(1, 32)) = 4
        _RayStrength ("Ray Strength", Range(0, 1)) = 0.6
        _RaySpeed ("Ray Speed", Float) = 0.6
        _RaySharpness ("Ray Sharpness", Range(0.5, 4)) = 1.5

        [Header(Second Curtain)]
        _SecondStrength ("Strength", Range(0, 1)) = 0.55
        _SecondHeightOffset ("Height Offset (pixels)", Float) = 18
        _SecondScale ("Wave Scale", Range(0.25, 4)) = 0.7

        [Header(Pixel Look)]
        _Steps ("Brightness Steps", Range(1, 8)) = 4
        [Toggle] _Dither ("Dither Between Steps", Float) = 1
        _EdgeFade ("Horizontal Edge Fade (uv)", Range(0, 0.5)) = 0.15
        _TopFade ("Top Edge Fade (uv)", Range(0, 0.5)) = 0.1
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
            float2 localPos : TEXCOORD1;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float _PixelsPerUnit;
            float _UseCustomTime;
            float _CustomTime;
            float _FrameRate;
            float _FadeInDuration;
            half4 _BottomColor;
            half4 _MidColor;
            half4 _TopColor;
            float _MidPoint;
            float _Intensity;
            float _BaseHeight;
            float _CurtainHeight;
            float _BottomSoftness;
            float _WaveAmplitude;
            float _WaveLength;
            float _WaveSpeed;
            float _DriftScale;
            float _DriftSpeed;
            float _RayWidth;
            float _RayStrength;
            float _RaySpeed;
            float _RaySharpness;
            float _SecondStrength;
            float _SecondHeightOffset;
            float _SecondScale;
            float _Steps;
            float _Dither;
            float _EdgeFade;
            float _TopFade;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS);
            float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
            output.positionCS = TransformWorldToHClip(positionWS);
            output.uv = input.uv;
            output.color = input.color;
            output.localPos = (positionWS - originWS).xy;
            return output;
        }

        float Hash11(float n)
        {
            n = frac(n * 0.1031);
            n *= n + 33.33;
            n *= n + n;
            return frac(n);
        }

        float Noise1(float x)
        {
            float i = floor(x);
            float f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(Hash11(i), Hash11(i + 1.0), f);
        }

        static const float BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        float Bayer4(float2 cell)
        {
            float2 c = cell - 4.0 * floor(cell / 4.0);
            int index = (int)c.x + (int)c.y * 4;
            return (BayerMatrix[index] + 0.5) / 16.0;
        }

        float Curtain(float2 p, float time, float seed, float baseHeight, float scale, out float heightT)
        {
            float waveLength = max(1.0, _WaveLength * scale);
            float x = p.x;

            float wave = sin(x * 6.2831853 / waveLength + time * _WaveSpeed + seed * 2.1) * 0.6
                       + sin(x * 6.2831853 / (waveLength * 0.47) - time * _WaveSpeed * 1.3 + seed * 5.7) * 0.25;
            float drift = Noise1(x / max(1.0, _DriftScale * scale) + time * _DriftSpeed + seed * 17.0) * 2.0 - 1.0;
            float baseLine = baseHeight + (wave + drift * 0.5) * _WaveAmplitude * scale;

            float d = p.y - baseLine;
            float height = max(1.0, _CurtainHeight * scale);
            heightT = saturate(d / height);

            float softness = max(0.5, _BottomSoftness);
            if (d < -softness) return 0.0;

            float bottom = saturate(1.0 + d / softness);
            float top = 1.0 - heightT;
            top *= top;

            float rayCell = floor(x / max(1.0, _RayWidth));
            float rays = Noise1(rayCell * 0.73 + time * _RaySpeed + seed * 31.0);
            rays = pow(rays, _RaySharpness);
            float flicker = 0.5 + 0.5 * sin(time * (1.5 + Hash11(rayCell + seed) * 2.5) + Hash11(rayCell * 1.9 + seed) * 6.2831853);
            float rayMask = lerp(1.0, rays * (0.6 + 0.4 * flicker), _RayStrength);

            return bottom * top * rayMask;
        }

        half3 AuroraColor(float t)
        {
            float mid = clamp(_MidPoint, 0.05, 0.95);
            if (t < mid) return lerp(_BottomColor.rgb, _MidColor.rgb, t / mid);
            return lerp(_MidColor.rgb, _TopColor.rgb, (t - mid) / (1.0 - mid));
        }

        float StepAndDither(float value, float2 pixel)
        {
            float steps = max(1.0, round(_Steps));
            float v = saturate(value) * steps;
            float level = floor(v);
            if (_Dither > 0.5 && frac(v) > Bayer4(pixel)) level += 1.0;
            return min(level, steps) / steps;
        }

        half4 frag(Varyings input) : SV_Target
        {
            half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
            if (mask <= 0.01) return 0;

            float time = _UseCustomTime > 0.5 ? _CustomTime : _Time.y;
            float reveal = _FadeInDuration > 0.0 ? saturate(time / _FadeInDuration) : 1.0;
            if (reveal <= 0.0) return 0;

            if (_FrameRate > 0.0) time = floor(time * _FrameRate) / _FrameRate;

            float2 pixel = floor(input.localPos * _PixelsPerUnit);
            float2 p = pixel + 0.5;

            float t1;
            float t2;
            float c1 = Curtain(p, time, 1.0, _BaseHeight, 1.0, t1);
            float c2 = Curtain(p, time * 0.8, 4.3, _BaseHeight + _SecondHeightOffset, _SecondScale, t2) * _SecondStrength;

            float value = max(c1, c2);
            float heightT = c1 >= c2 ? t1 : t2;

            float edgeX = _EdgeFade > 0.0 ? saturate(min(input.uv.x, 1.0 - input.uv.x) / _EdgeFade) : 1.0;
            float edgeY = _TopFade > 0.0 ? saturate((1.0 - input.uv.y) / _TopFade) : 1.0;
            value *= edgeX * edgeY * reveal * _Intensity;

            float alpha = StepAndDither(value, pixel);
            if (alpha <= 0.0) return 0;

            half4 result = half4(AuroraColor(heightT), alpha);
            result.rgb *= input.color.rgb;
            result.a *= mask;
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
