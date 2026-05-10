Shader "Custom/GradientSkybox"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.1, 0.2, 0.5, 1)
        _MidColor ("Mid Color", Color) = (0.5, 0.6, 0.8, 1)
        _BotColor ("Bottom Color", Color) = (0.8, 0.7, 0.5, 1)
        _BotBorder ("Bot→Mid Border", Range(0, 1)) = 0.35
        _BotBlend ("Bot→Mid Blend", Range(0, 0.5)) = 0.1
        _TopBorder ("Mid→Top Border", Range(0, 1)) = 0.65
        _TopBlend ("Mid→Top Blend", Range(0, 0.5)) = 0.1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _MidColor;
            fixed4 _BotColor;
            float _BotBorder;
            float _BotBlend;
            float _TopBorder;
            float _TopBlend;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.texcoord.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // t: 0 = bottom, 1 = top
                float t = normalize(i.dir).y * 0.5 + 0.5;

                // smoothstep blends centered on each border
                float botBlend = smoothstep(_BotBorder - _BotBlend, _BotBorder + _BotBlend, t);
                float topBlend = smoothstep(_TopBorder - _TopBlend, _TopBorder + _TopBlend, t);

                fixed4 col = lerp(_BotColor, _MidColor, botBlend);
                col = lerp(col, _TopColor, topBlend);

                return col;
            }
            ENDCG
        }
    }
}
