Shader "UI/Scanline Warp Text"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _FaceColor ("Face Color", Color) = (1, 1, 1, 1)
        _Sharpness ("Edge Sharpness", Range(1, 64)) = 32
        _Cutoff ("Edge Cutoff", Range(0, 1)) = 0.5
        _ShiftScale ("Shift Scale (screen px per shift px)", Range(0, 16)) = 4

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [HideInInspector] _CullMode ("Cull Mode", Float) = 0
        [HideInInspector] _GradientScale ("Gradient Scale", Float) = 5
        [HideInInspector] _TextureWidth ("Texture Width", Float) = 512
        [HideInInspector] _TextureHeight ("Texture Height", Float) = 512
        [HideInInspector] _ScaleRatioA ("Scale Ratio A", Float) = 1
        [HideInInspector] _ScaleRatioB ("Scale Ratio B", Float) = 1
        [HideInInspector] _ScaleRatioC ("Scale Ratio C", Float) = 1
        [HideInInspector] _WeightNormal ("Weight Normal", Float) = 0
        [HideInInspector] _WeightBold ("Weight Bold", Float) = 0.5
        [HideInInspector] _FaceDilate ("Face Dilate", Float) = 0
        [HideInInspector] _VertexOffsetX ("Vertex Offset X", Float) = 0
        [HideInInspector] _VertexOffsetY ("Vertex Offset Y", Float) = 0
        [HideInInspector] _MaskSoftnessX ("Mask Softness X", Float) = 0
        [HideInInspector] _MaskSoftnessY ("Mask Softness Y", Float) = 0
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [HideInInspector] _OutlineWidth ("OutlineWidth", Float) = 0
        [HideInInspector] _OutlineSoftness ("OutlineSoftness", Float) = 0
        [HideInInspector] _UnderlayOffsetX ("UnderlayOffsetX", Float) = 0
        [HideInInspector] _UnderlayOffsetY ("UnderlayOffsetY", Float) = 0
        [HideInInspector] _UnderlayDilate ("UnderlayDilate", Float) = 0
        [HideInInspector] _UnderlaySoftness ("UnderlaySoftness", Float) = 0
        [HideInInspector] _GlowOffset ("GlowOffset", Float) = 0
        [HideInInspector] _GlowInner ("GlowInner", Float) = 0
        [HideInInspector] _GlowOuter ("GlowOuter", Float) = 0
        [HideInInspector] _GlowPower ("GlowPower", Float) = 0
        [HideInInspector] _PerspectiveFilter ("PerspectiveFilter", Float) = 0
        [HideInInspector] _ShaderFlags ("ShaderFlags", Float) = 0
        [HideInInspector] _Bevel ("Bevel", Float) = 0
        [HideInInspector] _BevelOffset ("BevelOffset", Float) = 0
        [HideInInspector] _BevelWidth ("BevelWidth", Float) = 0
        [HideInInspector] _BevelClamp ("BevelClamp", Float) = 0
        [HideInInspector] _BevelRoundness ("BevelRoundness", Float) = 0
        [HideInInspector] _BumpOutline ("BumpOutline", Float) = 0
        [HideInInspector] _BumpFace ("BumpFace", Float) = 0
        [HideInInspector] _ScaleX ("ScaleX", Float) = 1
        [HideInInspector] _ScaleY ("ScaleY", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
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
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 texcoord : TEXCOORD0;
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
            float4 _MainTex_TexelSize;
            fixed4 _FaceColor;
            float _Sharpness;
            float _Cutoff;
            float _ShiftScale;
            float4 _ClipRect;

            float4 _MapScanlineLines[8];
            float _MapScanlineCount;
            float4 _MapScanlineArea;
            float _MapScanlineOverlapBoost;
            float4 _MapScanlineExcludes[4];
            float _MapScanlineExcludeCount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord.xy;
                OUT.color = v.color * _FaceColor;
                return OUT;
            }

            half GlyphAlpha(float2 uv)
            {
                half a = tex2D(_MainTex, uv).a;
                return saturate((a - _Cutoff) * _Sharpness + 0.5);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.worldPosition.xy;
                float inArea = step(_MapScanlineArea.x, p.x) * step(p.x, _MapScanlineArea.z)
                             * step(_MapScanlineArea.y, p.y) * step(p.y, _MapScanlineArea.w);

                [unroll]
                for (int e = 0; e < 4; e++)
                {
                    float4 ex = _MapScanlineExcludes[e];
                    float insideExclude = step(e + 0.5, _MapScanlineExcludeCount)
                                        * step(ex.x, p.x) * step(p.x, ex.z) * step(ex.y, p.y) * step(p.y, ex.w);
                    inArea *= 1.0 - insideExclude;
                }

                float brightness = 1.0;
                float shift = 0.0;
                float brightCount = 0.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float4 lineData = _MapScanlineLines[i];
                    float inside = inArea * step(i + 0.5, _MapScanlineCount)
                                 * step(lineData.x, p.y) * step(p.y, lineData.y);

                    brightness *= lerp(1.0, lineData.z, inside);
                    shift += lineData.w * inside;
                    brightCount += inside * step(1.0001, lineData.z);
                }

                brightness *= lerp(1.0, _MapScanlineOverlapBoost, step(1.5, brightCount));

                float2 uvPerScreenPixel = ddx(IN.texcoord);
                float2 uv = IN.texcoord - uvPerScreenPixel * shift * _ShiftScale;

                half4 color = IN.color;
                color.a *= GlyphAlpha(uv);
                color.rgb *= brightness;

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
