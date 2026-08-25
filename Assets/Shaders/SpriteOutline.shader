Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness ("Outline Thickness (px)", Range(0, 6)) = 1
        _OutlineEnabled ("Outline Enabled", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float4 _OutlineColor;
            float _OutlineThickness;
            float _OutlineEnabled;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 baseColor = tex2D(_MainTex, i.uv) * i.color;

                if (_OutlineEnabled < 0.5)
                    return baseColor;

                if (baseColor.a > 0.1)
                    return baseColor;

                float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;
                float neighborAlpha = 0.0;

                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2( texel.x, 0)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2(-texel.x, 0)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2(0,  texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2(0, -texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2( texel.x,  texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2(-texel.x,  texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2( texel.x, -texel.y)).a);
                neighborAlpha = max(neighborAlpha, tex2D(_MainTex, i.uv + float2(-texel.x, -texel.y)).a);

                if (neighborAlpha > 0.1)
                    return _OutlineColor;

                return baseColor;
            }
            ENDHLSL
        }
    }
}