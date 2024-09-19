Shader "Hidden/GaussianPyramid"
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

            TEXTURE2D_X(_TempMipTexture);
            TEXTURE2D_X(_LapPyramidTexture);
            SAMPLER(sampler_TempMipTexture);
            SAMPLER(sampler_LapPyramidTexture);

            float4 _Param; //_Direction * _TexelSize * _Spread
            int _LOD;
            int _TexID;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                //Apply separable gaussian blur.
                //http://rastergrid.com/blog/2010/09/efficient-gaussian-blur-with-linear-sampling/
                //Sampling at the offsets 3.2307692308, 1.3846153846, 0 allows us to look up information about 2 pixels at once due to bilinear filtering, so this is like
                // a 9x9 blur instead of a 5 + 5 
                if(_TexID == 0){
                return SAMPLE_TEXTURE2D_X_LOD(_TempMipTexture, sampler_TempMipTexture, input.texcoord - 3.2307692308 * _Param.xy, _LOD) * .0702702703 +
                    SAMPLE_TEXTURE2D_X_LOD(_TempMipTexture, sampler_TempMipTexture, input.texcoord - 1.3846153846 * _Param.xy, _LOD) * .3162162162 +
                    SAMPLE_TEXTURE2D_X_LOD(_TempMipTexture, sampler_TempMipTexture, input.texcoord, _LOD) * .2270270270 +
                    SAMPLE_TEXTURE2D_X_LOD(_TempMipTexture, sampler_TempMipTexture, input.texcoord + 1.3846153846 * _Param.xy, _LOD) * .3162162162 +
                    SAMPLE_TEXTURE2D_X_LOD(_TempMipTexture, sampler_TempMipTexture, input.texcoord + 3.2307692308 * _Param.xy, _LOD) * .0702702703;
                }
                return SAMPLE_TEXTURE2D_X_LOD(_LapPyramidTexture, sampler_LapPyramidTexture, input.texcoord - 3.2307692308 * _Param.xy, _LOD) * .0702702703 +
                    SAMPLE_TEXTURE2D_X_LOD(_LapPyramidTexture, sampler_LapPyramidTexture, input.texcoord - 1.3846153846 * _Param.xy, _LOD) * .3162162162 +
                    SAMPLE_TEXTURE2D_X_LOD(_LapPyramidTexture, sampler_LapPyramidTexture, input.texcoord, _LOD) * .2270270270 +
                    SAMPLE_TEXTURE2D_X_LOD(_LapPyramidTexture, sampler_LapPyramidTexture, input.texcoord + 1.3846153846 * _Param.xy, _LOD) * .3162162162 +
                    SAMPLE_TEXTURE2D_X_LOD(_LapPyramidTexture, sampler_LapPyramidTexture, input.texcoord + 3.2307692308 * _Param.xy, _LOD) * .0702702703;
            }            
            ENDHLSL
        }
    }
}