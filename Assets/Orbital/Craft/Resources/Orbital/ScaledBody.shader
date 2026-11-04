// A sphere of the flight view's sky (stage 3.6): a Lambert ball lit from its own _SunDir (the direction from THIS body to the Sun, so phases are
// right for a body far from the eye, which one directional light cannot do), or a self-luminous disk. Depth state comes from the material: scaled
// impostors are drawn far to near without depth (painter's order by the real distance), the near planet of the Local camera uses normal depth.
Shader "Orbital/ScaledBody"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission (0 = lit)", Float) = 0
        _SunDir ("Direction to the Sun", Vector) = (0, 1, 0, 0)
        _ZWrite ("ZWrite", Float) = 0
        _ZTest ("ZTest", Float) = 8
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Transparent" }
        Pass
        {
            Name "Body"
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Emission;
                float4 _SunDir;
            CBUFFER_END

            // set once for all bodies from FlightLightingSettings (SunLightRig)
            float _OrbitalBodyDay;
            float _OrbitalBodyNight;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                if (_Emission > 0.0) return float4(_Tint.rgb * _Emission, 1.0);
                float light = saturate(dot(normalize(input.normalWS), _SunDir.xyz));
                return float4(_Tint.rgb * (_OrbitalBodyNight + light * _OrbitalBodyDay), 1.0);
            }
            ENDHLSL
        }
    }
}
