Shader "LPVTest/StandardOpaque"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model.
        #pragma surface surf Standard
        #pragma target 5.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)






        #define SH_C0 0.282094792   // 1 / 2sqrt(pi)
        #define SH_C1 0.488602512   // sqrt(3/pi) / 2

        float4 dirToSH(float3 dir)
        {
            return float4(SH_C0, -SH_C1 * dir.y, SH_C1 * dir.z, -SH_C1 * dir.x);
        }

        sampler3D _LPV_r_accum;
        float4x4 _LPV_WorldToLightLocalMatrix;



        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;


            float3 sample_pos = IN.worldPos;
            sample_pos += o.Normal * 0.6;
            float3 lpv_pos = mul(_LPV_WorldToLightLocalMatrix, float4(sample_pos, 1));
            float4 sh_cell = tex3D(_LPV_r_accum, lpv_pos);

            float s = dot(sh_cell, dirToSH(o.Normal));
            o.Emission = max(float3(0, 0, 0), float3(s, s, s));
            //o.Emission = lpv_pos;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
