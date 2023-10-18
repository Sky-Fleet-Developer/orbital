Shader "Custom/CircleShader"
{
    Properties
    {
        _Radius ("Radius", Range(0.1, 10.0)) = 1.0
        _NodeCount ("Node Count", Range(4, 64)) = 16
        _TessellationUniform ("Tessellation Uniform", Range(1, 64)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma hull hull
            #pragma domain custom_domain
            #pragma target 4.6

            #include "UnityCG.cginc"
            #include "CustomTessellation.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : POSITION;
            };

            float _Radius;
            int _NodeCount;

            vertexOutput tessVertTransformed(vertexInput v)
            {
                vertexOutput o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = v.normal;
                o.tangent = v.tangent;
                return o;
            }

            [UNITY_domain("tri")]
            vertexOutput custom_domain(TessellationFactors factors, OutputPatch<vertexInput, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                vertexInput v;

                #define MY_DOMAIN_PROGRAM_INTERPOLATE(fieldName) v.fieldName = \
					patch[0].fieldName * barycentricCoordinates.x + \
					patch[1].fieldName * barycentricCoordinates.y + \
					patch[2].fieldName * barycentricCoordinates.z;

                MY_DOMAIN_PROGRAM_INTERPOLATE(vertex)
                MY_DOMAIN_PROGRAM_INTERPOLATE(normal)
                MY_DOMAIN_PROGRAM_INTERPOLATE(tangent)

                return tessVertTransformed(v);
            }

            half4 frag(v2f i) : SV_Target
            {
                /*// Calculate the distance from the camera
                float distToCamera = length(_WorldSpaceCameraPos - i.pos.xyz);
 
                // Calculate the number of nodes based on distance
                int nodes = (int)lerp(4, _NodeCount, saturate(distToCamera / _Radius));
 
                // Calculate the angle between nodes
                float angle = 6.283185 / nodes;
 
                // Calculate the current angle
                float currentAngle = atan2(i.pos.y, i.pos.x);
 
                // Calculate the radius based on distance
                float dynamicRadius = _Radius * saturate(distToCamera / _Radius);
 
                // Adjust the radius to avoid sharp corners
                float x = dynamicRadius * cos(currentAngle);
                float y = dynamicRadius * sin(currentAngle);
 
                return step(length(i.pos.xy - float2(x, y)), 0.01);*/
                return half4(.7, .3, .3, 1);
            }
            ENDCG
        }
    }
}