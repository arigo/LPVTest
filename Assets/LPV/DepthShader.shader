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
                /* if !UNITY_REVERSED_Z: 0 = near clip plane (at -127 * half_size)
                                         1 = far clip plane (at half_size)
                   Must turn it into -0.5 at -half_size, 0.5 at half_size. */
#if !defined(UNITY_REVERSED_Z)
                depth = depth * 64 - 63.5;
#else
                /* same, but starts with 1 at -127 * half_size and 0 at half_size */
                depth = 0.5 - depth * 64;
#endif
                f2a OUT;
                OUT.geometry = float4(i.world_normal, depth);
                OUT.color = _Color;
                return OUT;
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}