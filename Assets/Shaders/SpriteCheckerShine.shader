Shader "Custom/Sprite Checker Shine"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Shine)]
        [HDR] _ShineColor ("Shine Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(1, 10)) = 2
        _Additive ("Additive (lights dark pixels)", Range(0, 1)) = 0.1
        _ShineFade ("Shine Fade", Range(0, 1)) = 1
        [Toggle] _OverlayOnly ("Overlay Only", Float) = 0
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        [Toggle] _FlipDirection ("Flip Direction", Float) = 0
        _Width ("Core Width (pixels)", Range(0, 16)) = 1
        _Fringe ("Fringe Width (pixels)", Range(0, 16)) = 2

        [Header(Checker)]
        _CheckerLow ("Checker Low", Range(0, 1)) = 0.4
        _CheckerSize ("Checker Size (pixels)", Range(1, 4)) = 1

        [Header(Fill and Outline)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 1
        _EdgeAmount ("Outline Amount", Range(0, 1)) = 1
        _EdgeWidth ("Outline Width (pixels)", Range(1, 4)) = 1
        _EdgeAdditive ("Outline Additive (lights dark pixels)", Range(0, 1)) = 0.5
        [Toggle] _EdgeAlways ("Outline Always On", Float) = 0
        [Enum(Inside, 0, Outside, 1)] _EdgeSide ("Outline Side", Float) = 0

        [Header(Timing)]
        _SweepFrom ("Sweep From (pixels)", Float) = -32
        _SweepTo ("Sweep To (pixels)", Float) = 32
        _Duration ("Sweep Duration", Float) = 0.6
        _Delay ("Delay Between Sweeps", Float) = 2
        _TimeOffset ("Time Offset", Float) = 0
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
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

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
            float2 objectPos : TEXCOORD1;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _ShineColor;
            half _Intensity;
            half _Additive;
            half _ShineFade;
            half _OverlayOnly;
            float _PixelsPerUnit;
            half _FlipDirection;
            float _Width;
            float _Fringe;
            half _CheckerLow;
            float _CheckerSize;
            half _FillAmount;
            half _EdgeAmount;
            float _EdgeWidth;
            half _EdgeAdditive;
            half _EdgeAlways;
            half _EdgeSide;
            float _SweepFrom;
            float _SweepTo;
            float _Duration;
            float _Delay;
            float _TimeOffset;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
            output.positionCS = TransformObjectToHClip(positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color * unity_SpriteColor;
            output.objectPos = positionOS.xy;
            return output;
        }

        half SampleNeighbourAlpha(float2 uv)
        {
            if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
            return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
        }

        bool IsEdge(float2 uv, bool outside)
        {
            float2 texel = _MainTex_TexelSize.xy;
            int width = clamp((int)round(_EdgeWidth), 1, 4);

            [loop]
            for (int i = 1; i <= width; i++)
            {
                float2 dx = float2(texel.x * i, 0.0);
                float2 dy = float2(0.0, texel.y * i);

                half a0 = SampleNeighbourAlpha(uv + dx);
                half a1 = SampleNeighbourAlpha(uv - dx);
                half a2 = SampleNeighbourAlpha(uv + dy);
                half a3 = SampleNeighbourAlpha(uv - dy);

                if (outside)
                {
                    if (max(max(a0, a1), max(a2, a3)) > 0.01) return true;
                }
                else
                {
                    if (min(min(a0, a1), min(a2, a3)) <= 0.01) return true;
                }
            }

            return false;
        }

        half4 frag(Varyings input) : SV_Target
        {
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            bool opaque = tex.a > 0.01;
            bool outside = _EdgeSide > 0.5;
            bool edgeOn = _EdgeAmount > 0.001;
            bool overlay = _OverlayOnly > 0.5;
            half4 untouched = overlay ? half4(0, 0, 0, 0) : tex;

            if (_ShineFade <= 0.001) return opaque ? untouched : half4(0, 0, 0, 0);
            if (!opaque && !(outside && edgeOn)) return half4(0, 0, 0, 0);

            float2 p = floor(input.objectPos * _PixelsPerUnit);
            float diagonal = _FlipDirection > 0.5 ? p.x - p.y : p.x + p.y;

            float reach = _Width + _Fringe;
            float cycle = max(0.001, _Duration + _Delay);
            float t = fmod(_Time.y + _TimeOffset, cycle);
            float progress = saturate(t / max(0.001, _Duration));
            float sweep = round(lerp(_SweepFrom - reach - 1.0, _SweepTo + reach + 1.0, progress));
            float dist = abs(diagonal - sweep);

            bool inBand = dist <= reach;
            bool edgeAlways = _EdgeAlways > 0.5;
            bool edgeActive = edgeOn && (inBand || edgeAlways);
            float2 texelUV = (floor(input.uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
            half3 light = _ShineColor.rgb * _Intensity;

            if (!opaque)
            {
                if (!edgeActive || !IsEdge(texelUV, true)) return half4(0, 0, 0, 0);
                return half4(light, _EdgeAmount * _ShineFade * input.color.a);
            }

            if (!inBand && !(edgeActive && !outside)) return untouched;

            half shine = 0.0;
            half additive = _Additive;

            if (inBand)
            {
                float cellSize = max(1.0, round(_CheckerSize));
                float2 cell = floor(p / cellSize);
                bool checker = fmod(abs(cell.x + cell.y), 2.0) < 0.5;

                half fill = dist <= _Width
                    ? (checker ? 1.0 : _CheckerLow)
                    : (checker ? _CheckerLow : 0.0);

                shine = fill * _FillAmount;
            }

            if (edgeActive && !outside && _EdgeAmount >= shine && IsEdge(texelUV, false))
            {
                shine = _EdgeAmount;
                additive = _EdgeAdditive;
            }

            shine *= _ShineFade;
            if (shine <= 0.001) return untouched;
            half3 rgb = tex.rgb * lerp(half3(1, 1, 1), light, shine) + light * additive * shine;

            return half4(rgb, tex.a);
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
