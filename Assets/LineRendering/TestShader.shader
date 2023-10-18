Shader "HDRP/TestShader"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0.5
        _Smoothness("Smoothness", Range(0,1)) = 0.5
    } 
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        } Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD0;
            };
            
            half _Smoothness;
            half _Metallic;
            fixed4 _Color;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            


            float4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                float diffuse = max(0, dot(i.worldNormal, lightDir));
                float NdotV = dot(i.worldNormal, viewDir);
                float3 H = normalize(viewDir + lightDir);
                float roughness = 1 - _Smoothness;
                float3 F0 = lerp(_Color.rgb, float3(0.04, 0.04, 0.04), _Metallic);
                float3 specular = F0 + (1 - F0) * pow(max(0, dot(i.worldNormal, H)), 5 * roughness);
                float4 c = float4(_Color.rgb * diffuse + specular, _Color.a);
                return c;
            }
            ENDCG
        }
    } 
    FallBack "Diffuse"
}