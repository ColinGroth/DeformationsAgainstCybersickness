Shader "Hidden/prepareContrastShader"
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
            Name "ContrastPrepare"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"  // for XR functionality
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            TEXTURE2D_X(_ContrastTexture);
            SAMPLER(sampler_ContrastTexture);
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 localColor = SAMPLE_TEXTURE2D_X_LOD(_ContrastTexture, sampler_ContrastTexture, input.texcoord, 0);
                float selfMaskingTerm = 0.555;
                float spacialMaskingTerm = 0.135;

                float spacialMasking = 0;
                for (int i = -2; i <= 2; i++)
                {
                    for (int j = -2; j <= 2; j++)
                    {
                        spacialMasking += pow(abs(LOAD_TEXTURE2D_X_LOD(_ContrastTexture, input.texcoord * _ScreenSize + float2(j, i), 0).x), spacialMaskingTerm);
                    }
                }
                spacialMasking /= 25.0;

                float perceptualContrast = pow(abs(localColor.x), selfMaskingTerm) * sign(localColor.x) / (1 + spacialMasking);
                return float4(perceptualContrast * 1000, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}