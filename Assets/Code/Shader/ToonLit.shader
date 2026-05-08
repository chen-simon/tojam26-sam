Shader "Custom/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Toon)]
        _ShadowColor("Shadow Color", Color) = (0.45, 0.5, 0.65, 1)
        _RampThreshold("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmoothness("Ramp Smoothness", Range(0.001, 1)) = 0.05

        [Header(Rim)]
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.72
        _RimSmoothness("Rim Smoothness", Range(0.001, 0.5)) = 0.05
        _RimIntensity("Rim Intensity", Range(0, 4)) = 1

        [Header(Normal Map)]
        [Toggle(_NORMALMAP)] _NormalMapEnabled("Enable Normal Map", Float) = 0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1

        [HideInInspector] _Cutoff("Alpha Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ─────────────────────────────────────────────────────────────────────
        // Forward Lit Pass
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex   ToonVert
            #pragma fragment ToonFrag

            // Material keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // URP lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // GI / lightmap keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            // GPU instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowColor;
                half   _RampThreshold;
                half   _RampSmoothness;
                half4  _RimColor;
                half   _RimThreshold;
                half   _RimSmoothness;
                half   _RimIntensity;
                float  _BumpScale;
                // Required by ShadowCaster / DepthOnly passes (unused here)
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float4 tangentOS   : TANGENT;
                float2 uv          : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 tangentWS   : TEXCOORD3; // xyz = tangent, w = sign
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                float4 shadowCoord : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Cel-shading ramp: sharp at low smoothness, soft at high.
            half ToonRamp(half value, half threshold, half smoothness)
            {
                return smoothstep(threshold - smoothness * 0.5, threshold + smoothness * 0.5, value);
            }

            Varyings ToonVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs    = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS   = normalInputs.normalWS;
                output.tangentWS  = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH4(posInputs.positionWS, output.normalWS, GetWorldSpaceNormalizeViewDir(posInputs.positionWS), output.vertexSH, 0);

                // Only interpolate shadow coord for screen-space shadows.
                // Cascade/regular shadow coords are computed per-fragment to avoid
                // precision artifacts (visible as triangular banding on flat surfaces).
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    output.shadowCoord = ComputeScreenPos(posInputs.positionCS);
                #else
                    output.shadowCoord = float4(0, 0, 0, 0);
                #endif
                output.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Normal (world space)
                #if defined(_NORMALMAP)
                    float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    float3x3 TBN = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                    half3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    float3 normalWS = normalize(mul(normalTS, TBN));
                #else
                    float3 normalWS = normalize(input.normalWS);
                #endif

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Shadow coord computed per-fragment to eliminate interpolation
                // artifacts (triangular banding on flat surfaces amplified by toon ramp).
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = input.shadowCoord; // screen-space: computed in vertex
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                // ── Main light ────────────────────────────────────────────────
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));

                half  NdotL      = saturate(dot(normalWS, mainLight.direction));
                half  shadow     = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half  mainRamp   = ToonRamp(NdotL * shadow, _RampThreshold, _RampSmoothness);

                // Lerp albedo between shadow tint and full lit, then modulate by light color.
                half3 color = albedo.rgb * lerp(_ShadowColor.rgb, half3(1, 1, 1), mainRamp) * mainLight.color;

                // ── Additional lights ─────────────────────────────────────────
                // InputData is required by LIGHT_LOOP_BEGIN in Forward+ mode —
                // the macro uses inputData.positionWS / normalizedScreenSpaceUV
                // for cluster-tile lookups.
                InputData inputData = (InputData)0;
                inputData.positionWS            = input.positionWS;
                inputData.normalWS              = normalWS;
                inputData.viewDirectionWS       = viewDirWS;
                inputData.shadowCoord           = shadowCoord;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #ifdef _ADDITIONAL_LIGHTS
                {
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light addLight  = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half  addNdotL  = saturate(dot(normalWS, addLight.direction));
                        half  addAtten  = addLight.shadowAttenuation * addLight.distanceAttenuation;
                        half  addRamp   = ToonRamp(addNdotL * addAtten, _RampThreshold, _RampSmoothness);
                        color += albedo.rgb * addLight.color * addRamp;
                    LIGHT_LOOP_END
                }
                #endif

                // ── Ambient / GI ──────────────────────────────────────────────
                half3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                // GI fills shadow areas softly without washing out the toon split.
                color += albedo.rgb * bakedGI * lerp(half3(1, 1, 1), _ShadowColor.rgb, 0.5);

                // ── Rim light ─────────────────────────────────────────────────
                half fresnel = 1.0 - saturate(dot(viewDirWS, normalWS));
                half rim     = smoothstep(_RimThreshold - _RimSmoothness,
                                         _RimThreshold + _RimSmoothness, fresnel);
                color += _RimColor.rgb * rim * _RimIntensity * mainRamp;

                // ── Fog ───────────────────────────────────────────────────────
                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shadow Caster
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Minimal CBUFFER — ShadowCasterPass.hlsl requires these symbols.
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowColor;
                half   _RampThreshold;
                half   _RampSmoothness;
                half4  _RimColor;
                half   _RimThreshold;
                half   _RimSmoothness;
                half   _RimIntensity;
                float  _BumpScale;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        // Depth Only
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowColor;
                half   _RampThreshold;
                half   _RampSmoothness;
                half4  _RimColor;
                half   _RimThreshold;
                half   _RimSmoothness;
                half   _RimIntensity;
                float  _BumpScale;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────────
        // Depth Normals (used by SSAO and other effects)
        // ─────────────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex   DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowColor;
                half   _RampThreshold;
                half   _RampSmoothness;
                half4  _RimColor;
                half   _RimThreshold;
                half   _RimSmoothness;
                half   _RimIntensity;
                float  _BumpScale;
                half   _Cutoff;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
