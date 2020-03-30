
sampler3D _LPV_accum;
float4x4 _LPV_WorldToLightLocalMatrix;
float _LPV_GridCellSize;


#define LPVOcclusion  0.8

float3 SampleLPVIndirectLight(float3 sample_pos, float3 world_normal)
{
    const float LPV_SH_C0 = 0.282094792;   // 1 / 2sqrt(pi)
    const float LPV_SH_C1 = 0.488602512;   // sqrt(3/pi) / 2

    sample_pos += world_normal * _LPV_GridCellSize;
    float3 lpv_pos = mul(_LPV_WorldToLightLocalMatrix, float4(sample_pos, 1));
    return tex3D(_LPV_accum, lpv_pos).rgb;
}
