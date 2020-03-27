Shader "Hidden/ShadowVSM/Depth" {
    Properties
    {
    }

    SubShader {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 world_normal : TEXCOORD0;
            };

            float4x4 _LPV_WorldToLightLocalMatrix;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world_normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float depth = i.vertex.z;
#ifdef UNITY_REVERSED_Z
                depth = 0.5 - depth;
#else
                depth = depth - 0.5;
#endif
                return float4(mul((float3x3)_LPV_WorldToLightLocalMatrix, i.world_normal), depth);
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}