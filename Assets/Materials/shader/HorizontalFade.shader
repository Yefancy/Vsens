Shader "Custom/HorizontalFade"
{
    Properties
    {
        _Color("Base Color", Color) = (1,1,1,1)
        _CubeWidth("Width", Float) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="Transparent" }
        LOD 200

        Pass
        {
            // 使用标准Alpha混合方式
            Blend SrcAlpha OneMinusSrcAlpha
            
            // 添加这一行以确保在VR中正确渲染
//            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 添加多视图渲染支持
            #pragma multi_compile_instancing
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                // 将物体空间位置传递到片元着色器
                float3 localPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _CubeWidth;

            v2f vert(appdata v)
            {
                v2f o;
                
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                // 使用物体空间坐标来计算x轴上的fade效果
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
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