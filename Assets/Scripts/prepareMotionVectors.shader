Shader "Hidden/prepareMotionVectorsShader"
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

            TEXTURE2D_X(_MotionVectorTexture);
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 currentMotion = LOAD_TEXTURE2D_X(_MotionVectorTexture, input.texcoord * _ScreenSize.xy).xy * 2 - 1;
                currentMotion.y *= -1;
                return float4(currentMotion, 0, 1);
            }
            ENDHLSL
        }
    }
}