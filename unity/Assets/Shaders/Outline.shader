// Контур подсветки предмета: оболочка чуть больше предмета, видна только по краям.
// Толщина в пикселях почти не зависит от расстояния. _Box = 1 — для кубов (сдвиг по углам).
Shader "Kuro/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (0.886, 0.663, 0.231, 1)
        _Width ("Width", Float) = 0.0035
        _Box ("Box mesh", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "DisableBatching" = "True" }
        Pass
        {
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;
            float _Box;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 s = sign(v.vertex.xyz);
                float3 boxDir = normalize(unity_ObjectToWorld._m00_m10_m20) * s.x
                              + normalize(unity_ObjectToWorld._m01_m11_m21) * s.y
                              + normalize(unity_ObjectToWorld._m02_m12_m22) * s.z;
                float3 smoothDir = UnityObjectToWorldNormal(v.normal);
                float3 dir = lerp(smoothDir, boxDir, _Box);
                wpos += dir * _Width * distance(wpos, _WorldSpaceCameraPos);
                v2f o;
                o.pos = mul(UNITY_MATRIX_VP, float4(wpos, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
