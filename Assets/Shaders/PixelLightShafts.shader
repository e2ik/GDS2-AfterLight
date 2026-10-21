Shader "Custom/Pixel Light Shafts"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Mask (optional)", 2D) = "white" {}
        _Color ("Colour", Color) = (1, 0.95, 0.8, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.6
        _Origin ("Origin (UV)", Vector) = (0.5, 1.15, 0, 0)
        _Resolution ("Pixel Resolution", Vector) = (320, 180, 0, 0)
        _RayCount ("Ray Count", Range(1, 60)) = 18
        _Sharpness ("Ray Sharpness", Range(0.5, 8)) = 2.5
        _Spread ("Fan Spread (radians)", Range(0.1, 3.14)) = 0.9
        _Length ("Length", Range(0.1, 3)) = 1.2
        _StartFade ("Start Fade", Range(0, 1)) = 0.15
        _Speed ("Sway Speed", Range(0, 2)) = 0.15
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 0.6
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
        _Bands ("Bands (0 = smooth)", Range(0, 16)) = 5
        _Dither ("Dither", Range(0, 1)) = 1
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

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                half _Intensity;
                float4 _Origin;
                float4 _Resolution;
                float _RayCount;
                float _Sharpness;
                float _Spread;
                float _Length;
                float _StartFade;
                float _Speed;
                float _PulseSpeed;
                float _PulseAmount;
                float _Bands;
                float _Dither;
            CBUFFER_END

            static const float BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float Bayer4(float2 cell)
            {
                float2 wrapped = cell - 4.0 * floor(cell * 0.25);
                int x = clamp((int)wrapped.x, 0, 3);
                int y = clamp((int)wrapped.y, 0, 3);
                return (BayerMatrix[x + y * 4] + 0.5) / 16.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 resolution = max(_Resolution.xy, float2(1, 1));
                float2 cell = floor(input.uv * resolution);
                float2 uv = (cell + 0.5) / resolution;

                float2 delta = (uv - _Origin.xy) * float2(resolution.x / resolution.y, 1);
                float dist = length(delta);
                float angle = atan2(delta.x, -delta.y);

                float t = _Time.y * _Speed;
                float a = angle * _RayCount;
                float r1 = 0.5 + 0.5 * sin(a + t);
                float r2 = 0.5 + 0.5 * sin(a * 1.73 - t * 0.71 + 1.3);
                float r3 = 0.5 + 0.5 * sin(a * 0.47 + t * 0.37 + 4.1);
                float rays = pow(saturate(r1 * r2 * 0.6 + r3 * 0.4), _Sharpness);

                float fan = 1.0 - smoothstep(_Spread * 0.5, _Spread, abs(angle));
                float fade = saturate(1.0 - dist / _Length);
                fade *= fade;
                float start = smoothstep(0.0, max(_StartFade, 0.0001), dist);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float value = saturate(rays * fan * fade * start * _Intensity * pulse);

                if (_Bands > 0.5)
                {
                    float threshold = lerp(0.5, Bayer4(cell), _Dither);
                    value = saturate(floor(value * _Bands + threshold) / _Bands);
                }

                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half4 colour = _Color * input.color;
                colour.a *= value * mask;
                return colour;
            }
            ENDHLSL
        }
    }
}
