
#define SH_F2I  1024.0
#define SH_I2F  (1.0 / 1024.0)

#define SH_C0 0.282094792   // 1 / 2sqrt(pi)
#define SH_C1 0.488602512   // sqrt(3/pi) / 2
#define SH_cosLobe_C0 0.886226925  // sqrt(pi)/2
#define SH_cosLobe_C1 1.02332671   // sqrt(pi/3)

float4 dirToSH(float3 dir)
{
    return float4(SH_C0, -SH_C1 * dir.y, SH_C1 * dir.z, -SH_C1 * dir.x);
}

float4 dirToCosineLobe(float3 dir)
{
    return float4(SH_cosLobe_C0, -SH_cosLobe_C1 * dir.y, SH_cosLobe_C1 * dir.z, -SH_cosLobe_C1 * dir.x);
}
