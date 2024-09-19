using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using GazeTracker;


public class BlurRendererFeature : ScriptableRendererFeature
{
    private BlurRenderPass _blurPass;
    private static readonly int EyePositionsID = Shader.PropertyToID("_EyePositions");
    private static readonly int CamVelocityID = Shader.PropertyToID("CamVelocity");


    public override void Create()
    {
        _blurPass =
            new BlurRenderPass( 0, "SimpleBlur");
        _blurPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_blurPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        _blurPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        _blurPass.SetTarget(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _blurPass.Dispose();
        }
    }
    
    internal class BlurRenderPass : ScriptableRenderPass
    {
        private Material _blurMat;
        private Material _gaussMat;
        private Material _showEyesMat;
        private ProfilingSampler m_ProfilingSampler;
        private int m_PassIndex;
        RTHandle blur_CameraColorTarget;
        RTHandle pBlurRT;
        RTHandle gaussMipRT;
        RTHandle tempGaussMipRT;
        Vector4[] eyePosProj = new Vector4[2];
        Vector4[] eyeMovementUV = new Vector4[2];
        float camSpeed = 0.0f;
        Vector3 camPos = new Vector3(0, 0, 0);
        float camAngSpeed = 0.0f;
        Vector3 camForward = new Vector3(0, 0, 0);

        public BlurRenderPass(int index, string featureName)
        {
            _blurMat = new Material(Shader.Find("Hidden/blur"));
            _gaussMat = new Material(Shader.Find("Hidden/GaussianPyramid"));
            _showEyesMat = new Material(Shader.Find("Hidden/showEyesShader"));
            m_PassIndex = index;
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }

        public void Dispose()
        {
            tempGaussMipRT?.Release();
            pBlurRT?.Release();
            blur_CameraColorTarget?.Release();
            gaussMipRT?.Release();
        }

        public void SetTarget(RTHandle colorHandle)
        {
            blur_CameraColorTarget = colorHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(blur_CameraColorTarget);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;

            if (cameraData.isPreviewCamera)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                if (pBlurRT == null)
                {
                    pBlurRT = RTHandles.Alloc(new Vector2(1, 1), 2, colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
                        dimension: TextureDimension.Tex2DArray, name: "peripheralBlurTexture");
                    gaussMipRT = RTHandles.Alloc(blur_CameraColorTarget.rt.width, blur_CameraColorTarget.rt.height, 2,
                        colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        dimension: TextureDimension.Tex2DArray, useMipMap: true, autoGenerateMips: false,
                        filterMode: FilterMode.Trilinear, name: "GaussTexture");
                    tempGaussMipRT = RTHandles.Alloc(blur_CameraColorTarget.rt.width, blur_CameraColorTarget.rt.height, 2,
                        colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        dimension: TextureDimension.Tex2DArray, useMipMap: true, autoGenerateMips: false,
                        filterMode: FilterMode.Trilinear, name: "GaussTempTexture");
                    _gaussMat.SetTexture("_TempMipTexture", gaussMipRT);
                    _gaussMat.SetTexture("_LapPyramidTexture", tempGaussMipRT);
                }

                //-- Get Normalized Eye Positions 
                Matrix4x4 gpuProjLeft = GL.GetGPUProjectionMatrix(
                    renderingData.cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), true);
                Vector4 eyePosProjLeft = gpuProjLeft * new Vector4(-EyeTracking._eyeGazeDirection_l.x,
                    EyeTracking._eyeGazeDirection_l.y, EyeTracking._eyeGazeDirection_l.z, 0);
                eyePosProjLeft /= eyePosProjLeft.w;
                Matrix4x4 gpuProjRight = GL.GetGPUProjectionMatrix(
                    renderingData.cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), true);
                Vector4 eyePosProjRight = gpuProjRight * new Vector4(-EyeTracking._eyeGazeDirection_r.x,
                    EyeTracking._eyeGazeDirection_r.y, EyeTracking._eyeGazeDirection_r.z, 0);
                eyePosProjRight /= eyePosProjRight.w;
                eyeMovementUV[0] = eyePosProjLeft - eyePosProj[0];
                eyeMovementUV[1] = eyePosProjRight - eyePosProj[1];
                eyePosProj[0] = eyePosProjLeft;
                eyePosProj[1] = eyePosProjRight;
                
                GameObject rig = GameObject.Find("XRRig");
                Vector3 rigPos = rig.transform.position;
                camSpeed = (rigPos - camPos).magnitude / Time.deltaTime;
                camPos = rigPos;
                Vector3 rigForward = rig.transform.forward;
                camAngSpeed = Vector3.Angle(rigForward, camForward) / Time.deltaTime / 90.0f;
                camForward = rigForward;
                // Debug.Log("camSpeed: " + Mathf.Max(camSpeed, camAngSpeed));
                
                /*
                 * Create Gaussian Pyramid
                 */
                Blitter.BlitCameraTexture(cmd, blur_CameraColorTarget, gaussMipRT);
                
                int width = gaussMipRT.rt.width;
                int height = gaussMipRT.rt.height;

                cmd.SetGlobalVector("_BlitScaleBias", Vector2.one);
                int lodCount = 10;
                for (int i = 0; i < lodCount - 1; i++)
                {
                    width >>= 1;
                    height >>= 1;
                    Vector4 param = new Vector4(1.0f / (width - 1), 0, 0, 0);
                    cmd.SetGlobalVector("_Param", param);
                    cmd.SetGlobalInt("_LOD", i);
                    cmd.SetGlobalInt("_TexID", 0);
                    CoreUtils.SetRenderTarget(cmd, tempGaussMipRT, miplevel: (i + 1));
                    cmd.DrawProcedural(Matrix4x4.identity, _gaussMat, m_PassIndex, MeshTopology.Triangles, 3, 1);
                   
                    param = new Vector4(0, -1.0f / (height - 1), 0, 0);
                    cmd.SetGlobalVector("_Param", param);
                    cmd.SetGlobalInt("_LOD", i + 1);
                    cmd.SetGlobalInt("_TexID", 1);
                    CoreUtils.SetRenderTarget(cmd, gaussMipRT, miplevel: (i + 1)); 
                    cmd.DrawProcedural(Matrix4x4.identity, _gaussMat, m_PassIndex, MeshTopology.Triangles, 3, 1);
                }
                
                
                _blurMat.SetVectorArray(EyePositionsID, eyePosProj);
                _blurMat.SetFloat(CamVelocityID, Mathf.Max(camSpeed / 5.0f, camAngSpeed / 0.44f));
                Blitter.BlitCameraTexture(cmd, gaussMipRT, pBlurRT, _blurMat, m_PassIndex);
                _showEyesMat.SetVectorArray("EyePositions", eyePosProj);
                //Blitter.BlitCameraTexture(cmd, pBlurRT, blur_CameraColorTarget, _showEyesMat, m_PassIndex);
                Blitter.BlitCameraTexture(cmd, pBlurRT, blur_CameraColorTarget);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }
}