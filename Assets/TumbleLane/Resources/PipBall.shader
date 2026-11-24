// Pip's look: Standard lighting plus a warm rim light on the sky-facing edge
// and a dark inverted-hull outline, so the ball reads at a glance against the cream track.
Shader "TumbleLane/PipBall"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.45, 0.04, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.55
        _Lift ("Self light", Range(0, 1)) = 0.18
        _RimColor ("Rim colour", Color) = (1, 0.93, 0.72, 1)
        _RimPower ("Rim power", Range(0.5, 8)) = 2.6
        _RimStrength ("Rim strength", Range(0, 3)) = 1.1
        _OutlineColor ("Outline colour", Color) = (0.22, 0.07, 0.02, 1)
        _OutlineWidth ("Outline width (object units)", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "Always" }
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _OutlineColor;
            float _OutlineWidth;
            struct v2f { float4 pos : SV_POSITION; UNITY_FOG_COORDS(0) };
            v2f vert(appdata_base v)
            {
                v2f o;
                float3 p = v.vertex.xyz + normalize(v.normal) * _OutlineWidth;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _OutlineColor;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _RimColor;
        half _Glossiness, _Lift, _RimPower, _RimStrength;
        struct Input { float2 uv_MainTex; float3 viewDir; float3 worldNormal; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            float3 n = normalize(IN.worldNormal);
            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), n)), _RimPower);
            half up = saturate(n.y * 0.6 + 0.55); // strongest on top, faint underneath
            o.Emission = c.rgb * _Lift + _RimColor.rgb * rim * up * _RimStrength;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
