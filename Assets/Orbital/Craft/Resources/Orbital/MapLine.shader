// A line of the flight map (stage 4): a quad per segment widened in screen space, so its width is in pixels at any scale. A vertex holds its own end, the other
// end in TEXCOORD1 and in TEXCOORD0 the side (x) and which end it is (y). An end behind the camera is moved along the segment to just before the camera.
Shader "Orbital/MapLine"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _WidthPx ("Width, pixels", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "Line"
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _WidthPx;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 side : TEXCOORD0;
                float3 other : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float side : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                const float front = 0.01;
                float4 a = TransformWorldToHClip(input.positionOS);
                float4 b = TransformWorldToHClip(input.other);
                if (a.w < front) a = lerp(a, b, (front - a.w) / (b.w - a.w));
                if (b.w < front) b = lerp(b, a, (front - b.w) / (a.w - b.w));
                float2 screenA = a.xy / a.w * _ScreenParams.xy;
                float2 screenB = b.xy / b.w * _ScreenParams.xy;
                float2 along = (screenB - screenA) * input.side.y;
                float len = length(along);
                along = len > 1e-4 ? along / len : float2(1.0, 0.0);
                float2 across = float2(-along.y, along.x);
                float half_ = _WidthPx * 0.5 + 1.0; // one pixel more for the soft edge
                Varyings output;
                output.positionCS = a;
                output.positionCS.xy += across * input.side.x * half_ * 2.0 / _ScreenParams.xy * a.w;
                output.side = input.side.x * half_;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float half_ = _WidthPx * 0.5 + 1.0;
                float edge = saturate(half_ - abs(input.side));
                return float4(_Color.rgb, _Color.a * edge);
            }
            ENDHLSL
        }
    }
}
