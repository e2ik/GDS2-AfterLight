Shader "UI/Multiply Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _OutlineColor ("Outline Tint", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 0.5
        _Falloff ("Falloff", Range(0, 1)) = 0.5
        _Rings ("Rings", Range(1, 8)) = 3
        _PixelSize ("Pixel Size (art pixels)", Range(0.25, 4)) = 1
        _Checker ("Checker Contrast", Range(0, 1)) = 1
        [Toggle] _LineEnabled ("Line Enabled", Float) = 0
        _LineColor ("Line Color", Color) = (0, 0, 0, 1)
        _LineStrength ("Line Strength", Range(0, 1)) = 1
        [Toggle] _LineDiagonals ("Line Diagonals", Float) = 1

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
        Blend DstColor Zero
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
            float4 _ClipRect;

            half4 _OutlineColor;
            half _Strength;
            half _Falloff;
            float _Rings;
            float _PixelSize;
            half _Checker;
            half _LineEnabled;
            half4 _LineColor;
            half _LineStrength;
            half _LineDiagonals;

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

            half SampleAlphaLod(float2 uv)
            {
                return tex2Dlod(_MainTex, float4(uv, 0, 0)).a;
            }

            half SampleNeighbour(float2 center, float2 offset)
            {
                float2 uv = center + _MainTex_TexelSize.xy * offset;
                if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
                return SampleAlphaLod(uv);
            }

            bool IsLinePixel(float2 uv)
            {
                float2 center = (floor(uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;

                half sides = max(max(SampleNeighbour(center, float2(-1, 0)), SampleNeighbour(center, float2(1, 0))),
                                 max(SampleNeighbour(center, float2(0, -1)), SampleNeighbour(center, float2(0, 1))));
                if (sides > 0.5) return true;
                if (_LineDiagonals < 0.5) return false;

                half corners = max(max(SampleNeighbour(center, float2(-1, -1)), SampleNeighbour(center, float2(1, -1))),
                                   max(SampleNeighbour(center, float2(-1, 1)), SampleNeighbour(center, float2(1, 1))));
                return corners > 0.5;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half clipFactor = 1.0;
                #ifdef UNITY_UI_CLIP_RECT
                clipFactor = UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                float2 uvDx = ddx(IN.texcoord);
                float2 uvDy = ddy(IN.texcoord);

                if (SampleAlphaLod(IN.texcoord) > 0.5 || clipFactor <= 0.0) return fixed4(1, 1, 1, 1);

                if (_LineEnabled > 0.5 && IsLinePixel(IN.texcoord))
                {
                    half lineStrength = _LineStrength * _LineColor.a * IN.color.a;
                    return fixed4(lerp(half3(1, 1, 1), _LineColor.rgb, lineStrength), 1);
                }

                float uvPerScreenPixel = max(1e-6, length(float2(uvDx.x, uvDy.x)));
                float screenPixelsPerArtPixel = 1.0 / (uvPerScreenPixel * _MainTex_TexelSize.z);
                float cellSize = max(1.0, round(screenPixelsPerArtPixel * max(0.25, _PixelSize)));

                float2 cell = floor(IN.vertex.xy / cellSize);
                float2 centerOffset = (cell + 0.5) * cellSize - IN.vertex.xy;
                float2 uv = IN.texcoord + uvDx * centerOffset.x + uvDy * centerOffset.y;
                float2 cellStepX = uvDx * cellSize;
                float2 cellStepY = uvDy * cellSize;

                int radius = (int)clamp(round(_Rings), 1.0, 8.0);
                float radiusSq = (radius + 0.5) * (radius + 0.5);
                float minDistSq = 1e6;

                [loop]
                for (int y = -radius; y <= radius; y++)
                {
                    [loop]
                    for (int x = -radius; x <= radius; x++)
                    {
                        float distSq = x * x + y * y;
                        if (distSq > radiusSq || distSq >= minDistSq) continue;

                        float2 sampleUV = uv + cellStepX * x + cellStepY * y;
                        if (SampleAlphaLod(sampleUV) > 0.5) minDistSq = distSq;
                    }
                }

                if (minDistSq > radiusSq) return fixed4(1, 1, 1, 1);

                float dist = max(1.0, sqrt(minDistSq));
                float edgeFade = saturate((radius + 0.5 - dist) / 0.5);
                half level = _Strength * pow(max(_Falloff, 0.0001), dist - 1.0) * edgeFade;

                bool checker = fmod(cell.x + cell.y, 2.0) < 0.5;
                half lowLevel = level * lerp(1.0, _Falloff, _Checker);
                half strength = (checker ? level : lowLevel) * _OutlineColor.a * IN.color.a * clipFactor;

                half3 multiplier = lerp(half3(1, 1, 1), _OutlineColor.rgb, strength) * (1 - strength);
                return fixed4(multiplier, 1);
            }
            ENDCG
        }
    }
}
