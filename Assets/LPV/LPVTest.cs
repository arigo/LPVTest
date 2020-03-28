using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class LPVTest : MonoBehaviour
{
    public int lpvGridResolution;
    public float lpvGridCellSize;
    public ComputeShader lpvShader;
    public Color skyColor;

    public int propagateSteps;
    public bool drawGizmosR, drawGizmosG, drawGizmosB;


    struct ShRGB
    {
        internal RenderTexture r, g, b;
    }
    ShRGB _lpvTex3D, _lpvTex3D_prev, _lpvTex3D_accum;


    static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    void DestroyTarget(ref RenderTexture tex)
    {
        if (tex)
            DestroyImmediate(tex);
        tex = null;
    }

    void DestroyTarget(ref ShRGB sh)
    {
        DestroyTarget(ref sh.r);
        DestroyTarget(ref sh.g);
        DestroyTarget(ref sh.b);
    }

    void DestroyTargets()
    {
        DestroyTarget(ref _lpvTex3D);
        DestroyTarget(ref _lpvTex3D_prev);
        DestroyTarget(ref _lpvTex3D_accum);
    }

    RenderTexture CreateTarget()
    {
        /* depth, and four components as described in FetchShadowCamera() */
        var desc = new RenderTextureDescriptor(lpvGridResolution, lpvGridResolution, RenderTextureFormat.ARGBFloat);
        desc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        desc.volumeDepth = lpvGridResolution;
        desc.enableRandomWrite = true;
        desc.useMipMap = false;
        desc.autoGenerateMips = false;

        RenderTexture tg = new RenderTexture(desc);
        tg.wrapMode = TextureWrapMode.Clamp;
        tg.filterMode = FilterMode.Bilinear;
        tg.Create();
        return tg;
    }

    ShRGB CreateShRGB()
    {
        return new ShRGB
        {
            r = CreateTarget(),
            g = CreateTarget(),
            b = CreateTarget(),
        };
    }

    void SetShRGBTextures(int kernel, string suffix, ShRGB sh)
    {
        lpvShader.SetTexture(kernel, "LPV_r" + suffix, sh.r);
        lpvShader.SetTexture(kernel, "LPV_g" + suffix, sh.g);
        lpvShader.SetTexture(kernel, "LPV_b" + suffix, sh.b);
    }

    private void Update()
    {
        if (!GetComponent<RSMTest>().UpdateShadowsFull(out RenderTexture shadow_texture,
                                                       out RenderTexture shadow_texture_color,
                                                       out Matrix4x4 world_to_light_local_matrix))
            return;

        if (_lpvTex3D.r != null && _lpvTex3D.r.width != lpvGridResolution)
            DestroyTargets();

        if (_lpvTex3D.r == null)
        {
            if (lpvGridResolution == 0)
                return;
            _lpvTex3D_prev = CreateShRGB();
            _lpvTex3D_accum = CreateShRGB();
            _lpvTex3D = CreateShRGB();
        }

        lpvShader.SetInt("GridResolution", lpvGridResolution);
        lpvShader.SetMatrix("WorldToLightLocalMatrix", world_to_light_local_matrix);

        int clear_kernel = lpvShader.FindKernel("ClearKernel");
        SetShRGBTextures(clear_kernel, "", _lpvTex3D);
        SetShRGBTextures(clear_kernel, "_accum", _lpvTex3D_accum);
        int thread_groups = (lpvGridResolution + 3) / 4;
        lpvShader.Dispatch(clear_kernel, thread_groups, thread_groups, thread_groups);

        GetInitialBorderColor(out var border_sh_r, out var border_sh_g, out var border_sh_b);
        int border_kernel = lpvShader.FindKernel("BorderKernel");
        SetShRGBTextures(border_kernel, "", _lpvTex3D);
        //SetShRGBTextures(border_kernel, "_accum", _lpvTex3D_accum);
        lpvShader.SetVector("LPV_sh_r", border_sh_r);
        lpvShader.SetVector("LPV_sh_g", border_sh_g);
        lpvShader.SetVector("LPV_sh_b", border_sh_b);
        thread_groups = (lpvGridResolution + 7) / 8;
        lpvShader.Dispatch(border_kernel, thread_groups, thread_groups, 1);

        int inject_kernel = lpvShader.FindKernel("InjectKernel");
        SetShRGBTextures(inject_kernel, "", _lpvTex3D);
        //SetShRGBTextures(inject_kernel, "_accum", _lpvTex3D_accum);
        lpvShader.SetTexture(inject_kernel, "ShadowTexture", shadow_texture);
        lpvShader.SetTexture(inject_kernel, "ShadowTextureColor", shadow_texture_color);
        thread_groups = (lpvGridResolution + 7) / 8;
        lpvShader.Dispatch(inject_kernel, thread_groups, thread_groups, 1);

        int propagate_step_kernel = lpvShader.FindKernel("PropagateStepKernel");
        SetShRGBTextures(propagate_step_kernel, "_accum", _lpvTex3D_accum);
        thread_groups = (lpvGridResolution + 3) / 4;
        for (int i = 0; i < propagateSteps; i++)
        {
            Swap(ref _lpvTex3D_prev, ref _lpvTex3D);
            SetShRGBTextures(propagate_step_kernel, "_prev", _lpvTex3D_prev);
            SetShRGBTextures(propagate_step_kernel, "", _lpvTex3D);
            lpvShader.Dispatch(propagate_step_kernel, thread_groups, thread_groups, thread_groups);
        }

        int clear_border_kernel = lpvShader.FindKernel("ClearBorderKernel");
        SetShRGBTextures(clear_border_kernel, "_accum", _lpvTex3D_accum);
        thread_groups = (lpvGridResolution + 7) / 8;
        lpvShader.Dispatch(clear_border_kernel, thread_groups, thread_groups, thread_groups);

        Shader.SetGlobalTexture("_LPV_r_accum", _lpvTex3D_accum.r);
        Shader.SetGlobalTexture("_LPV_g_accum", _lpvTex3D_accum.g);
        Shader.SetGlobalTexture("_LPV_b_accum", _lpvTex3D_accum.b);
        Shader.SetGlobalFloat("_LPV_GridCellSize", lpvGridCellSize);
        Shader.SetGlobalMatrix("_LPV_WorldToLightLocalMatrix", world_to_light_local_matrix);
    }

    void GetInitialBorderColor(out Vector4 sh_r, out Vector4 sh_g, out Vector4 sh_b)
    {
        Vector3 dir = GetComponent<RSMTest>().directionalLight.transform.InverseTransformDirection(Vector3.down);
        const float SH_cosLobe_C0 = 0.886226925f;
        const float SH_cosLobe_C1 = 1.02332671f;
        Vector4 cosineLobe = new Vector4(SH_cosLobe_C0, -SH_cosLobe_C1 * dir.y, SH_cosLobe_C1 * dir.z, -SH_cosLobe_C1 * dir.x);

        sh_r = cosineLobe * skyColor.r;
        sh_g = cosineLobe * skyColor.g;
        sh_b = cosineLobe * skyColor.b;
    }


#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_lpvTex3D_accum.r == null)
            return;

        Vector4[] array = null;
        if (drawGizmosR) DrawGizmos1(ref array, _lpvTex3D_accum.r, 1, 0, 0);
        if (drawGizmosG) DrawGizmos1(ref array, _lpvTex3D_accum.g, 0, 1, 0);
        if (drawGizmosB) DrawGizmos1(ref array, _lpvTex3D_accum.b, 0, 0, 1);
    }

    void DrawGizmos1(ref Vector4[] array, RenderTexture rt, int cr, int cg, int cb)
    {
        int extract_kernel = lpvShader.FindKernel("ExtractKernel");
        var buffer = new ComputeBuffer(lpvGridResolution * lpvGridResolution * lpvGridResolution, 4 * 4, ComputeBufferType.Default);
        lpvShader.SetBuffer(extract_kernel, "ExtractTexture", buffer);
        lpvShader.SetTexture(extract_kernel, "LPV_r", rt);
        int thread_groups = (lpvGridResolution + 3) / 4;
        lpvShader.Dispatch(extract_kernel, thread_groups, thread_groups, thread_groups);

        if (array == null)
            array = new Vector4[lpvGridResolution * lpvGridResolution * lpvGridResolution];
        buffer.GetData(array);
        buffer.Release();

        var rsm = GetComponent<RSMTest>();
        var tr = rsm.directionalLight.transform;
        float half_size = 0.5f * rsm.gridResolution * rsm.gridPixelSize;
        float pixel_size = rsm.gridPixelSize;
        Vector3 org = tr.position - half_size * (tr.right + tr.up + tr.forward);
        Vector3 pix_x = tr.right * pixel_size;
        Vector3 pix_y = tr.up * pixel_size;
        Vector3 pix_z = tr.forward * pixel_size;

        Gizmos.color = Color.white;
        Gizmos.DrawLine(org, org + pix_x * lpvGridResolution);
        Gizmos.DrawLine(org, org + pix_y * lpvGridResolution);
        Gizmos.DrawLine(org, org + pix_z * lpvGridResolution);

        Color base_color = new Color(0.5f * cr, 0.5f * cg, 0.5f * cb);

        int index = 0;
        for (int z = 0; z < lpvGridResolution; z++)
            for (int y = 0; y < lpvGridResolution; y++)
                for (int x = 0; x < lpvGridResolution; x++)
                {
                    Vector4 entry = array[index++];
                    if (entry != Vector4.zero)
                    {
                        Vector3 center = org + pix_x * (x + .5f) + pix_y * (y + .5f) + pix_z * (z + .5f);
                        Vector4 shBase = new Vector4(0.2821f, -0.4886f, 0.4886f, -0.4886f);
                        for (int k = 0; k < 30; k++)
                        {
                            Vector3 dir = Random.onUnitSphere;

                            Vector4 shNormal = shBase;
                            shNormal.y *= dir.y;
                            shNormal.z *= dir.z;
                            shNormal.w *= dir.x;
                            float len = Vector4.Dot(entry, shNormal);
                            Gizmos.color = len >= 0 ? base_color : (new Color(0.5f, 0.5f, 0.5f) - base_color);
                            Vector3 dir1 = pix_x * dir.x + pix_y * dir.y + pix_z * dir.z;
                            Gizmos.DrawLine(center, center + len * dir1);
                        }
                    }
                }
    }
#endif
}
