
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

#define SH_SHIFT  (127.0 / 255.0)

float4 packSH(float4 sh)
{
    /* scale/shift the full-precision value 'sh' into four values between 0 and 1,
     * meant to be written to a ARGB32 texture.  We try to preserve the value 0:
     * simply returning "sh * 0.5 + 0.5" would map 0 to 0.5, and 0.5 can't be
     * represented exactly---it corresponds to the byte value 127.5. */
    return sh * 0.5 + SH_SHIFT;
}

float4 unpackSH(float4 sh)
{
    return (sh - SH_SHIFT) * 2.0;
}
