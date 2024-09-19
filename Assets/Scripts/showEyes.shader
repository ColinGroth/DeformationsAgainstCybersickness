Shader "Hidden/showEyesShader"
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
            
            float4 EyePositions[2]; // for visualization
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 fovea = EyePositions[unity_StereoEyeIndex].xy * 0.5 + 0.5;
                if (distance(input.texcoord, fovea) < 0.005)
                {
                    return float4(0, 1, 0, 1);
                }
                
                return float4(LOAD_TEXTURE2D_X(_BlitTexture, input.texcoord * _ScreenSize.xy).xyz, 1);
            }
            ENDHLSL
        }
    }
}