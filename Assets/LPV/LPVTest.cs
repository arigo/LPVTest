using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class LPVTest : MonoBehaviour
{
    public int lpvGridResolution;
    public float lpvGridCellSize;
    public ComputeShader lpvCompute;
#if false
    public Color skyColor;
#endif

    public int propagateSteps;
    public bool drawGizmos, drawGizmosGV;


    struct ShRGB
    {
        internal RenderTexture r, g, b;
        internal void Create() { r.Create(); g.Create(); b.Create(); }
        internal void Release() { r.Release(); g.Release(); b.Release(); }
    }
    ShRGB _lpvTex3D, _lpvTex3D_prev;
    RenderTexture _tex3d_accum, _tex3d_gv;


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
        DestroyTarget(ref _tex3d_accum);
        DestroyTarget(ref _tex3d_gv);
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

    RenderTexture CreateTex3dGV()
    {
        var desc = new RenderTextureDescriptor(lpvGridResolution, lpvGridResolution, RenderTextureFormat.ARGB32);
        desc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        desc.volumeDepth = lpvGridResolution;
        desc.enableRandomWrite = true;
        desc.useMipMap = false;
        desc.autoGenerateMips = false;

        RenderTexture tg = new RenderTexture(desc);
        tg.wrapMode = TextureWrapMode.Clamp;
        tg.filterMode = FilterMode.Point;
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
        lpvCompute.SetTexture(kernel, "LPV_r" + suffix, sh.r);
        lpvCompute.SetTexture(kernel, "LPV_g" + suffix, sh.g);
        lpvCompute.SetTexture(kernel, "LPV_b" + suffix, sh.b);
    }

    private void Update()
    {
        if (_lpvTex3D.r != null && _lpvTex3D.r.width != lpvGridResolution)
            DestroyTargets();

        if (_lpvTex3D.r == null)
        {
            if (lpvGridResolution == 0)
                return;
            _tex3d_accum = CreateTarget();
            _tex3d_gv = CreateTex3dGV();
            _lpvTex3D_prev = CreateShRGB();
            _lpvTex3D = CreateShRGB();
        }
        _tex3d_gv.Create();

        if (!GetComponent<RSMTest>().UpdateShadowsFull(out RenderTexture shadow_texture,
                                               out RenderTexture shadow_texture_color,
                                               out Matrix4x4 world_to_light_local_matrix,
                                               _tex3d_gv))
            return;

        _lpvTex3D.Create();
        _tex3d_accum.Create();

        lpvCompute.SetInt("GridResolution", lpvGridResolution);
        lpvCompute.SetMatrix("WorldToLightLocalMatrix", world_to_light_local_matrix);

        int clear_kernel = lpvCompute.FindKernel("ClearKernel");
        SetShRGBTextures(clear_kernel, "", _lpvTex3D);
        lpvCompute.SetTexture(clear_kernel, "LPV_accum", _tex3d_accum);
        int thread_groups = (lpvGridResolution + 3) / 4;
        lpvCompute.Dispatch(clear_kernel, thread_groups, thread_groups, thread_groups);

#if false
        GetInitialBorderColor(out var border_sh_r, out var border_sh_g, out var border_sh_b);
        int border_kernel = lpvCompute.FindKernel("BorderKernel");
        SetShRGBTextures(border_kernel, "", _lpvTex3D);
        //SetShRGBTextures(border_kernel, "_accum", _lpvTex3D_accum);
        lpvCompute.SetVector("LPV_sh_r", border_sh_r);
        lpvCompute.SetVector("LPV_sh_g", border_sh_g);
        lpvCompute.SetVector("LPV_sh_b", border_sh_b);
        thread_groups = (lpvGridResolution + 7) / 8;
        lpvCompute.Dispatch(border_kernel, thread_groups, thread_groups, 1);
#endif

        int inject_kernel = lpvCompute.FindKernel("InjectKernel");
        SetShRGBTextures(inject_kernel, "", _lpvTex3D);
        lpvCompute.SetTexture(inject_kernel, "ShadowTexture", shadow_texture);
        lpvCompute.SetTexture(inject_kernel, "ShadowTextureColor", shadow_texture_color);
        thread_groups = (lpvGridResolution + 7) / 8;
        lpvCompute.Dispatch(inject_kernel, thread_groups, thread_groups, 1);

        shadow_texture.Release();
        shadow_texture_color.Release();

        int propagate_step_kernel = lpvCompute.FindKernel("PropagateStepKernel");
        lpvCompute.SetTexture(propagate_step_kernel, "LPV_accum", _tex3d_accum);
        lpvCompute.SetTexture(propagate_step_kernel, "LPV_gv", _tex3d_gv);
        thread_groups = (lpvGridResolution + 3) / 4;
        _lpvTex3D_prev.Create();
        for (int i = 0; i < propagateSteps; i++)
        {
            Swap(ref _lpvTex3D_prev, ref _lpvTex3D);
            SetShRGBTextures(propagate_step_kernel, "_prev", _lpvTex3D_prev);
            SetShRGBTextures(propagate_step_kernel, "", _lpvTex3D);
            lpvCompute.SetInt("PropagationStep", i);
            lpvCompute.Dispatch(propagate_step_kernel, thread_groups, thread_groups, thread_groups);
        }
        _lpvTex3D_prev.Release();
        _lpvTex3D.Release();

        int clear_border_kernel = lpvCompute.FindKernel("ClearBorderKernel");
        lpvCompute.SetTexture(clear_border_kernel, "LPV_accum", _tex3d_accum);
        thread_groups = (lpvGridResolution + 7) / 8;
        lpvCompute.Dispatch(clear_border_kernel, thread_groups, thread_groups, thread_groups);

        Shader.SetGlobalTexture("_LPV_accum", _tex3d_accum);
        Shader.SetGlobalFloat("_LPV_GridCellSize", lpvGridCellSize);

        if (!drawGizmosGV)
            _tex3d_gv.Release();
    }

#if false
    /* Disabled, because it doesn't really work if the resolution of the grid is increased */
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
#endif


#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector4[] array = null;
        if (drawGizmos) DrawGizmosAccum(ref array, _tex3d_accum);
        if (drawGizmosGV) DrawGizmosGV(ref array, _tex3d_gv);
    }

    void DrawGizmosExtract(ref Vector4[] array, RenderTexture rt, int mode)
    {
        int debug_kernel = lpvCompute.FindKernel("DebugKernel");
        lpvCompute.SetTexture(debug_kernel, "ExtractSource", rt);

        var buffer = new ComputeBuffer(lpvGridResolution * lpvGridResolution * lpvGridResolution, 4 * 4, ComputeBufferType.Default);
        lpvCompute.SetBuffer(debug_kernel, "ExtractTexture", buffer);
        lpvCompute.SetInt("GridResolution", lpvGridResolution);
        lpvCompute.SetInt("ExtractMode", mode);
        int thread_groups = (lpvGridResolution + 3) / 4;
        lpvCompute.Dispatch(debug_kernel, thread_groups, thread_groups, thread_groups);

        if (array == null)
            array = new Vector4[lpvGridResolution * lpvGridResolution * lpvGridResolution];
        buffer.GetData(array);
        buffer.Release();
    }

    void DrawGizmosAccum(ref Vector4[] array, RenderTexture rt)
    {
        if (rt == null)
            return;
        DrawGizmosExtract(ref array, rt, 0);

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

        float dd = 0.5f;

        int index = 0;
        for (int z = 0; z < lpvGridResolution; z++)
            for (int y = 0; y < lpvGridResolution; y++)
                for (int x = 0; x < lpvGridResolution; x++)
                {
                    Vector4 entry = array[index++];
                    if (entry != Vector4.zero)
                    {
                        Vector3 center = org + pix_x * (x + dd) + pix_y * (y + dd) + pix_z * (z + dd);
                        Gizmos.color = new Color(entry.x * 5, entry.y * 5, entry.z * 5);
                        Gizmos.DrawSphere(center, pixel_size * 0.125f);
                    }
                }
    }

    void DrawGizmosGV(ref Vector4[] array, RenderTexture rt)
    {
        if (rt == null)
            return;
        DrawGizmosExtract(ref array, rt, 1);

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

        Color base_color = Color.black;
        float dd = 0;

        int index = 0;
        for (int z = 0; z < lpvGridResolution; z++)
            for (int y = 0; y < lpvGridResolution; y++)
                for (int x = 0; x < lpvGridResolution; x++)
                {
                    Vector4 entry = array[index++];
                    if (entry != Vector4.zero)
                    {
                        Vector3 center = org + pix_x * (x + dd) + pix_y * (y + dd) + pix_z * (z + dd);
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
