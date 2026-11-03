// Постобработка камеры офиса (KuroPost.cs): свечение ярких мест, экспозиция,
// тонирование ACES, контраст, насыщенность, теплый оттенок и виньетка.
// Проходы: 0 — выделить яркое и уменьшить, 1 — уменьшить, 2 — увеличить и сложить, 3 — итог.
Shader "Hidden/Kuro/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _Bloom;
    float _Threshold, _Knee, _BloomIntensity, _Exposure, _Contrast, _Saturation, _Vignette;
    float4 _Tint;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }

    half4 fragPrefilter (v2f i) : SV_Target
    {
        half3 c = min(Box4(i.uv, 1), 20);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 1e-4);
        half k = max(soft, br - _Threshold) / max(br, 1e-4);
        return half4(c * k, 1);
    }

    half4 fragDown (v2f i) : SV_Target { return half4(Box4(i.uv, 1), 1); }

    half4 fragUp (v2f i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }

    half3 Aces (half3 x)
    {
        return saturate(x * (2.51 * x + 0.03) / (x * (2.43 * x + 0.59) + 0.14));
    }

    half4 fragFinal (v2f i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_Bloom, i.uv).rgb * _BloomIntensity;
        c *= _Exposure * _Tint.rgb;
        c = Aces(c);
        half3 g = LinearToGammaSpace(c);
        g = saturate((g - 0.5) * _Contrast + 0.5);
        half l = dot(g, half3(0.2126, 0.7152, 0.0722));
        g = saturate(lerp(l.xxx, g, _Saturation));
        float2 d = (i.uv - 0.5) * float2(_MainTex_TexelSize.y * _MainTex_TexelSize.z, 1);
        g *= 1 - _Vignette * smoothstep(0.25, 0.85, dot(d, d) * 2.2);
        // Легкий шум против ступенек в градиентах.
        g += (frac(sin(dot(i.uv * _MainTex_TexelSize.zw, float2(12.9898, 78.233))) * 43758.5453) - 0.5) / 255.0;
        return half4(GammaToLinearSpace(saturate(g)), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFinal
            ENDCG }
    }
}
