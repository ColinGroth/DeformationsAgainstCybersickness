Shader "Hidden/warpShader"
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
            Name "Warp"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"  // for XR functionality
            // The Blit.hlsl file provides the vertex shader (Vert),
            // input structure (Attributes) and output structure (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex vert_warp
            #pragma fragment frag

            // TEXTURE2D_X(_BlitTexture);
            TEXTURE2D_X(_CameraOpaqueTexture);
            TEXTURE2D_X(_MotionVectorsMipTexture);

            SAMPLER(sampler_CameraOpaqueTexture);
            SAMPLER(sampler_MotionVectorsMipTexture);

            float _QuadSizeIn2_X;
            float _QuadSizeIn2_Y;
            int _QuadsPerRow;
            int _QuadsPerColumn;
            float4 _EyePositions[2]; // for visualization
            StructuredBuffer<float2> _Positions;
            float _AngularVelocity;
            float2 _VelocityVec;

            Varyings vert_warp(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                const uint id = input.vertexID;

                int corner = id % 4;
                int quad_num = id / 4;
                uint2 vert_idx = uint2(quad_num % _QuadsPerRow, quad_num / _QuadsPerRow);
                
                vert_idx.x += ((corner + 1) & 2) >> 1;  // if(corner == 1 || corner == 2) add 1
                vert_idx.y += (corner & 2) >> 1;        // if(corner == 2 || corner == 3) add 1

                float2 pos = float2(vert_idx.x * _QuadSizeIn2_X - 1, vert_idx.y * _QuadSizeIn2_Y - 1);
                float2 uv =  float2(1, -1) * pos * 0.5 + 0.5;
                output.texcoord = uv;

                float2 displacement; 
                if(vert_idx.x == 0 || vert_idx.x == _QuadsPerRow || vert_idx.y == 0 || vert_idx.y == _QuadsPerColumn)
                {
                    output.positionCS = float4(pos, 0, 1);
                    return output;
                }
                
                if(_AngularVelocity > 0.01)
                {
                    float d_eyesOnLine = dot(_EyePositions[unity_StereoEyeIndex] * 0.5, _VelocityVec); 
                    float d_vertexOnLine = dot(uv - 0.5, _VelocityVec); //here for simple calculation all values are considered in a [-.5,.5] space
                    float scale = (1 - cos(abs(d_vertexOnLine) * 1.5 * HALF_PI)) * 0.624; // scale with cos of distance to closer: focal point or center; intensity values found in the pre-experiment  //min(abs(d_eyesOnLine - d_vertexOnLine), abs(d_vertexOnLine))
                    displacement = (_VelocityVec * d_vertexOnLine - (uv - 0.5)) * float2(-_AngularVelocity, _AngularVelocity) * 2 * scale;  // get distance to line by line projection
                    float ratioLinDisplace = max(1 - _AngularVelocity / 0.3, 0);
                    displacement += _Positions[int(uv.y * (_QuadsPerColumn - 1)) * (_QuadsPerRow - 1) + uv.x * (_QuadsPerRow - 1) + unity_StereoEyeIndex * (_QuadsPerRow - 1) * (_QuadsPerColumn - 1)] * ratioLinDisplace;
                }
                else
                    displacement = _Positions[int(uv.y * (_QuadsPerColumn - 1)) * (_QuadsPerRow - 1) + uv.x * (_QuadsPerRow - 1) + unity_StereoEyeIndex * (_QuadsPerRow - 1) * (_QuadsPerColumn - 1)];
                    
                output.positionCS = float4(pos - displacement, 0, 1);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); // for unity_StereoEyeIndex
                
                // return float4(input.texcoord, 0, 1);
                
                // float2 fovea = _EyePositions[unity_StereoEyeIndex].xy * 0.5 + 0.5;
                // if (distance(input.texcoord, fovea) < 0.005)
                // {
                //     if (unity_StereoEyeIndex == 0)
                //         return float4(1, 0, 0, 1);
                //     return float4(0, 1, 0, 1);
                // }

                return SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, input.texcoord);
            }
            ENDHLSL
        }
    }
}