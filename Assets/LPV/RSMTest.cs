using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class RSMTest : MonoBehaviour
{
    public Light directionalLight;
    public int gridResolution;     /* TODO: allow a larger value than in LPVTest */
    public float gridPixelSize;
    public LayerMask cullingMask = -1;
    public Shader depthShader;


    public RenderTexture _target, _target2;
    Camera _shadowCam;

    void DestroyTargets()
    {
        if (_target)
            DestroyImmediate(_target);
        _target = null;
        if (_target2)
            DestroyImmediate(_target2);
        _target2 = null;
    }

    RenderTexture CreateTarget()
    {
        /* depth, and four components as described in FetchShadowCamera() */
        RenderTexture tg = new RenderTexture(gridResolution, gridResolution, 24,
                                             RenderTextureFormat.ARGBHalf);
        tg.wrapMode = TextureWrapMode.Clamp;
        tg.Create();
        return tg;
    }

    RenderTexture CreateTarget2()
    {
        RenderTexture tg = new RenderTexture(gridResolution, gridResolution, 0,
                                             RenderTextureFormat.ARGB32);
        tg.wrapMode = TextureWrapMode.Clamp;
        tg.Create();
        return tg;
    }

    bool UpdateRenderTexture()
    {
        if (_target != null && _target.width != gridResolution)
            DestroyTargets();

        if (_target == null)
        {
            if (gridResolution <= 0)
                return false;
            _target2 = CreateTarget2();
            _target = CreateTarget();
        }
        return true;
    }

    bool InitializeUpdateSteps()
    {
        if (!UpdateRenderTexture())
            return false;

        SetUpShadowCam();
        _shadowCam.SetTargetBuffers(new RenderBuffer[] { _target.colorBuffer, _target2.colorBuffer },
                                    _target.depthBuffer);

        return true;
    }

    public bool UpdateShadowsFull(out RenderTexture target, out RenderTexture target_color)
    {
        if (!InitializeUpdateSteps())
        {
            target = null;
            target_color = null;
            return false;
        }

        float half_size = 0.5f * gridResolution * gridPixelSize;
        _shadowCam.orthographicSize = half_size;
        _shadowCam.nearClipPlane = -half_size;
        _shadowCam.farClipPlane = half_size;

        var mat = Matrix4x4.Scale(Vector3.one / (2f*half_size)) *
                  Matrix4x4.Translate(Vector3.one * half_size) *
                  _shadowCam.transform.worldToLocalMatrix;

        Shader.SetGlobalMatrix("_LPV_WorldToLightLocalMatrix", mat);

        _shadowCam.RenderWithShader(depthShader, "RenderType");

        target = _target;
        target_color = _target2;
        return true;
    }

    Camera FetchShadowCamera()
    {
        if (_shadowCam == null)
        {
            // Create the shadow rendering camera
            GameObject go = new GameObject("RSM shadow cam (not saved)");
            //go.hideFlags = HideFlags.HideAndDontSave;
            go.hideFlags = HideFlags.DontSave;

            _shadowCam = go.AddComponent<Camera>();
            _shadowCam.orthographic = true;
            _shadowCam.enabled = false;
            /* the shadow camera renders to four components:
             *    r, g, b: surface normal vector
             *    a: depth, in [-0.5, 0.5] with 0.0 being at the camera position
             *              and larger values being farther from the light source
             */
            _shadowCam.backgroundColor = new Color(0, 0, 0, 1);
            _shadowCam.clearFlags = CameraClearFlags.SolidColor;
            _shadowCam.aspect = 1;
        }
        return _shadowCam;
    }


    void SetUpShadowCam()
    {
        var cam = FetchShadowCamera();

        var trackTransform = directionalLight.transform;
        cam.transform.SetPositionAndRotation(trackTransform.position, trackTransform.rotation);

        /* Set up the clip planes so that we store depth values in the range [-0.5, 0.5],
         * with values near zero being near us even if depthOfShadowRange is very large.
         * This maximizes the precision in the RHalf textures near us. */
        cam.cullingMask = cullingMask;
    }
}
