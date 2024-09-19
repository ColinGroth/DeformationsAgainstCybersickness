Shader "Hidden/blur"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"  // for XR functionality
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            // TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _EyePositions[2];
            float CamVelocity; //in m/s
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 fovea = _EyePositions[unity_StereoEyeIndex].xy * 0.5 + 0.5;
                int maxLevel = 4;
                float foveaSize = 0.05; // size in 0.5
                float p_distance = min(max(distance(input.texcoord, fovea) - foveaSize - (1 - min(CamVelocity, 1.0)) * (0.5 - foveaSize), 0), 0.499);
                if(p_distance < 0.001)
                    return LOAD_TEXTURE2D_X_LOD(_BlitTexture, input.texcoord * _ScreenSize, 0);
                int lod = int(p_distance * 2 * maxLevel);
                float lodScale = p_distance * 2 * maxLevel - lod;
                float3 sampleLow = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_BlitTexture, input.texcoord, lod).xyz;
                float3 sampleHigh = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_BlitTexture, input.texcoord, lod + 1).xyz;
                return float4(sampleLow * (1 - lodScale) + sampleHigh * lodScale, 1);
                
                return float4(sampleLow * (1 - lodScale) + sampleHigh * lodScale, 1);
            }
            ENDHLSL
        }
    }
}