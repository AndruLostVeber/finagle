// Мягкое затенение-подложка: пятно под предметом или полоса в углу комнаты.
// Умножает то, что уже нарисовано, на (1 − альфа маски × сила). Без записи глубины.
Shader "Kuro/AO"
{
    Properties
    {
        _MainTex ("Mask (alpha)", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend DstColor Zero
            ZWrite Off
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Strength;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half a = tex2D(_MainTex, i.uv).a * _Strength;
                return half4((1 - a).xxx, 1);
            }
            ENDCG
        }
    }
}
