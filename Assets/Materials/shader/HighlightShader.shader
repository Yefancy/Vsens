Shader "Custom/HighlightShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}

        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 8
        _FlickerMin   ("Flicker Min", Range(0, 1))  = 0.3
        _FlickerMax   ("Flicker Max", Range(0, 3))  = 1.2
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;

            float _FlickerSpeed;
            float _FlickerMin;
            float _FlickerMax;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // ✅ flicker：在 Min~Max 之间变化
                float t = _Time.y * _FlickerSpeed;
                float flicker = lerp(_FlickerMin, _FlickerMax, 0.5 + 0.5 * sin(t));

                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // 方案A：整体变亮/变暗（含 alpha）
                col *= flicker;

                // 如果你只想闪“亮度”不想透明度跟着变，把上面一行换成：
                // col.rgb *= flicker;

                return col;
            }
            ENDCG
        }
    }
}
