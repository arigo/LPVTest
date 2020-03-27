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

            struct f2a
            {
                float4 geometry : COLOR0;
                float4 color : COLOR1;
            };

            float4x4 _LPV_WorldToLightLocalMatrix;
            float4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world_normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            f2a frag(v2f i)
            {
                float depth = i.vertex.z;
#ifdef UNITY_REVERSED_Z
                depth = 0.5 - depth;
#else
                depth = depth - 0.5;
#endif
                f2a OUT;
                OUT.geometry = float4(mul((float3x3)_LPV_WorldToLightLocalMatrix, i.world_normal), depth);
                OUT.color = _Color;
                return OUT;
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}