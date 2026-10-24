Shader "Custom/Pixel Heat Haze"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Sprite Texture (mask)", 2D) = "white" {}
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        [Toggle] _UseCustomTime ("Use Custom Time", Float) = 0
        _CustomTime ("Custom Time", Float) = 0
        _FrameRate ("Animation Frame Rate (0 = smooth)", Float) = 12
        _FadeInDuration ("Fade In Duration (seconds)", Float) = 0

        [Header(Distortion)]
        _Strength ("Strength (pixels)", Range(0, 8)) = 2
        _WaveLength ("Wave Length (pixels)", Range(2, 128)) = 18
        _RiseSpeed ("Rise Speed", Float) = 0.6
        _Turbulence ("Turbulence", Range(0, 1)) = 0.4
        _TurbulenceScale ("Turbulence Scale (pixels)", Range(1, 64)) = 6
        _TurbulenceSpeed ("Turbulence Speed", Float) = 1.5
        _BandHeight ("Band Height (pixels)", Range(1, 8)) = 1

        [Header(Shape)]
        _BottomStrength ("Bottom Strength", Range(0, 1)) = 1
        _TopStrength ("Top Strength", Range(0, 1)) = 0
        _EdgeFade ("Horizontal Edge Fade (uv)", Range(0, 0.5)) = 0.15

        [Header(Tint)]
        _TintColor ("Tint", Color) = (1, 0.6, 0.3, 0.08)

        [Header(Embers)]
        [HDR] _EmberColor ("Start Color", Color) = (1, 0.75, 0.3, 1)
        [HDR] _EmberEndColor ("End Color", Color) = (1, 0.25, 0.1, 1)
        _EmberDensity ("Density", Range(0, 1)) = 0.35
        _EmberSpacing ("Column Spacing (pixels)", Range(2, 64)) = 10
        _EmbersPerColumn ("Embers Per Column", Range(1, 3)) = 2
        _EmberSize ("Size (pixels)", Range(1, 3)) = 1
        _EmberSpeed ("Rise Speed (pixels per second)", Float) = 14
        _EmberSpeedVariance ("Speed Variance", Range(0, 1)) = 0.4
        _EmberRiseHeight ("Rise Height (fraction of sprite)", Range(0.05, 1)) = 0.8
        _EmberSway ("Sway (pixels)", Range(0, 6)) = 2
        _EmberSwayFrequency ("Sway Frequency", Float) = 1.5
        _EmberFlicker ("Flicker", Range(0, 1)) = 0.4
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
            float4 screenPos : TEXCOORD2;
            float2 worldPos : TEXCOORD3;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_CameraSortingLayerTexture);
        SAMPLER(sampler_point_clamp);

        CBUFFER_START(UnityPerMaterial)
            float _PixelsPerUnit;
            float _UseCustomTime;
            float _CustomTime;
            float _FrameRate;
            float _FadeInDuration;
            float _Strength;
            float _WaveLength;
            float _RiseSpeed;
            float _Turbulence;
            float _TurbulenceScale;
            float _TurbulenceSpeed;
            float _BandHeight;
            float _BottomStrength;
            float _TopStrength;
            float _EdgeFade;
            half4 _TintColor;
            half4 _EmberColor;
            half4 _EmberEndColor;
            float _EmberDensity;
            float _EmberSpacing;
            float _EmbersPerColumn;
            float _EmberSize;
            float _EmberSpeed;
            float _EmberSpeedVariance;
            float _EmberRiseHeight;
            float _EmberSway;
            float _EmberSwayFrequency;
            float _EmberFlicker;
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
            output.screenPos = ComputeScreenPos(output.positionCS);
            output.worldPos = positionWS.xy;
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

        half4 Embers(float pixelX, float rowFromBottom, float heightPixels, float time)
        {
            if (_EmberDensity <= 0.0 || heightPixels < 1.0) return 0;

            float spacing = max(2.0, round(_EmberSpacing));
            float size = max(1.0, round(_EmberSize));
            float risePixels = max(1.0, heightPixels * _EmberRiseHeight);
            int perColumn = (int)clamp(round(_EmbersPerColumn), 1.0, 3.0);
            float baseColumn = floor(pixelX / spacing);

            [unroll]
            for (int c = -1; c <= 1; c++)
            {
                float column = baseColumn + c;

                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    if (k >= perColumn) continue;

                    float seed = column * 13.73 + k * 71.31;
                    if (Hash11(seed + 0.5) >= _EmberDensity) continue;

                    float speed = max(1.0, _EmberSpeed * (1.0 + (Hash11(seed + 1.7) * 2.0 - 1.0) * _EmberSpeedVariance));
                    float travel = time * speed / risePixels + Hash11(seed + 2.9);
                    float cycle = floor(travel);
                    float t = travel - cycle;

                    float jitter = floor((Hash11(seed + cycle * 3.17 + 4.1) - 0.5) * spacing * 0.8);
                    float sway = round(sin((t * _EmberSwayFrequency + Hash11(seed + 5.3)) * 6.2831853) * _EmberSway);
                    float px = column * spacing + jitter + sway;
                    float py = floor(t * risePixels);

                    if (pixelX < px || pixelX >= px + size) continue;
                    if (rowFromBottom < py || rowFromBottom >= py + size) continue;

                    float fade = saturate(t / 0.1) * (1.0 - t);
                    float flickerRoll = Hash11(seed + floor(time * 10.0) * 1.31);
                    float flicker = flickerRoll < _EmberFlicker * 0.5 ? 0.4 : 1.0;
                    float alpha = ceil(saturate(fade * 1.6) * flicker * 3.0) / 3.0;

                    half3 color = lerp(_EmberColor.rgb, _EmberEndColor.rgb, t);
                    return half4(color, alpha * _EmberColor.a);
                }
            }

            return 0;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float uvPerArtPixel = abs(ddy(input.uv.y)) / max(abs(ddy(input.localPos.y * _PixelsPerUnit)), 1e-5);
            float heightPixels = uvPerArtPixel > 0.0 ? round(1.0 / uvPerArtPixel) : 0.0;
            float worldPerScreenPixel = max(abs(ddx(input.worldPos.x)), 1e-5);

            half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
            if (mask <= 0.01) return 0;

            float time = _UseCustomTime > 0.5 ? _CustomTime : _Time.y;
            float reveal = _FadeInDuration > 0.0 ? saturate(time / _FadeInDuration) : 1.0;
            if (reveal <= 0.0) return 0;
            if (_FrameRate > 0.0) time = floor(time * _FrameRate) / _FrameRate;

            float2 pixel = floor(input.localPos * _PixelsPerUnit);
            float rows = max(1.0, round(_BandHeight));
            float row = floor(pixel.y / rows);
            float wave = sin((row * rows / max(1.0, _WaveLength) - time * _RiseSpeed) * 6.2831853);
            float turbulence = Noise1(row * rows / max(1.0, _TurbulenceScale) - time * _TurbulenceSpeed) * 2.0 - 1.0;
            float shape = lerp(wave, turbulence, _Turbulence);

            float vertical = lerp(_BottomStrength, _TopStrength, saturate(input.uv.y));
            float edge = _EdgeFade > 0.0 ? saturate(min(input.uv.x, 1.0 - input.uv.x) / _EdgeFade) : 1.0;
            float amount = vertical * edge * mask * reveal;

            float offsetPixels = round(shape * _Strength * amount);

            float screenPixelsPerArtPixel = (1.0 / max(1.0, _PixelsPerUnit)) / worldPerScreenPixel;

            float2 screenUV = input.screenPos.xy / input.screenPos.w;
            screenUV.x += offsetPixels * screenPixelsPerArtPixel / _ScreenParams.x;

            half3 scene = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_point_clamp, screenUV).rgb;
            half3 tinted = lerp(scene, _TintColor.rgb, _TintColor.a * amount);

            float rowFromBottom = floor(saturate(input.uv.y) * heightPixels);
            half4 ember = Embers(pixel.x, rowFromBottom, heightPixels, time);
            float emberAlpha = ember.a * edge * reveal;
            tinted = lerp(tinted, ember.rgb, emberAlpha);

            return half4(tinted * input.color.rgb, mask);
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
