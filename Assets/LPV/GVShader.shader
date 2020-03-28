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
            //#pragma multi_compile   _ ORIENTATION_YZ ORIENTATION_ZX

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

            RWStructuredBuffer<int> _LPV_gv : register(u1);
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
                xyz.z *= _LPV_GridResolution;

                int3 pos = int3(xyz);

                float3 normal = i.world_normal;
                normal = normalize(mul((float3x3)_LPV_WorldToLightLocalMatrix, normal));
                int4 shNormalScaled = int4(dirToCosineLobe(normal) * SH_F2I * (normal.z * normal.z));

                int index = pos.x + _LPV_GridResolution * (pos.y + _LPV_GridResolution * pos.z);
                index *= 4; InterlockedAdd(_LPV_gv[index], shNormalScaled.x);
                index += 1; InterlockedAdd(_LPV_gv[index], shNormalScaled.y);
                index += 1; InterlockedAdd(_LPV_gv[index], shNormalScaled.z);
                index += 1; InterlockedAdd(_LPV_gv[index], shNormalScaled.w);

                /* dummy result, ignored */
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}