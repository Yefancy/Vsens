Shader "Custom/XAxisEdgeHighlightCustomSize"
{
    Properties
    {
        _HighlightColor("高亮颜色", Color) = (1,1,0,1)
        _EdgeThreshold("边缘阈值", Float) = 0.1
        // 自定义Cube尺寸，格式为 (width, height, depth, 0)
        _CubeSize("Cube尺寸", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        // 关闭剔除，保证所有面都能显示效果
        Cull Off

        Pass
        {
            // 启用alpha混合
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // 顶点输入结构
            struct appdata
            {
                float4 vertex : POSITION;
            };

            // 传递给片元的变量
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            fixed4 _HighlightColor;
            float _EdgeThreshold;
            float4 _CubeSize; // x:宽度, y:高度, z:深度

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // 注意：这里的v.vertex为默认Cube的局部坐标（[-0.5,0.5]）
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 将默认Cube的局部坐标映射到自定义尺寸下的实际坐标（只处理y和z）
                float normY = i.localPos.y * _CubeSize.y;
                float normZ = i.localPos.z * _CubeSize.z;
                float2 p = float2(normY, normZ);

                // 计算y和z方向的半尺寸，边沿中心即为(y,z) = (±halfY, ±halfZ)
                float halfY = _CubeSize.y * 0.5;
                float halfZ = _CubeSize.z * 0.5;
                float2 edge1 = float2(-halfY, -halfZ);
                float2 edge2 = float2(-halfY,  halfZ);
                float2 edge3 = float2( halfY, -halfZ);
                float2 edge4 = float2( halfY,  halfZ);

                // 计算当前片元到四个x轴边沿中心的距离
                float d1 = distance(p, edge1);
                float d2 = distance(p, edge2);
                float d3 = distance(p, edge3);
                float d4 = distance(p, edge4);
                float minDist = min(min(d1, d2), min(d3, d4));

                // 使用smoothstep使得距离越近，边沿效果越明显
                float edgeFactor = 1.0 - smoothstep(0.0, _EdgeThreshold, minDist);

                fixed4 col = _HighlightColor;
                col.a *= edgeFactor;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
