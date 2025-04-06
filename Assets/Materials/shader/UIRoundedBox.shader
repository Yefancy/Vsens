Shader "UI/RoundedRectWithBorder_Fixed"
{
    Properties
    {
        _Color("Fill Color", Color) = (1,1,1,1)
        _BorderColor("Border Color", Color) = (0,0,0,1)
        _Radius("Corner Radius", Float) = 30
        _BorderWidth("Border Width", Float) = 4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Color;
            float4 _BorderColor;
            float _Radius;
            float _BorderWidth;

            float roundedBoxSDF(float2 uv, float2 size, float radius)
            {
                float2 halfSize = size * 0.5;
                float2 pos = uv * size;
                float2 d = abs(pos - halfSize) - (halfSize - radius);
                return length(max(d, 0.0)) - radius;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 size = float2(1.0, 1.0); // normalized space
                float radius = _Radius / 100.0;
                float border = _BorderWidth / 100.0;

                float sdf = roundedBoxSDF(i.uv, size, radius);
                float antiAlias = fwidth(sdf);

                float fillAlpha = smoothstep(0.0, -antiAlias, sdf);
                float borderAlpha = smoothstep(border + antiAlias, border - antiAlias, abs(sdf));

                float4 col = lerp(_BorderColor, _Color, borderAlpha);
                col.a *= fillAlpha;
                return col;
            }
            ENDCG
        }
    }
}
