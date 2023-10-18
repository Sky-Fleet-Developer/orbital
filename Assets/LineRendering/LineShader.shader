Shader "Custom/LineShader" {
    Properties {
        _Color ("Line Color", Color) = (1, 1, 1, 1)
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
            };
            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            StructuredBuffer<float2> _Positions;
            StructuredBuffer<float4x4> _Matrices;
            uniform uint _BaseVertexIndex;
            float4 _Color;
            
            v2f vert(uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float2 pos = _Positions[vertexID + _BaseVertexIndex];
                float4 wpos = mul(_Matrices[instanceID], float4(pos.x, 0, pos.y, 1.0f));
                o.vertex = mul(UNITY_MATRIX_VP, wpos);
                o.color = _Color;
                return o;
            }

            half4 frag (v2f i) : SV_Target {
                return i.color;
            }
            ENDCG
        }
    }
}