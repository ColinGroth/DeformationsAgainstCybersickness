using System.Runtime.CompilerServices;
using GazeTracker;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;


public class ContrastRendererFeatureNew : ScriptableRendererFeature
{
    private ContrastRenderPass contrastPass;

    private Material luminanceMat;
    private Material contrastMat;
    private Material gaussMaterial;
    private Material lapMaterial;
    private Material copyMat;
    private static readonly int EyePositionsID = Shader.PropertyToID("_EyePositions");

    public override void Create()
    {
        Shader luminanceShader = Shader.Find("Hidden/luminanceShader");
        luminanceMat = new Material(luminanceShader);
        Shader contrastShader = Shader.Find("Unlit/LuminanceContrast");
        contrastMat = new Material(contrastShader);
        Shader gaussShader = Shader.Find("Hidden/GaussianPyramid");
        gaussMaterial = new Material(gaussShader);
        Shader lapShader = Shader.Find("Unlit/LaplacianPyramid");
        lapMaterial = new Material(lapShader);
        Shader copyShader = Shader.Find("Hidden/copyShader");
        copyMat = new Material(copyShader);

        contrastPass =
            new ContrastRenderPass(luminanceMat, contrastMat, gaussMaterial, lapMaterial, copyMat, 0, "Contrast");
        contrastPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        // // This copy of requirements is used as a parameter to configure input in order to avoid copy color pass
        // ScriptableRenderPassInput modifiedRequirements = ScriptableRenderPassInput.Color;
        // // Removing Color flag in order to avoid unnecessary CopyColor pass
        // modifiedRequirements ^= ScriptableRenderPassInput.Color;
        // contrastPass.ConfigureInput(modifiedRequirements);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(contrastPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        contrastPass.ConfigureInput(ScriptableRenderPassInput.Color);
        contrastPass.SetTarget(renderer.cameraColorTargetHandle);
    }
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            contrastPass.Dispose();
        }
    }

    internal class ContrastRenderPass : ScriptableRenderPass
    {
        private Material _luminanceMat;
        private Material _contrastMat;
        private Material _gaussMat;
        private Material _lapMat;
        private Material _copyMat;
        private ProfilingSampler m_ProfilingSampler;
        private int m_PassIndex;
        int lodCount = 5;
        RTHandle contrast_CameraColorTarget;
        RTHandle tempMipRT;
        RTHandle lapPyramidRT;

        public ContrastRenderPass(Material material1, Material material2, Material material3, Material material4,
            Material material5, int index, string featureName)
        {
            _luminanceMat = material1;
            _contrastMat = material2;
            _gaussMat = material3;
            _lapMat = material4;
            _copyMat = material5;
            m_PassIndex = index;
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }

        public void Dispose()
        {
            contrast_CameraColorTarget?.Release();
            tempMipRT?.Release();
            lapPyramidRT?.Release();
        }

        public void SetTarget(RTHandle colorHandle)
        {
            contrast_CameraColorTarget = colorHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(contrast_CameraColorTarget);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;

            if (cameraData.isPreviewCamera)
                return;
            
            EyeTracking.updateEyeData(Time.deltaTime);

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                if (tempMipRT == null)
                {
                    tempMipRT = RTHandles.Alloc(contrast_CameraColorTarget.rt.width, contrast_CameraColorTarget.rt.height, 2,
                        colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        dimension: TextureDimension.Tex2DArray, useMipMap: true, autoGenerateMips: false,
                        filterMode: FilterMode.Trilinear, name: "TempTexture");
                    lapPyramidRT = RTHandles.Alloc(contrast_CameraColorTarget.rt.width, contrast_CameraColorTarget.rt.height, 2,
                        colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        dimension: TextureDimension.Tex2DArray, useMipMap: true, autoGenerateMips: false,
                        filterMode: FilterMode.Trilinear, name: "LaplacePyramidTexture");
                    _gaussMat.SetTexture("_TempMipTexture", tempMipRT);
                    _gaussMat.SetTexture("_LapPyramidTexture", lapPyramidRT);
                    _lapMat.SetTexture("_GaussTexture", tempMipRT);
                    Shader.SetGlobalTexture("_ContrastTexture", lapPyramidRT);
                }

                //-- Get Normalized Eye Positions 
                Matrix4x4 gpuProjLeft = GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), true);
                Vector4 eyePosProjLeft = gpuProjLeft * new Vector4(-EyeTracking._eyeGazeDirection_l.x, EyeTracking._eyeGazeDirection_l.y, EyeTracking._eyeGazeDirection_l.z, 0);
                eyePosProjLeft /= eyePosProjLeft.w;
                Matrix4x4 gpuProjRight = GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), true);
                Vector4 eyePosProjRight = gpuProjRight * new Vector4(-EyeTracking._eyeGazeDirection_r.x, EyeTracking._eyeGazeDirection_r.y, EyeTracking._eyeGazeDirection_r.z, 0);
                eyePosProjRight /= eyePosProjRight.w;
                Vector4[] eyePosProj = {eyePosProjLeft, eyePosProjRight};
                
                Blitter.BlitCameraTexture(cmd, contrast_CameraColorTarget, tempMipRT, _luminanceMat, m_PassIndex);

                /*
                 * Create Gaussian Pyramid
                 */
                int width = tempMipRT.rt.width;
                int height = tempMipRT.rt.height;

                cmd.SetGlobalVector("_BlitScaleBias", Vector2.one);
                for (int i = 0; i < lodCount - 1; i++)
                {
                    width >>= 1;
                    height >>= 1;
                    Vector4 param = new Vector4(1.0f / (width - 1), 0, 0, 0);
                    cmd.SetGlobalVector("_Param", param);
                    cmd.SetGlobalInt("_LOD", i);
                    cmd.SetGlobalInt("_TexID", 0);
                    CoreUtils.SetRenderTarget(cmd, lapPyramidRT, miplevel: (i + 1));
                    cmd.DrawProcedural(Matrix4x4.identity, _gaussMat, m_PassIndex, MeshTopology.Triangles, 3, 1);
                   
                    param = new Vector4(0, -1.0f / (height - 1), 0, 0);
                    cmd.SetGlobalVector("_Param", param);
                    cmd.SetGlobalInt("_LOD", i + 1);
                    cmd.SetGlobalInt("_TexID", 1);
                    CoreUtils.SetRenderTarget(cmd, tempMipRT, miplevel: (i + 1)); 
                    cmd.DrawProcedural(Matrix4x4.identity, _gaussMat, m_PassIndex, MeshTopology.Triangles, 3, 1);
                }

                /*
                 * Create Laplacian Pyramid
                 */
                _lapMat.SetVectorArray(EyePositionsID, eyePosProj);
                Blitter.BlitCameraTexture(cmd, tempMipRT, lapPyramidRT, mipLevel: lodCount-1);
                Blitter.BlitCameraTexture(cmd, tempMipRT, lapPyramidRT, mipLevel: lodCount - 2);
                for (int i = 0; i < lodCount - 2; i++)
                {
                    cmd.SetGlobalInt("_LOD", i);
                    CoreUtils.SetRenderTarget(cmd, lapPyramidRT, miplevel: i); 
                    cmd.DrawProcedural(Matrix4x4.identity, _lapMat, m_PassIndex, MeshTopology.Triangles, 3, 1);
                }
                
                // Blitter.BlitCameraTexture(cmd, lapPyramidRT, m_CameraColorTarget, _contrastMat, m_PassIndex);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }
}