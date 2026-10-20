Shader "Custom/Sprite Light Rim"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _RimColor ("Rim Color", Color) = (0, 0, 0, 0)
        _RimDepth ("Depth (pixels)", Range(1, 12)) = 3
        _RimFadePower ("Fade Power", Range(0.25, 4)) = 1
        [Toggle] _RimDiagonals ("Include Diagonals", Float) = 1
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
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

        Blend One One
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
        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _RimColor;
            float _RimDepth;
            float _RimFadePower;
            float _RimDiagonals;
            half _AlphaCutoff;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color;
            return output;
        }

        half Solid(float2 uv)
        {
            return step(_AlphaCutoff, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a);
        }

        half4 frag(Varyings input) : SV_Target
        {
            if (Solid(input.uv) < 0.5) return half4(0, 0, 0, 0);

            int depth = clamp((int)round(_RimDepth), 1, 12);
            int found = 0;

            [unroll]
            for (int i = 1; i <= 12; i++)
            {
                if (i > depth || found > 0) break;

                float2 t = _MainTex_TexelSize.xy * i;
                half open = 1 - Solid(input.uv + float2(0, t.y));

                if (_RimDiagonals > 0.5)
                {
                    open = max(open, 1 - Solid(input.uv + float2(t.x, t.y)));
                    open = max(open, 1 - Solid(input.uv + float2(-t.x, t.y)));
                }

                if (open > 0.5) found = i;
            }

            if (found == 0) return half4(0, 0, 0, 0);

            half fade = 1 - (half)(found - 1) / depth;
            fade = pow(saturate(fade), _RimFadePower);

            return half4(_RimColor.rgb * fade * input.color.a, 0);
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
