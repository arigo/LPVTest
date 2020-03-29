Shader "Hidden/LPVTest/GVShader" {
    /*
       Geometry Volume shader
       ======================


     */
    Properties
    {
    }

    SubShader {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            ZTest off
            ZWrite off
            Cull off
            ColorMask 0

            CGPROGRAM
            #pragma target 5.0
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile   _ ORIENTATION_2 ORIENTATION_3

            #include "SHCommon.cginc"

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

            RWStructuredBuffer<int> _RSM_gv : register(u1);
            uint _LPV_GridResolution;
            float4x4 _LPV_WorldToLightLocalMatrix;


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world_normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 xyz = i.vertex.xyz;
#if defined(UNITY_REVERSED_Z)
                xyz.z = 1 - xyz.z;
#endif
                xyz.z *= _LPV_GridResolution * 0.99999;   /* makes sure 0 <= pos.z < GridResolution */

                int3 pos = int3(xyz);

                float3 normal = i.world_normal;
                normal = normalize(mul((float3x3)_LPV_WorldToLightLocalMatrix, normal));
                float reduce = normal.z;

                /* Handle the case of two-faced geometry: this produces SH that are nice cosine
                   lobes pointing in the normal direction on one side---namely, the side facing
                   away from the sun. */
                normal *= sign(reduce);

#ifdef ORIENTATION_2
                pos = pos.yzx;
                reduce = normal.y;
#endif
#ifdef ORIENTATION_3
                pos = pos.zxy;
                reduce = normal.x;
#endif
                int4 shNormalScaled = int4(dirToCosineLobe(normal) * SH_F2I * (reduce * reduce));

                int index = pos.x + _LPV_GridResolution * (pos.y + _LPV_GridResolution * pos.z);
                index *= 4; InterlockedAdd(_RSM_gv[index], shNormalScaled.x);
                index += 1; InterlockedAdd(_RSM_gv[index], shNormalScaled.y);
                index += 1; InterlockedAdd(_RSM_gv[index], shNormalScaled.z);
                index += 1; InterlockedAdd(_RSM_gv[index], shNormalScaled.w);

                /* dummy result, ignored */
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}