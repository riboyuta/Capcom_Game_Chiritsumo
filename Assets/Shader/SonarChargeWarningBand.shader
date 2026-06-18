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
        _LockSweepProgress("Lock Sweep Progress", Range(0, 1)) = 0.0
        _LockSweepColor("Lock Sweep Color", Color) = (1.0, 0.95, 0.55, 1.0)
        _LockSweepWidth("Lock Sweep Width", Range(0.005, 0.3)) = 0.06
        _LockSweepSoftness("Lock Sweep Softness", Range(0.001, 0.2)) = 0.04
        _LockSweepIntensity("Lock Sweep Intensity", Range(0, 5)) = 2.0

        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _EndSoftness ("End Softness", Range(0.0, 0.5)) = 0.08

        _CoreWidthRatio("Core Width Ratio", Range(0.4, 0.95)) = 0.78
        _EdgeNoiseScale("Edge Noise Scale", Float) = 1.2
        _EdgeNoiseAmount("Edge Noise Amount", Range(0, 0.25)) = 0.06
        _EdgeWobbleSpeed("Edge Wobble Speed", Float) = 0.35
        _EdgeGlowWidth("Edge Glow Width", Range(0.01, 0.3)) = 0.08

        _NoiseScale ("Noise Scale", Float) = 18.0
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.35

        _FlowSpeed ("Flow Speed", Float) = 1.5
        _FlowStripeScale ("Flow Stripe Scale", Float) = 10.0
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.45

        _ChargingFlowSpeedMultiplier("Charging Flow Speed Multiplier", Range(1, 6)) = 2.5
        _ChargingFlowStrengthMultiplier("Charging Flow Strength Multiplier", Range(1, 3)) = 1.35
        _ChargingEdgeGlowMultiplier("Charging Edge Glow Multiplier", Range(1, 4)) = 1.6
        _ChargingBrightnessBoost("Charging Brightness Boost", Range(0, 2)) = 0.35

        _EdgeGlowStrength ("Edge Glow Strength", Range(0, 3)) = 1.4
        _LockFlashIntensity ("Lock Flash Intensity", Range(0, 5)) = 2.5

        _WarningLength ("Warning Length", Float) = 1.0
        _WarningWidth ("Warning Width", Float) = 1.0

        _PatternReferenceLength ("Pattern Reference Length", Float) = 12.0
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
                float _LockSweepProgress;
                float4 _LockSweepColor;
                float _LockSweepWidth;
                float _LockSweepSoftness;
                float _LockSweepIntensity;

                float _EdgeSoftness;
                float _EndSoftness;

                float _CoreWidthRatio;
                float _EdgeNoiseScale;
                float _EdgeNoiseAmount;
                float _EdgeWobbleSpeed;
                float _EdgeGlowWidth;

                float _NoiseScale;
                float _NoiseStrength;

                float _FlowSpeed;
                float _FlowStripeScale;
                float _FlowStrength;

                float _ChargingFlowSpeedMultiplier;
                float _ChargingFlowStrengthMultiplier;
                float _ChargingEdgeGlowMultiplier;
                float _ChargingBrightnessBoost;

                float _EdgeGlowStrength;
                float _LockFlashIntensity;

                float _WarningLength;
                float _WarningWidth;

                float _PatternReferenceLength;
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
            
                // 幅方向の中央が0、上下端が1。
                float across =
                    abs(uv.y - 0.5) * 2.0;
            
                // 前後端を少しフェードさせる。
                float startFade = smoothstep(
                    0.0,
                    max(0.0001, _EndSoftness),
                    uv.x);
            
                float endFade =
                    1.0 - smoothstep(
                        1.0 - _EndSoftness,
                        1.0,
                        uv.x);
            
                float lengthFade =
                    startFade * endFade;
            
                float time = _Time.y;
            
                float lockedState =
                    saturate(
                        1.0 - abs(_WarningState - 1.0));
            
                float chargingState =
                    saturate(_WarningState - 1.0);

                // uv.xは敵側0 → 帯の先端1。
                // Locked中に1本の光を始点から終点まで移動させる。
                float sweepCenter =
                    smoothstep(
                        0.0,
                        1.0,
                        saturate(_LockSweepProgress));
                
                float sweepDistance =
                    abs(uv.x - sweepCenter);
                
                float sweepMask =
                    1.0 - smoothstep(
                        _LockSweepWidth,
                        _LockSweepWidth
                            + max(0.0001, _LockSweepSoftness),
                        sweepDistance);
                
                sweepMask *= lockedState;
                sweepMask *= lengthFade;
            
                // 帯の長さに応じて模様を繰り返す。
                float referenceLength =
                    max(_PatternReferenceLength, 0.001);
            
                float patternRepeat =
                    max(
                        _WarningLength / referenceLength,
                        1.0);
            
                float patternX =
                    uv.x * patternRepeat;
            
                // 上端と下端で別のノイズを使う。
                float edgeSide =
                    step(0.5, uv.y);
            
                float edgeNoise =
                    ValueNoise(float2(
                        patternX * _EdgeNoiseScale
                            - time * _EdgeWobbleSpeed,
                        edgeSide * 17.13));
            
                float edgeOffset =
                    (edgeNoise - 0.5)
                    * 2.0
                    * _EdgeNoiseAmount;
            
                // 0付近がノイズで変形した実際の輪郭。
                float noisyEdgeDistance =
                    (1.0 - across) + edgeOffset;
            
                float noisyEdgeMask =
                    smoothstep(
                        0.0,
                        max(0.0001, _EdgeSoftness),
                        noisyEdgeDistance);
            
                // 中央部分はノイズに関係なく必ず表示する。
                // 当たり判定幅の読みやすさを維持するためのコア。
                float coreFadeEnd =
                    min(
                        _CoreWidthRatio + 0.04,
                        0.999);
            
                float coreMask =
                    1.0 - smoothstep(
                        _CoreWidthRatio,
                        coreFadeEnd,
                        across);
            
                float shapeMask =
                    max(
                        coreMask,
                        noisyEdgeMask);
            
                // ノイズ輪郭に沿った発光部分。
                float edgeGlow =
                    1.0 - smoothstep(
                        0.0,
                        max(0.001, _EdgeGlowWidth),
                        abs(noisyEdgeDistance));
            
                edgeGlow *= lengthFade;
            
                // Charge中だけ内部模様の速度を上げる。
                float activeFlowSpeed =
                    _FlowSpeed
                    * lerp(
                        1.0,
                        _ChargingFlowSpeedMultiplier,
                        chargingState);
            
                float noise =
                    ValueNoise(float2(
                        patternX * _NoiseScale
                            - time * activeFlowSpeed,
                        uv.y * _NoiseScale * 0.45));
            
                float flowRaw =
                    sin(
                        (
                            patternX * _FlowStripeScale
                            - time * activeFlowSpeed
                        )
                        * 6.28318
                        + noise * 2.0);
            
                float flow =
                    smoothstep(
                        0.15,
                        0.85,
                        flowRaw * 0.5 + 0.5);
            
                float activeFlowStrength =
                    _FlowStrength
                    * lerp(
                        1.0,
                        _ChargingFlowStrengthMultiplier,
                        chargingState);
            
                float activeEdgeGlowStrength =
                    _EdgeGlowStrength
                    * lerp(
                        1.0,
                        _ChargingEdgeGlowMultiplier,
                        chargingState);
            
                float lockFlash =
                    saturate(_LockFlash)
                    * _LockFlashIntensity;
            
                float brightness =
                    0.85
                    + _WarningPulse * 0.25
                    + lockedState * 0.25
                    + chargingState
                        * _ChargingBrightnessBoost
                    + lockFlash;
            
                float3 color =
                    _BaseColor.rgb;
            
                color = lerp(
                    color,
                    _EdgeColor.rgb,
                    saturate(
                        edgeGlow
                        * activeEdgeGlowStrength));
            
                color +=
                    _FlowColor.rgb
                    * flow
                    * activeFlowStrength
                    * 0.8;

                color +=
                _LockSweepColor.rgb
                * sweepMask
                * _LockSweepIntensity;
            
                // 方向確定時は帯全体へ明色を加える。
                color +=
                    _FlowColor.rgb
                    * lockFlash
                    * 0.35;
            
                color *= brightness;
            
                float baseMask =
                    shapeMask * lengthFade;
            
                float noiseAlpha =
                    lerp(
                        1.0,
                        noise,
                        saturate(_NoiseStrength));
            
                // 内部ノイズで透明になりすぎないようにする。
                float stableNoiseAlpha =
                    lerp(
                        0.85,
                        1.0,
                        noiseAlpha);
            
                float alpha =
                    _WarningAlpha
                    * baseMask
                    * stableNoiseAlpha;
            
                alpha +=
                    flow
                    * activeFlowStrength
                    * 0.12
                    * baseMask;
            
                alpha += edgeGlow * 0.22;
                alpha += lockFlash * 0.20;

                alpha +=
                    sweepMask
                    * _LockSweepColor.a
                    * 0.25;
            
                alpha = saturate(alpha);
            
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}