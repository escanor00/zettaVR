// Shader aditivo da esfera da Zetta: um único material para anéis, fragmentos, faíscas e núcleo.
// Cor = gradiente núcleo→borda pela distância ao centro da esfera (_Centro, _Raio em metros no mundo).
// Cor do vértice: g = fase do piscar, b = intensidade do piscar, a = brilho do vértice.
Shader "Zetta/EsferaAditiva"
{
    Properties
    {
        _MainTex ("Textura radial", 2D) = "white" {}
        _CorNucleo ("Cor do núcleo", Color) = (1, 0.95, 0.78, 1)
        _CorBorda ("Cor da borda", Color) = (1, 0.45, 0.04, 1)
        _Brilho ("Brilho", Float) = 1
        _Centro ("Centro (mundo)", Vector) = (0, 0, 0, 0)
        _Raio ("Raio (mundo)", Float) = 0.1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One, One One
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
            fixed4 _CorNucleo;
            fixed4 _CorBorda;
            float _Brilho;
            float4 _Centro;
            float _Raio;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR;
                float2 uv : TEXCOORD0;
                float d : TEXCOORD1;
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
                o.uv = v.uv;
                float3 mundo = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.d = saturate(distance(mundo, _Centro.xyz) / max(_Raio, 0.0001));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                float pisca = 0.5 + 0.5 * sin(_Time.y * (3.0 + i.col.g * 6.0) + i.col.g * 40.0);
                float f = 1.0 - i.col.b * pisca * pisca;
                float3 c = lerp(_CorNucleo.rgb, _CorBorda.rgb, smoothstep(0.05, 0.95, i.d));
                float a = saturate(i.col.a * t.a * f * _Brilho);
                return fixed4(c, a);
            }
            ENDCG
        }
    }
}
