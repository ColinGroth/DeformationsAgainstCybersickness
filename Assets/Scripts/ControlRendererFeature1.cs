using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using GazeTracker;


public class ControlRendererFeature : ScriptableRendererFeature
{
    private ControlRenderPass _controlPass;
    private Material warpMat;
    private static readonly int IterationID = Shader.PropertyToID("Iteration");
    private static readonly int EyePositionLeftID = Shader.PropertyToID("_EyePositionLeft");
    private static readonly int EyePositionRightID = Shader.PropertyToID("_EyePositionRight");
    private static readonly int EyePositionsID = Shader.PropertyToID("_EyePositions");
    private static readonly int EyeMovementVectorID = Shader.PropertyToID("_EyeMovementVector");
    private static readonly int CamVelocityID = Shader.PropertyToID("CamVelocity");
    private static readonly int EyePositions = Shader.PropertyToID("EyePositions");

    public override void Create()
    {
        warpMat = new Material(Shader.Find("Hidden/warpShader"));

        _controlPass =
            new ControlRenderPass(warpMat, 0, "Control");
        _controlPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_controlPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        _controlPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        _controlPass.SetTarget(renderer.cameraColorTargetHandle);
    }
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _controlPass.Dispose();
        }
    }

    internal class ControlRenderPass : ScriptableRenderPass
    {
        private Material _warpMat;
        private Material _prepareMotionMat;
        private Material _prepareContrastMat;
        private Material _motionVectorsMat;
        Matrix4x4[] ViewProjMat_prev = new Matrix4x4[2];
        Matrix4x4[] ViewProjMat_curr = new Matrix4x4[2];
        private ProfilingSampler m_ProfilingSampler;
        private int m_PassIndex;
        RTHandle Control_CameraColorTarget;
        RTHandle warpedRT_dummy;
        RTHandle motionVectorsRT_dummy;
        RTHandle contrastRT_dummy;
        int frameCount = 0;
        const int quadSize = 32;
        private int quadsPerRow;
        private int quadsPerColumn;
        Vector2 quadSizeIn2;
        ComputeShader optimizationShader;
        ComputeShader fullResetShader;
        ComputeShader saccadeResetShader;
        ComputeBuffer verticesBuffer;
        ComputeBuffer tempBuffer;
        ComputeBuffer velocitiesBuffer;
        private int optimKernelIdx;
        private int resetKernelIdx;
        private int saccadeResetKernelIdx;
        int numVertices;
        Vector4[] eyePosProj = new Vector4[2];
        Vector4[] eyeMovementUV = new Vector4[2];
        float camSpeed = 0.0f;
        Vector3 camPos = new Vector3(0, 0, 0);
        float camAngSpeedNorm = 0.0f;
        Vector3 rigForwardPrev = new Vector3(0, 0, 0);
        private Quaternion rigRotationPrev = new Quaternion();

        public ControlRenderPass(Material material, int index, string featureName)
        {
            _warpMat = material;
            _prepareMotionMat = new Material(Shader.Find("Hidden/prepareMotionVectorsShader"));
            _prepareContrastMat = new Material(Shader.Find("Hidden/prepareContrastShader"));
            _motionVectorsMat = new Material(Shader.Find("Unlit/MotionVectorShader"));
            m_PassIndex = index;
            m_ProfilingSampler = new ProfilingSampler(featureName);

            optimizationShader = Resources.Load<ComputeShader>("optimizationCompute");
            fullResetShader = Resources.Load<ComputeShader>("resetScene");
            saccadeResetShader = Resources.Load<ComputeShader>("resetSceneSaccade");
        }

        public void Dispose()
        {
            Control_CameraColorTarget?.Release();
            warpedRT_dummy?.Release();
            motionVectorsRT_dummy?.Release();
            contrastRT_dummy?.Release();
            
        }

        public void SetTarget(RTHandle colorHandle)
        {
            Control_CameraColorTarget = colorHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(Control_CameraColorTarget);
        }

        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;

            if (cameraData.isPreviewCamera)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                if (warpedRT_dummy == null)
                {
                    warpedRT_dummy = RTHandles.Alloc(new Vector2(1, 1), 2, colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        dimension: TextureDimension.Tex2DArray, name: "WarpedTexture");
                    motionVectorsRT_dummy = RTHandles.Alloc(new Vector2(1, 1), 2,
                        colorFormat: GraphicsFormat.R32G32_SFloat, useMipMap: true, autoGenerateMips: true,
                        dimension: TextureDimension.Tex2DArray, filterMode: FilterMode.Bilinear,
                        name: "MotionVectorsMipTexture");
                    contrastRT_dummy = RTHandles.Alloc(new Vector2(1, 1), 2,
                        colorFormat: GraphicsFormat.R32_SFloat, useMipMap: true, autoGenerateMips: true,
                        dimension: TextureDimension.Tex2DArray, filterMode: FilterMode.Bilinear,
                        name: "ContrastMipTexture");

                    quadsPerRow = (int)((warpedRT_dummy.rt.width + (quadSize - 1)) / (float)quadSize);
                    quadsPerColumn = (int)((warpedRT_dummy.rt.height + (quadSize - 1)) / (float)quadSize);
                    quadSizeIn2 = new Vector2(2.0f * quadSize / warpedRT_dummy.rt.width,
                        2.0f * quadSize / warpedRT_dummy.rt.height);
                    numVertices = (quadsPerRow - 1) * (quadsPerColumn - 1) * 2;
                    verticesBuffer = new ComputeBuffer(numVertices * 2, 2 * sizeof(float));
                    tempBuffer = new ComputeBuffer(numVertices, 2 * sizeof(float));
                    velocitiesBuffer = new ComputeBuffer(numVertices, 2 * sizeof(float));

                    _warpMat.SetFloat("_QuadSizeIn2_X", quadSizeIn2.x);
                    _warpMat.SetFloat("_QuadSizeIn2_Y", quadSizeIn2.y);
                    _warpMat.SetInt("_QuadsPerRow", quadsPerRow);
                    _warpMat.SetInt("_QuadsPerColumn", quadsPerColumn);
                    _warpMat.SetTexture("_MotionVectorsMipTexture", motionVectorsRT_dummy);
                    _warpMat.SetBuffer("_Positions", verticesBuffer);

                    optimKernelIdx = optimizationShader.FindKernel("ComputeMovement");
                    optimizationShader.SetTexture(optimKernelIdx, "motionVectors", motionVectorsRT_dummy);
                    optimizationShader.SetTexture(optimKernelIdx, "contrast", contrastRT_dummy);
                    optimizationShader.SetBuffer(optimKernelIdx, "points", verticesBuffer);
                    optimizationShader.SetBuffer(optimKernelIdx, "pointsPrev", tempBuffer);
                    optimizationShader.SetBuffer(optimKernelIdx, "velocities", velocitiesBuffer);
                    optimizationShader.SetInt("NumPoints", numVertices);
                    optimizationShader.SetInt("Width", quadsPerRow - 1);
                    optimizationShader.SetInt("Height", quadsPerColumn - 1);

                    resetKernelIdx = fullResetShader.FindKernel("FullReset");
                    fullResetShader.SetBuffer(resetKernelIdx, "points", verticesBuffer);
                    fullResetShader.SetInt("NumPoints", numVertices);
                    saccadeResetKernelIdx = saccadeResetShader.FindKernel("SaccadeReset");
                    saccadeResetShader.SetBuffer(saccadeResetKernelIdx, "points", verticesBuffer);
                    saccadeResetShader.SetInt("_NumPoints", numVertices);
                    saccadeResetShader.SetInt("_Width", quadsPerRow - 1);
                    saccadeResetShader.SetInt("_Height", quadsPerColumn - 1);
                }

                //-- Get Normalized Eye Positions 
                Camera cam = renderingData.cameraData.camera;
                Matrix4x4 gpuProjLeft = GL.GetGPUProjectionMatrix(
                    cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), true);
                Vector4 eyePosProjLeft = gpuProjLeft * new Vector4(-EyeTracking._eyeGazeDirection_l.x,
                    EyeTracking._eyeGazeDirection_l.y, EyeTracking._eyeGazeDirection_l.z, 0);
                eyePosProjLeft /= eyePosProjLeft.w;
                Matrix4x4 gpuProjRight = GL.GetGPUProjectionMatrix(
                    cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), true);
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
                camAngSpeedNorm = Vector3.Angle(rigForward, rigForwardPrev) / Time.deltaTime / 90.0f / 0.44f;
                Vector3 camMovementVec = Quaternion.Inverse(rigRotationPrev) * rig.transform.rotation * Vector3.forward;
                rigForwardPrev = rigForward;
                rigRotationPrev = rig.transform.rotation;
                // Debug.Log("Vec: " + new Vector2(camMovementVec.x, camMovementVec.y).normalized + " speed: " +  camAngSpeedNorm);

                //-- Compute Motion Vectors
                ViewProjMat_curr[0] = gpuProjLeft * cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                ViewProjMat_curr[1] = gpuProjRight * cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
                _motionVectorsMat.SetMatrixArray("_PrevViewProjMatrix_TT", ViewProjMat_prev);
                _motionVectorsMat.SetMatrixArray("_CurrentViewProjMatrix_TT", ViewProjMat_curr);
                Blitter.BlitCameraTexture(cmd, Control_CameraColorTarget, motionVectorsRT_dummy, _motionVectorsMat, m_PassIndex);

                //-- Prepare Contrast
                // Blitter.BlitCameraTexture(cmd, motionVectorsRT, motionVectorsRT, _prepareMotionMat, m_PassIndex);
                Blitter.BlitCameraTexture(cmd, contrastRT_dummy, contrastRT_dummy, _prepareContrastMat, m_PassIndex);
                
                //-- Reset Grid
                if (frameCount++ == 2 || EyeTracking._currentMovement == EyeTracking.MovementType.Blink) // || frameCount % 300 == 0
                    fullResetShader.Dispatch(resetKernelIdx, numVertices / 64 + 1, 1, 1);
                else if (EyeTracking._currentMovement == EyeTracking.MovementType.Saccade)
                {
                    saccadeResetShader.SetVectorArray(EyePositions, eyePosProj);
                    saccadeResetShader.SetVectorArray(EyeMovementVectorID, eyeMovementUV);
                    saccadeResetShader.Dispatch(saccadeResetKernelIdx, numVertices / 64 + 1, 1, 1);
                }

                //-- Run Optimization of Grid
                optimizationShader.SetVector(EyePositionLeftID, eyePosProj[0]);
                optimizationShader.SetVector(EyePositionRightID, eyePosProj[1]);
                optimizationShader.SetFloat(CamVelocityID, camSpeed);
                optimizationShader.SetFloat("IsMoving", camSpeed > 0.001f ? 1.0f : 0.001f);
                int iterationCount = 0;
                int optimIterations = 50;
                for (int i = 0; i < optimIterations; i++)
                {
                    optimizationShader.SetInt(IterationID, iterationCount++);
                    optimizationShader.Dispatch(optimKernelIdx, numVertices / 64 + 1, 1,
                        1); // maybe dispatch in 2 dimensions
                }

                //-- Warp
                _warpMat.SetFloat("_AngularVelocity", camAngSpeedNorm);
                _warpMat.SetVector("_VelocityVec", new Vector2(camMovementVec.x, camMovementVec.y).normalized);
                _warpMat.SetVectorArray(EyePositionsID, eyePosProj);
                CoreUtils.SetRenderTarget(cmd, warpedRT_dummy);
                cmd.DrawProcedural(Matrix4x4.identity, _warpMat, m_PassIndex, MeshTopology.Quads,
                    4 * quadsPerRow * quadsPerColumn, 1);
                // Blitter.BlitCameraTexture(cmd, warpedRT, warp_CameraColorTarget);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            ViewProjMat_prev[0] = ViewProjMat_curr[0];
            ViewProjMat_prev[1] = ViewProjMat_curr[1];
            
            CommandBufferPool.Release(cmd);
        }
    }
}