using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class LPVTest : MonoBehaviour
{
    public int lpvGridResolution;
    public float lpvGridCellSize;
    public ComputeShader lpvShader;

    public RenderTexture _lpvTex3D_r, _lpvTex3D_r_prev, _lpvTex3D_r_accum;
    public int propagateSteps;
    public bool drawGizmos;


    private void Start()
    {
        DestroyTargets();
    }


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

    void DestroyTargets()
    {
        DestroyTarget(ref _lpvTex3D_r);
        DestroyTarget(ref _lpvTex3D_r_prev);
        DestroyTarget(ref _lpvTex3D_r_accum);
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

    private void Update()
    {
        if (!GetComponent<RSMTest>().UpdateShadowsFull(out RenderTexture shadow_texture))
            return;

        if (_lpvTex3D_r != null && _lpvTex3D_r.width != lpvGridResolution)
            DestroyTargets();

        if (_lpvTex3D_r == null)
        {
            if (lpvGridResolution == 0)
                return;
            _lpvTex3D_r_prev = CreateTarget();
            _lpvTex3D_r_accum = CreateTarget();
            _lpvTex3D_r = CreateTarget();
        }

        lpvShader.SetInt("GridResolution", lpvGridResolution);
        lpvShader.SetFloat("InverseGridResolution", 1f / lpvGridResolution);

        int clear_kernel = lpvShader.FindKernel("ClearKernel");
        lpvShader.SetTexture(clear_kernel, "LPV_r", _lpvTex3D_r);
        lpvShader.SetTexture(clear_kernel, "LPV_r_accum", _lpvTex3D_r_accum);
        int thread_groups = (lpvGridResolution + 3) / 4;
        lpvShader.Dispatch(clear_kernel, thread_groups, thread_groups, thread_groups);

        int inject_kernel = lpvShader.FindKernel("InjectKernel");
        lpvShader.SetTexture(inject_kernel, "LPV_r", _lpvTex3D_r);
        lpvShader.SetTexture(inject_kernel, "LPV_r_accum", _lpvTex3D_r_accum);
        lpvShader.SetTexture(inject_kernel, "ShadowTexture", shadow_texture);
        thread_groups = (lpvGridResolution + 7) / 8;
        lpvShader.Dispatch(inject_kernel, thread_groups, thread_groups, 1);

        int propagate_step_kernel = lpvShader.FindKernel("PropagateStepKernel");
        lpvShader.SetTexture(propagate_step_kernel, "LPV_r_accum", _lpvTex3D_r_accum);
        thread_groups = (lpvGridResolution + 3) / 4;
        for (int i = 0; i < propagateSteps; i++)
        {
            Swap(ref _lpvTex3D_r_prev, ref _lpvTex3D_r);
            lpvShader.SetTexture(propagate_step_kernel, "LPV_r_prev", _lpvTex3D_r_prev);
            lpvShader.SetTexture(propagate_step_kernel, "LPV_r", _lpvTex3D_r);
            lpvShader.Dispatch(propagate_step_kernel, thread_groups, thread_groups, thread_groups);
        }

        Shader.SetGlobalTexture("_LPV_r_accum", _lpvTex3D_r_accum);
    }


#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos || _lpvTex3D_r == null)
            return;

        int extract_kernel = lpvShader.FindKernel("ExtractKernel");
        var buffer = new ComputeBuffer(lpvGridResolution * lpvGridResolution * lpvGridResolution, 4 * 4, ComputeBufferType.Default);
        lpvShader.SetBuffer(extract_kernel, "ExtractTexture", buffer);
        lpvShader.SetTexture(extract_kernel, "LPV_r", _lpvTex3D_r_accum);
        int thread_groups = (lpvGridResolution + 3) / 4;
        lpvShader.Dispatch(extract_kernel, thread_groups, thread_groups, thread_groups);

        Vector4[] array = new Vector4[lpvGridResolution * lpvGridResolution * lpvGridResolution];
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

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(org, org + pix_x * lpvGridResolution);
        Gizmos.DrawLine(org, org + pix_y * lpvGridResolution);
        Gizmos.DrawLine(org, org + pix_z * lpvGridResolution);

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
                            Gizmos.color = len >= 0 ? new Color(0.5f, 0, 0) : new Color(0, 0.5f, 0.5f);
                            Vector3 dir1 = pix_x * dir.x + pix_y * dir.y + pix_z * dir.z;
                            Gizmos.DrawLine(center, center + len * dir1);
                        }
                    }
                }
    }
#endif
}
