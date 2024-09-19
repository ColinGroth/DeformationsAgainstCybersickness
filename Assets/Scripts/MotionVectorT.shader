Shader "Unlit/MotionVectorShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
//        _CameraVeclocity(" _CameraVeclocity", Float) = 0.5   
//        _FrameInterval("_FrameInterval", Float) = 0.5
    }
    SubShader
    {
        Pass
        {
            Cull Off

            ZWrite Off

            ZTest Always

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
			#pragma target 2.0

            
            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            // -------------------------------------
            // Inputs
            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
            float4x4 _PrevViewProjMatrix_TT[2];
            float4x4 _CurrentViewProjMatrix_TT[2];
            // float _CameraVelocity;
            // float _FrameInterval;

            // -------------------------------------
            // Fragment
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // Calculate PositionInputs
                half depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, input.texcoord).x;
                depth = depth >= 1.0 ? 0 : depth;
                half2 screenSize = half2(1 / _ScreenSize.x, 1 / _ScreenSize.y);
                //half2 screenSize = half2(1.0 / 3840, 1.0 / 2160);
                //half depth = 0;
                PositionInputs positionInputs = GetPositionInput(input.texcoord * _ScreenSize, screenSize, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

                // Calculate positions
                float4 previousPositionVP = mul(_PrevViewProjMatrix_TT[unity_StereoEyeIndex], float4(positionInputs.positionWS, 1.0));
                float4 positionVP = mul(_CurrentViewProjMatrix_TT[unity_StereoEyeIndex], float4(positionInputs.positionWS, 1.0));
                previousPositionVP.xy = previousPositionVP.xy / previousPositionVP.w;
                positionVP.xy = positionVP.xy / positionVP.w;

                // Calculate velocity
                float2 velocity = (positionVP.xy - previousPositionVP.xy); /// _FrameInterval;
                #if UNITY_UV_STARTS_AT_TOP
                    velocity.y = -velocity.y;
                #endif

                // Convert velocity from Clip space (-1..1) to NDC 0..1 space
                // Note it doesn't mean we don't have negative value, we store negative or positive offset in NDC space.
                // Note: ((positionVP * 0.5 + 0.5) - (previousPositionVP * 0.5 + 0.5)) = (velocity * 0.5)
                return float4(velocity.xy * 0.5f, 0.0f, 1.0f);
            }

            ENDHLSL
        }
    }
}
