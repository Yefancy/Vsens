Shader "UI/RoundedBorder"
{
    Properties
    {
        _Color ("Border Color", Color) = (1,1,1,1)
        _Width ("Border Width", Range(0, 0.5)) = 0.1
        _Radius ("Corner Radius", Range(0, 0.5)) = 0.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;
            float _Width;
            float _Radius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 计算到边界的距离（SDF）
                float2 uv = i.uv * 2 - 1; // 转换到[-1,1]范围
                float2 absUV = abs(uv);
                float2 corner = smoothstep(1.0 - _Radius, 1.0 - _Radius + 0.01, absUV);
                float dist = max(absUV.x, absUV.y) - (1.0 - _Radius);
                float border = smoothstep(_Width - 0.01, _Width + 0.01, abs(dist));

                return fixed4(_Color.rgb, _Color.a * border);
            }
            ENDCG
        }
    }
}