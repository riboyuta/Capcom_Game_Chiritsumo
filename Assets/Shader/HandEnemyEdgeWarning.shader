Shader "UI/HandEnemyEdgeWarning"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}

        _WarningColor ("Warning Color", Color) = (1, 0, 0, 1)
        _WarningAlpha ("Warning Alpha", Range(0, 1)) = 0
        _Edge ("Edge", Float) = 0

        _EdgeThickness ("Edge Thickness", Range(0.01, 0.5)) = 0.18
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.22
        _NoiseScale ("Noise Scale", Float) = 42
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
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

            sampler2D _MainTex;

            fixed4 _WarningColor;
            float _WarningAlpha;
            float _Edge;
            float _EdgeThickness;
            float _EdgeSoftness;
            float _NoiseScale;
            float _NoiseStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;

                float distanceToEdge = uv.x;

                if (_Edge > 0.5 && _Edge < 1.5)
                {
                    distanceToEdge = 1.0 - uv.x;
                }
                else if (_Edge > 1.5 && _Edge < 2.5)
                {
                    distanceToEdge = 1.0 - uv.y;
                }
                else if (_Edge > 2.5)
                {
                    distanceToEdge = uv.y;
                }

                float edgeMask = 1.0 - smoothstep(
                    _EdgeThickness,
                    _EdgeThickness + _EdgeSoftness,
                    distanceToEdge
                );

                float2 noiseUv = uv * _NoiseScale + float2(_Time.y * 3.1, _Time.y * 1.7);
                float noise = Hash(floor(noiseUv));

                float wave = sin(distanceToEdge * 80.0 - _Time.y * 18.0 + noise * 6.28318);
                wave = wave * 0.5 + 0.5;

                float noiseAlpha = lerp(1.0, 0.45 + noise * 0.85, _NoiseStrength);
                float waveAlpha = lerp(1.0, wave, 0.35);

                float alpha = edgeMask * noiseAlpha * waveAlpha * _WarningAlpha;
                alpha = saturate(alpha * _WarningColor.a * input.color.a);

                return fixed4(_WarningColor.rgb, alpha);
            }

            ENDCG
        }
    }
}