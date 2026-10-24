Shader "Custom/Sprite Edge Equalizer"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        [Enum(Bottom, 0, Top, 1)] _Edge ("Edge", Float) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1

        [Header(Colors)]
        [HDR] _ColorA ("Color A", Color) = (0.2, 1, 0.8, 1)
        [HDR] _ColorB ("Color B", Color) = (0.35, 0.45, 1, 1)
        [HDR] _ColorC ("Color C", Color) = (1, 0.35, 0.85, 1)
        _ColorSpread ("Color Spread", Range(0.1, 4)) = 1
        _ColorScroll ("Color Scroll Speed", Range(-2, 2)) = 0.05
        _ColorOffset ("Color Offset", Range(0, 1)) = 0

        [Header(Darken)]
        _ShadeColor ("Darken Color", Color) = (0, 0, 0, 1)
        _ShadeHeight ("Darken Height", Range(0.01, 1)) = 0.8
        _ShadeFalloff ("Darken Falloff", Range(0.1, 6)) = 1.5
        _ShadeAlpha ("Darken Alpha", Range(0, 1)) = 0.6

        [Header(Fade)]
        _FadeHeight ("Fade Height", Range(0.01, 1)) = 0.6
        _FadeFalloff ("Fade Falloff", Range(0.1, 6)) = 2
        _FadeAlpha ("Fade Alpha", Range(0, 1)) = 0.5
        _FadeBeatBoost ("Fade Beat Boost", Range(0, 2)) = 0.6
        _FadeLevelBoost ("Fade Level Boost", Range(0, 2)) = 0.5

        [Header(Equalizer)]
        [Toggle] _Mirror ("Mirror (bass in middle)", Float) = 1
        [Enum(Scrolling, 0, Bar Height, 1, Vertical, 2)] _BarColorMode ("Bar Color Mode", Float) = 0
        [IntRange] _BarCount ("Bar Count", Range(4, 128)) = 32
        _BarGap ("Bar Gap", Range(0, 0.9)) = 0.3
        _BarShift ("Bar Shift", Range(-1, 1)) = 0
        _BandOffset ("Band Offset", Range(0, 1)) = 0
        [Toggle] _ReverseBands ("Reverse Bands", Float) = 0
        _BarMinHeight ("Min Height", Range(0, 1)) = 0.02
        _BarMaxHeight ("Max Height", Range(0, 1)) = 0.6
        _BarAlpha ("Bar Alpha", Range(0, 1)) = 0.9
        _BarTopFade ("Bar Top Fade", Range(0, 1)) = 0.6
        _BeatHeightBoost ("Beat Height Boost", Range(0, 1)) = 0.15
        [IntRange] _PixelRows ("Pixel Rows (0 = smooth)", Range(0, 64)) = 0
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
        TEXTURE2D(_AudioSpectrumTex);
        SAMPLER(sampler_AudioSpectrumTex);
        float _AudioBeat;
        float _AudioLevel;
        float _AudioActive;

        CBUFFER_START(UnityPerMaterial)
            float _Edge;
            float _Opacity;
            half4 _ColorA;
            half4 _ColorB;
            half4 _ColorC;
            float _ColorSpread;
            float _ColorScroll;
            float _ColorOffset;
            half4 _ShadeColor;
            float _ShadeHeight;
            float _ShadeFalloff;
            float _ShadeAlpha;
            float _FadeHeight;
            float _FadeFalloff;
            float _FadeAlpha;
            float _FadeBeatBoost;
            float _FadeLevelBoost;
            float _Mirror;
            float _BarColorMode;
            float _BarCount;
            float _BarGap;
            float _BarShift;
            float _BandOffset;
            float _ReverseBands;
            float _BarMinHeight;
            float _BarMaxHeight;
            float _BarAlpha;
            float _BarTopFade;
            float _BeatHeightBoost;
            float _PixelRows;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.color = input.color;
            return output;
        }

        half3 Gradient(float t)
        {
            float s = frac(t) * 3.0;
            if (s < 1.0) return lerp(_ColorA.rgb, _ColorB.rgb, s);
            if (s < 2.0) return lerp(_ColorB.rgb, _ColorC.rgb, s - 1.0);
            return lerp(_ColorC.rgb, _ColorA.rgb, s - 2.0);
        }

        half3 GradientRamp(float t)
        {
            float s = saturate(t) * 2.0;
            return s < 1.0 ? lerp(_ColorA.rgb, _ColorB.rgb, s) : lerp(_ColorB.rgb, _ColorC.rgb, s - 1.0);
        }

        float Spectrum(float x)
        {
            return SAMPLE_TEXTURE2D_LOD(_AudioSpectrumTex, sampler_AudioSpectrumTex, float2(saturate(x), 0.5), 0).r * _AudioActive;
        }

        half4 frag(Varyings input) : SV_Target
        {
            float x = input.uv.x;
            float y = _Edge > 0.5 ? 1.0 - input.uv.y : input.uv.y;
            float t = _Time.y;

            float beat = _AudioBeat * _AudioActive;
            float level = _AudioLevel * _AudioActive;

            float yq = _PixelRows > 0.5 ? (floor(y * _PixelRows) + 0.5) / _PixelRows : y;
            float bx = frac(x + _BarShift);
            float sx = _Mirror > 0.5 ? abs(bx * 2.0 - 1.0) : bx;

            float count = max(_BarCount, 1.0);
            float columns = _Mirror > 0.5 ? count * 0.5 : count;
            float local = frac(sx * columns);
            float center = (floor(sx * columns) + 0.5) / columns;
            float halfGap = _BarGap * 0.5;
            float inBar = step(halfGap, local) * step(local, 1.0 - halfGap);

            float band = _ReverseBands > 0.5 ? 1.0 - center : center;
            band = frac(band + _BandOffset);

            float value = Spectrum(band);
            float height = lerp(_BarMinHeight, _BarMaxHeight, value) * (1.0 + beat * _BeatHeightBoost);
            float fadeStart = height * (1.0 - _BarTopFade);
            float body = yq <= height ? 1.0 : 0.0;
            float topFade = _BarTopFade > 0.001 ? 1.0 - saturate((yq - fadeStart) / max(height - fadeStart, 0.0001)) : 1.0;
            float bars = saturate(inBar * body * lerp(0.35, 1.0, topFade) * _BarAlpha);

            float shade = pow(saturate(1.0 - y / _ShadeHeight), _ShadeFalloff) * _ShadeAlpha * _ShadeColor.a;

            float fade = pow(saturate(1.0 - y / _FadeHeight), _FadeFalloff);
            fade = saturate(fade * _FadeAlpha * (1.0 + beat * _FadeBeatBoost + level * _FadeLevelBoost));

            half3 color = Gradient(x * _ColorSpread + _ColorOffset + t * _ColorScroll);

            half3 premul = _ShadeColor.rgb * shade;
            half alpha = shade;

            premul = color * fade + premul * (1.0 - fade);
            alpha = fade + alpha * (1.0 - fade);

            half3 barColor = color;
            if (_BarColorMode > 1.5) barColor = GradientRamp(yq / max(_BarMaxHeight, 0.0001));
            else if (_BarColorMode > 0.5) barColor = GradientRamp(value);
            barColor *= 1.35;
            premul = barColor * bars + premul * (1.0 - bars);
            alpha = bars + alpha * (1.0 - bars);

            half3 rgb = premul / max(alpha, 0.0001);
            alpha *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a * _Opacity;
            if (alpha <= 0.001) discard;

            return half4(rgb * input.color.rgb, saturate(alpha));
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
