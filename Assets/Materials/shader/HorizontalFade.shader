Shader "Custom/HorizontalFade"
{
    Properties
    {
        _Color("Base Color", Color) = (1,1,1,1)
        _CubeWidth("Width", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            // 使用标准Alpha混合方式
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                // 将物体空间位置传递到片元着色器
                float3 localPos : TEXCOORD0;
            };

            fixed4 _Color;
            float _CubeWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 使用物体空间坐标来计算x轴上的fade效果
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fade = saturate(abs(i.localPos.x / _CubeWidth));
                fixed4 col = _Color;
                col.a *= (1.0 - fade);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
