Shader "Unlit/LaplacianPyramid"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline" "ShaderGraphTargetId"="UniversalFullscreenSubTarget"
        }
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output strucutre (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            TEXTURE2D_X(_GaussTexture);
            SAMPLER(sampler_GaussTexture);

            int _LOD;
            float4 _EyePositions[2];

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 rgbGaussian = SAMPLE_TEXTURE2D_X_LOD(_GaussTexture, sampler_GaussTexture, input.texcoord, _LOD);
                float4 rgbDownsampled = SAMPLE_TEXTURE2D_X_LOD(_GaussTexture, sampler_GaussTexture, input.texcoord, _LOD + 1);
                float4 rgbDownsampledTwice = SAMPLE_TEXTURE2D_X_LOD(_GaussTexture, sampler_GaussTexture, input.texcoord, _LOD + 2);
                float4 contrast = float4((rgbGaussian.xyz - rgbDownsampled.xyz) / (rgbDownsampledTwice.xyz + 0.001), 1.0);

                // normalize with CSF
                float FOV = 100;
                float spacialFrequency = _ScreenSize.x * 0.5 / FOV; //in cpd
                float eccentricity = abs(distance(input.texcoord, _EyePositions[unity_StereoEyeIndex].xy * 0.5 + 0.5) * FOV);
                float acuityLoss = 0.04; //fundamental eccentricity parameter
                float adaptationToPeak = pow(1 + 0.7 / rgbDownsampledTwice.x, -0.2);
                float standardCSF = 2.6 * (0.0192 + 0.114 * spacialFrequency) * exp(-pow(0.114 * spacialFrequency, 1.1));
                float CSF = 1.0 / exp(acuityLoss * eccentricity * spacialFrequency) * adaptationToPeak * standardCSF;

                return contrast * CSF;
            }
            ENDHLSL
        }
    }
}