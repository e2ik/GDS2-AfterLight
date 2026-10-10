Shader "UI/Inner Stroke"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _StrokeColor ("Stroke Color", Color) = (1, 1, 1, 1)
        _StrokeWidth ("Stroke Width (texels)", Range(1, 4)) = 1
        _PulseSpeed ("Pulse Speed", Float) = 0
        _PulseMinAlpha ("Pulse Min Alpha", Range(0, 1)) = 0.3
        _AlphaThreshold ("Alpha Threshold", Range(0.01, 1)) = 0.5
        [Toggle] _BrightenMode ("Brighten Instead Of Colour", Float) = 0
        _Brightness ("Brightness", Range(1, 4)) = 1.5
        _BrightenLift ("Brighten Lift", Range(0, 1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _StrokeColor;
            float _StrokeWidth;
            float _PulseSpeed;
            float _PulseMinAlpha;
            float _AlphaThreshold;
            float _BrightenMode;
            float _Brightness;
            float _BrightenLift;
            float _UIUnscaledTime;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float SampleAlpha(float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2D(_MainTex, uv).a * inside;
            }

            float MinNeighbourAlpha(float2 uv, float2 o)
            {
                float a = SampleAlpha(uv + float2(o.x, 0));
                a = min(a, SampleAlpha(uv - float2(o.x, 0)));
                a = min(a, SampleAlpha(uv + float2(0, o.y)));
                a = min(a, SampleAlpha(uv - float2(0, o.y)));
                a = min(a, SampleAlpha(uv + o));
                a = min(a, SampleAlpha(uv - o));
                a = min(a, SampleAlpha(uv + float2(o.x, -o.y)));
                a = min(a, SampleAlpha(uv + float2(-o.x, o.y)));
                return a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float strokeTexels = max(1.0, round(_StrokeWidth));
                float minAlpha = MinNeighbourAlpha(IN.texcoord, _MainTex_TexelSize.xy * strokeTexels);

                float texAlpha = tex2D(_MainTex, IN.texcoord).a;
                float isEdge = step(_AlphaThreshold, texAlpha) * (1.0 - step(_AlphaThreshold, minAlpha));

                float pulse = 1.0;
                if (_PulseSpeed > 0.0)
                {
                    float wave = (1.0 - cos(_UIUnscaledTime * _PulseSpeed * 6.2831853)) * 0.5;
                    pulse = lerp(_PulseMinAlpha, 1.0, wave);
                }

                float strokeAmount = isEdge * _StrokeColor.a * pulse;
                float3 brightened = saturate(color.rgb * _Brightness + _BrightenLift);
                float3 strokeRgb = lerp(_StrokeColor.rgb, brightened, step(0.5, _BrightenMode));
                color.rgb = lerp(color.rgb, strokeRgb, strokeAmount);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
