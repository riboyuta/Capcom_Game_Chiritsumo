Shader "UI/BlinkRespawnTransition"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _OpenAmount ("Open Amount", Range(0, 1)) = 1
        _Softness ("Softness", Range(0.001, 0.2)) = 0.035
        _Aspect ("Aspect", Float) = 1
        _HorizontalRadius ("Horizontal Radius", Float) = 1.8
        _VerticalRadius ("Vertical Radius", Float) = 1.7
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct V2F
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            fixed4 _Color;
            float _OpenAmount;
            float _Softness;
            float _Aspect;
            float _HorizontalRadius;
            float _VerticalRadius;

            V2F vert(AppData v)
            {
                V2F o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(V2F i) : SV_Target
            {
                float openAmount = saturate(_OpenAmount);

                if (openAmount >= 0.999)
                {
                    return fixed4(_Color.rgb, 0);
                }

                if (openAmount <= 0.001)
                {
                    return fixed4(_Color.rgb, _Color.a * i.color.a);
                }

                float2 p = i.uv * 2.0 - 1.0;
                p.x *= max(_Aspect, 0.0001);

                float rx = max(_Aspect * _HorizontalRadius, 0.001);
                float ry = max(openAmount * _VerticalRadius, 0.001);

                float ellipseValue =
                    (p.x * p.x) / (rx * rx) +
                    (p.y * p.y) / (ry * ry);

                float softness = max(_Softness, 0.0001);

                float visibleInsideEllipse = 1.0 - smoothstep(
                    1.0 - softness,
                    1.0 + softness,
                    ellipseValue
                );

                float alpha = (1.0 - visibleInsideEllipse) * _Color.a * i.color.a;

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}