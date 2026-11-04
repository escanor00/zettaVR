// Vidro holográfico da Zetta: translucidez + borda fina + leve gradiente, tudo no próprio shader (sem desfoque, sem grab pass).
// Uma quad com UV 0..1; _Tamanho = largura/altura em metros (para manter o raio dos cantos e a espessura da borda constantes).
Shader "Zetta/VidroHolo"
{
    Properties
    {
        _Color ("Multiplicador (alfa geral)", Color) = (1, 1, 1, 1)
        _CorTopo ("Fundo (topo)", Color) = (0.10, 0.30, 0.45, 0.30)
        _CorBase ("Fundo (base)", Color) = (0.03, 0.12, 0.22, 0.14)
        _CorBorda ("Borda", Color) = (0.26, 0.78, 1, 1)
        _Tamanho ("Tamanho (m)", Vector) = (0.3, 0.2, 0, 0)
        _Raio ("Raio dos cantos (m)", Float) = 0.012
        _Espessura ("Espessura da borda (m)", Float) = 0.0018
        _Margem ("Margem da aura (m)", Float) = 0.02
        _Aura ("Aura aditiva", Float) = 0.5
        _Brilho ("Brilho da borda", Float) = 1
        _Realce ("Realce (0..1)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _CorTopo, _CorBase, _CorBorda;
            float4 _Tamanho;
            float _Raio, _Espessura, _Brilho, _Realce, _Margem, _Aura;
            // ajustes ao vivo (AjustesZetta): globais; 0 = não definido → 1
            float _ZettaOpac, _ZettaBorda;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 meio = _Tamanho.xy * 0.5;
                float2 p = (i.uv - 0.5) * (_Tamanho.xy + 2.0 * _Margem);
                // distância assinada de um retângulo de cantos arredondados (negativa dentro)
                float2 q = abs(p) - (meio - _Raio);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Raio;
                float aa = max(fwidth(d), 0.0002);
                float dentro = 1.0 - smoothstep(-aa, aa, d);
                float borda = dentro * smoothstep(-_Espessura - aa, -_Espessura + aa, d);

                float t = saturate(i.uv.y);
                fixed4 fundo = lerp(_CorBase, _CorTopo, t);
                // faixa de luz lenta e sutil para dar vida ao vidro
                float faixa = smoothstep(0.0, 0.5, 0.5 - abs(frac(i.uv.y * 0.6 - _Time.y * 0.05) - 0.5)) * 0.05;
                float af = saturate(fundo.a + faixa + _Realce * 0.2) * dentro * (1.0 - borda);
                float3 rgb = (fundo.rgb + _CorBorda.rgb * (faixa * 2.0 + _Realce * 0.3)) * af;
                // borda sólida e brilhante + aura aditiva (fora e um pouco dentro), sem escurecer o que está atrás
                float luz = _Brilho * (0.8 + 0.2 * t) * (_ZettaBorda > 0.0 ? _ZettaBorda : 1.0);
                rgb += _CorBorda.rgb * borda * luz;
                float aura = (d > 0.0 ? exp(-d / max(_Margem, 0.0001) * 3.5) * _Aura : 0.0) + dentro * exp(d / 0.008) * 0.3;
                rgb += _CorBorda.rgb * aura * _Brilho * 0.6 * (1.0 - borda);
                float a = saturate(af + borda);
                float k = _Color.a * (_ZettaOpac > 0.0 ? _ZettaOpac : 1.0);
                return fixed4(rgb * k, saturate(a * k));
            }
            ENDCG
        }
    }
}
