﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class RSMTest : MonoBehaviour
{
    public Light directionalLight;
    public int gridResolution;     /* TODO: allow a larger value than in LPVTest */
    public float gridPixelSize;
    public LayerMask cullingMask = -1;
    public Shader depthShader, gvShader;
    public ComputeShader gvCompute;


    RenderTexture _target, _target2;
    Camera _shadowCam;

    private void Start()
    {
        DestroyTargets();
    }

    private void OnDestroy()
    {
        DestroyTargets();
    }

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
        return tg;
    }

    RenderTexture CreateTarget2()
    {
        RenderTexture tg = new RenderTexture(gridResolution, gridResolution, 0,
                                             RenderTextureFormat.ARGB32);
        tg.wrapMode = TextureWrapMode.Clamp;
        return tg;
    }

    void UpdateRenderTexture()
    {
        if (_target != null && _target.width != gridResolution)
            DestroyTargets();

        if (_target == null)
        {
            _target2 = CreateTarget2();
            _target = CreateTarget();
        }
    }

    public bool UpdateShadowsFull(out RenderTexture target, out RenderTexture target_color,
                                  out Matrix4x4 world_to_light_local_matrix,
                                  RenderTexture tex3d_gv)
    {
        if (gridResolution <= 0)
        {
            target = null;
            target_color = null;
            world_to_light_local_matrix = Matrix4x4.identity;
            return false;
        }
        UpdateRenderTexture();

        var cam = FetchShadowCamera();
        var trackTransform = directionalLight.transform;
        cam.transform.SetPositionAndRotation(trackTransform.position, trackTransform.rotation);

        float half_size = 0.5f * gridResolution * gridPixelSize;
        var mat = Matrix4x4.Scale(Vector3.one / (2f * half_size)) *
                  Matrix4x4.Translate(Vector3.one * half_size) *
                  cam.transform.worldToLocalMatrix;
        world_to_light_local_matrix = mat;

        /* First step: render into the GV (geometry volume).  Here, there is no depth map
         * and the fragment shader writes into the ComputeBuffer cb_gv.  At the end we
         * copy (and pack) the information into the more compact tex3d_gv.
         */
        var cb_gv = new ComputeBuffer(4 * gridResolution * gridResolution * gridResolution, 4);
        int clear_kernel = gvCompute.FindKernel("ClearKernel");
        gvCompute.SetInt("GridResolution", gridResolution);
        gvCompute.SetBuffer(clear_kernel, "RSM_gv", cb_gv);
        int thread_groups = (gridResolution * gridResolution * gridResolution + 63) / 64;
        gvCompute.Dispatch(clear_kernel, thread_groups, 1, 1);

        Shader.SetGlobalInt("_LPV_GridResolution", gridResolution);
        Shader.SetGlobalMatrix("_LPV_WorldToLightLocalMatrix", mat);

        cam.orthographicSize = half_size;
        cam.nearClipPlane = -half_size;
        cam.farClipPlane = half_size;
        cam.targetTexture = _target2;
        cam.clearFlags = CameraClearFlags.Nothing;
        Graphics.SetRandomWriteTarget(1, cb_gv);

        var orig_position = cam.transform.position;
        var orig_rotation = cam.transform.rotation;
        var axis_x = cam.transform.right;
        var axis_y = cam.transform.up;
        var axis_z = cam.transform.forward;
        cam.transform.position -= 0.5f * gridPixelSize * (axis_x + axis_y);
        cam.RenderWithShader(gvShader, "RenderType");
        cam.transform.SetPositionAndRotation(orig_position, orig_rotation);

        cam.transform.Rotate(axis_y, -90, Space.World);
        cam.transform.Rotate(axis_z, -90, Space.World);
        cam.transform.position -= 0.5f * gridPixelSize * (axis_z + axis_x);
        Shader.EnableKeyword("ORIENTATION_2");
        cam.RenderWithShader(gvShader, "RenderType");
        Shader.DisableKeyword("ORIENTATION_2");
        cam.transform.SetPositionAndRotation(orig_position, orig_rotation);

        cam.transform.Rotate(axis_z, 90, Space.World);
        cam.transform.Rotate(axis_y, 90, Space.World);
        cam.transform.position -= 0.5f * gridPixelSize * (axis_y + axis_z);
        Shader.EnableKeyword("ORIENTATION_3");
        cam.RenderWithShader(gvShader, "RenderType");
        Shader.DisableKeyword("ORIENTATION_3");
        cam.transform.SetPositionAndRotation(orig_position, orig_rotation);

        int pack_kernel = gvCompute.FindKernel("PackKernel");
        gvCompute.SetBuffer(pack_kernel, "RSM_gv", cb_gv);
        gvCompute.SetTexture(pack_kernel, "LPV_gv", tex3d_gv);
        thread_groups = (gridResolution + 3) / 4;
        gvCompute.Dispatch(pack_kernel, thread_groups, thread_groups, thread_groups);
        cb_gv.Release();

        /* Second step: render into the RSM (reflective shadow map).  This is a regular
         * vertex+fragment shader combination with a depth map, which renders into two
         * 2D textures, _target and _target2.
         */
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.SetTargetBuffers(new RenderBuffer[] { _target.colorBuffer, _target2.colorBuffer },
                             _target.depthBuffer);
        cam.nearClipPlane = -127f * half_size;
        cam.RenderWithShader(depthShader, "RenderType");

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
            _shadowCam.aspect = 1;
            /* Obscure: if the main camera is stereo, then this one will be confused in
             * the SetTargetBuffers() mode unless we force it to not be stereo */
            _shadowCam.stereoTargetEye = StereoTargetEyeMask.None;
        }
        _shadowCam.cullingMask = cullingMask;
        return _shadowCam;
    }
}
