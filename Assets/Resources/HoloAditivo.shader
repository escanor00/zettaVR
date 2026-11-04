// Linhas e preenchimento "modelo cientifico": aditivo, com fresnel (borda mais viva, centro quase transparente) e cor de vertice.
// _Color.a = opacidade geral; _Base = parcela fixa; _Rim = reforco na borda (silhueta). Normais ausentes => sem fresnel.
Shader "Zetta/HoloAditivo"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Cor", Color) = (0.3, 0.8, 1, 0.4)
        _Base ("Parcela fixa", Float) = 0.5
        _Rim ("Reforco na borda", Float) = 1
        _Potencia ("Potencia do fresnel", Float) = 2.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Base;
            float _Rim;
            float _Potencia;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR;
                float2 uv : TEXCOORD0;
                float fres : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float3 mundo = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 vista = normalize(_WorldSpaceCameraPos - mundo);
                float tem = step(0.0001, dot(n, n));
                float f = 1.0 - saturate(abs(dot(n, vista)));
                o.fres = tem * pow(f, _Potencia);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                float a = _Color.a * i.col.a * t.a * (_Base + _Rim * i.fres);
                return fixed4(_Color.rgb * i.col.rgb * t.rgb, saturate(a));
            }
            ENDCG
        }
    }
}
