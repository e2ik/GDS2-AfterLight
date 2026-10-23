Shader "Custom/Pixel Shooting Stars"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Sprite Texture (mask)", 2D) = "white" {}
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        [Toggle] _UseCustomTime ("Use Custom Time", Float) = 0
        _CustomTime ("Custom Time", Float) = 0

        [Header(Shooting Stars)]
        [HDR] _StarColor ("Trail Color", Color) = (0.7, 0.85, 1, 1)
        [HDR] _HeadColor ("Head Color", Color) = (1, 1, 1, 1)
        _Angle ("Direction (degrees)", Range(0, 360)) = 200
        _Speed ("Speed (pixels per second)", Float) = 120
        _SpeedVariance ("Speed Variance", Range(0, 1)) = 0.4
        _TrailLength ("Trail Length (pixels)", Range(1, 64)) = 14
        _LaneSpacing ("Lane Spacing (pixels)", Range(2, 128)) = 18
        _Span ("Travel Distance (pixels)", Float) = 320
        _Interval ("Average Gap Between Stars (seconds)", Float) = 3
        _Density ("Chance A Lane Fires", Range(0, 1)) = 0.35
        _FadeSteps ("Trail Fade Steps", Range(1, 8)) = 4
        [Toggle] _DitherTrail ("Dither Trail", Float) = 1

        [Header(Background Stars)]
        [HDR] _BgStarColor ("Star Color", Color) = (0.8, 0.85, 1, 1)
        _BgStarDensity ("Star Density", Range(0, 0.05)) = 0.006
        _TwinkleSpeed ("Twinkle Speed", Float) = 2
        _TwinkleMin ("Twinkle Min", Range(0, 1)) = 0.25
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
            half4 _StarColor;
            half4 _HeadColor;
            float _Angle;
            float _Speed;
            float _SpeedVariance;
            float _TrailLength;
            float _LaneSpacing;
            float _Span;
            float _Interval;
            float _Density;
            float _FadeSteps;
            float _DitherTrail;
            half4 _BgStarColor;
            float _BgStarDensity;
            float _TwinkleSpeed;
            float _TwinkleMin;
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

        float Hash21(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        static const float BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        float Bayer4(float2 cell)
        {
            float2 c = cell - 4.0 * floor(cell / 4.0);
            int index = (int)c.x + (int)c.y * 4;
            return (BayerMatrix[index] + 0.5) / 16.0;
        }

        float Quantize(float value)
        {
            float steps = max(1.0, round(_FadeSteps));
            return ceil(saturate(value) * steps) / steps;
        }

        half4 ShootingStars(float2 pixel, float time)
        {
            float rad = radians(_Angle);
            float2 dir = float2(cos(rad), sin(rad));
            float2 perp = float2(-dir.y, dir.x);

            float2 center = pixel + 0.5;
            float along = dot(center, dir);
            float across = dot(center, perp);

            float spacing = max(2.0, _LaneSpacing);
            float lane = floor(across / spacing + 0.5);
            float laneCenter = lane * spacing + floor((Hash11(lane * 7.31 + 1.7) - 0.5) * spacing * 0.6);

            if (abs(across - laneCenter) > 0.5) return 0;

            float speed = _Speed * (1.0 + (Hash11(lane * 3.17 + 9.1) * 2.0 - 1.0) * _SpeedVariance);
            speed = max(1.0, speed);

            float trail = max(1.0, _TrailLength);
            float span = max(1.0, _Span);
            float cycleLength = span + trail + max(0.0, _Interval) * speed;

            if (time < 0.0) return 0;

            float phaseOffset = Hash11(lane * 5.83 + 2.3) * cycleLength;
            float travel = time * speed + phaseOffset;
            float cycle = floor(travel / cycleLength);

            if (cycle * cycleLength - phaseOffset < 0.0) return 0;

            if (Hash21(float2(lane, cycle)) >= _Density) return 0;

            float head = (travel - cycle * cycleLength) - span * 0.5;
            float behind = head - along;

            if (behind < 0.0 || behind > trail) return 0;

            if (behind < 1.0) return _HeadColor;

            float fade = Quantize(1.0 - behind / trail);
            if (_DitherTrail > 0.5)
            {
                if (fade < Bayer4(pixel)) return 0;
                return half4(_StarColor.rgb, _StarColor.a);
            }

            return half4(_StarColor.rgb, _StarColor.a * fade);
        }

        half4 BackgroundStars(float2 pixel, float time)
        {
            if (_BgStarDensity <= 0.0) return 0;

            float h = Hash21(pixel + 17.0);
            if (h >= _BgStarDensity) return 0;

            float phase = Hash21(pixel * 1.37 + 3.0) * 6.2831853;
            float twinkle = lerp(_TwinkleMin, 1.0, 0.5 + 0.5 * sin(time * _TwinkleSpeed + phase));
            return half4(_BgStarColor.rgb, _BgStarColor.a * Quantize(twinkle));
        }

        half4 frag(Varyings input) : SV_Target
        {
            half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
            if (mask <= 0.01) return 0;

            float2 pixel = floor(input.localPos * _PixelsPerUnit);
            float time = _UseCustomTime > 0.5 ? _CustomTime : _Time.y;

            half4 result = BackgroundStars(pixel, time);
            half4 star = ShootingStars(pixel, time);
            if (star.a > 0.0) result = star;

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
