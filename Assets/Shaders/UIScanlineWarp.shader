Shader "UI/Scanline Warp"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _KeepShape ("Keep Original Shape", Float) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            #pragma target 3.0

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

            float4 _MapScanlineLines[8];
            float _MapScanlineCount;
            float4 _MapScanlineArea;
            float _MapScanlineOverlapBoost;
            float _KeepShape;

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

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.worldPosition.xy;
                float inArea = step(_MapScanlineArea.x, p.x) * step(p.x, _MapScanlineArea.z)
                             * step(_MapScanlineArea.y, p.y) * step(p.y, _MapScanlineArea.w);

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

                float2 uv = IN.texcoord - float2(_MainTex_TexelSize.x * shift, 0);
                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                if (_KeepShape > 0.5)
                {
                    half originalAlpha = (tex2D(_MainTex, IN.texcoord).a + _TextureSampleAdd.a) * IN.color.a;
                    color.a = min(color.a, originalAlpha);
                }
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
