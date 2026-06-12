Shader "Custom/Sonar/ChargeWarningBand"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1.0, 0.12, 0.04, 1.0)
        _EdgeColor ("Edge Color", Color) = (1.0, 0.55, 0.15, 1.0)
        _FlowColor ("Flow Color", Color) = (1.0, 0.95, 0.55, 1.0)

        _WarningAlpha ("Warning Alpha", Range(0, 1)) = 0.75
        _WarningPulse ("Warning Pulse", Range(0, 1)) = 0.0
        _WarningProgress ("Warning Progress", Range(0, 1)) = 0.0
        _WarningState ("Warning State", Float) = 0.0
        _LockFlash ("Lock Flash", Range(0, 1)) = 0.0

        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _EndSoftness ("End Softness", Range(0.0, 0.5)) = 0.08

        _NoiseScale ("Noise Scale", Float) = 18.0
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.35

        _FlowSpeed ("Flow Speed", Float) = 1.5
        _FlowStripeScale ("Flow Stripe Scale", Float) = 10.0
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.45

        _EdgeGlowStrength ("Edge Glow Strength", Range(0, 3)) = 1.4
        _LockFlashIntensity ("Lock Flash Intensity", Range(0, 5)) = 2.5

        _WarningLength ("Warning Length", Float) = 1.0
        _WarningWidth ("Warning Width", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float4 _FlowColor;

                float _WarningAlpha;
                float _WarningPulse;
                float _WarningProgress;
                float _WarningState;
                float _LockFlash;

                float _EdgeSoftness;
                float _EndSoftness;

                float _NoiseScale;
                float _NoiseStrength;

                float _FlowSpeed;
                float _FlowStripeScale;
                float _FlowStrength;

                float _EdgeGlowStrength;
                float _LockFlashIntensity;

                float _WarningLength;
                float _WarningWidth;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(a, b, u.x),
                    lerp(c, d, u.x),
                    u.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // uv.x = 長さ方向
                // uv.y = 幅方向
                float across = abs(uv.y - 0.5) * 2.0;

                // 帯の横端をフェードさせる。
                float edgeFade = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, across);

                // 帯の前後端も少しフェードさせて、ただの四角感を減らす。
                float startFade = smoothstep(0.0, max(0.0001, _EndSoftness), uv.x);
                float endFade = 1.0 - smoothstep(1.0 - _EndSoftness, 1.0, uv.x);
                float lengthFade = startFade * endFade;

                float time = _Time.y;

                float noise = ValueNoise(float2(
                    uv.x * _NoiseScale - time * _FlowSpeed,
                    uv.y * _NoiseScale * 0.45));

                // ノイズで帯の透明度を少し崩す。
                float noiseAlpha = lerp(1.0, noise, _NoiseStrength);

                // 長さ方向へ流れる筋。
                float flowRaw = sin((uv.x * _FlowStripeScale - time * _FlowSpeed) * 6.28318 + noise * 2.0);
                float flow = smoothstep(0.15, 0.85, flowRaw * 0.5 + 0.5);

                // 中央と外周で色を変える。
                float edgeGlow = smoothstep(0.45, 1.0, across) * edgeFade;
                float centerPower = 1.0 - saturate(across);

                float lockedState = saturate(1.0 - abs(_WarningState - 1.0));
                float chargingState = saturate(_WarningState - 1.0);

                float lockFlash = saturate(_LockFlash) * _LockFlashIntensity;

                float brightness =
                    0.85
                    + _WarningPulse * 0.25
                    + lockedState * 0.25
                    + chargingState * 0.2
                    + lockFlash;

                float3 color = _BaseColor.rgb;
                color = lerp(color, _EdgeColor.rgb, edgeGlow * _EdgeGlowStrength);
                color = lerp(color, _FlowColor.rgb, flow * _FlowStrength);
                color *= brightness;

                float alpha =
                    _WarningAlpha
                    * edgeFade
                    * lengthFade
                    * noiseAlpha;

                alpha += edgeGlow * 0.15;
                alpha += lockFlash * 0.2;
                alpha = saturate(alpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}