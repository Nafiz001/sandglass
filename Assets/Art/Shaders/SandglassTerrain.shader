// World-space procedural surface for the arena: layered sandstone strata,
// wind ripples for sand, grain and bump, all computed from position so big
// primitives never show stretched textures. Lit with URP's PBR path.
Shader "Sandglass/Terrain"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.82, 0.62, 0.44, 1)
        _ShadeColor("Shade Color", Color) = (0.6, 0.42, 0.3, 1)
        _Strata("Strata", Range(0, 1)) = 0.6
        _StrataScale("Strata Per Metre", Float) = 2.2
        _Grain("Grain", Range(0, 1)) = 0.35
        _NoiseScale("Noise Scale", Float) = 1.2
        _Bump("Bump", Range(0, 3)) = 1
        _Ripples("Ripples", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.08
        _BaseDarken("Base Darken", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadeColor;
                half _Strata;
                float _StrataScale;
                half _Grain;
                float _NoiseScale;
                half _Bump;
                half _Ripples;
                half _Smoothness;
                half _BaseDarken;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x),
                         lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                         lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p, int octaves)
            {
                float a = 0.5, s = 0.0;
                for (int k = 0; k < octaves; k++)
                {
                    s += a * vnoise(p);
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }
                return s;
            }

            // Surface height used for the bump: carved strata grooves on stone,
            // wind ripples on sand, fine grain on both.
            float surfaceHeight(float3 p)
            {
                float warp = fbm(p * _NoiseScale * 0.6, 2);
                float band = frac(p.y * _StrataScale + warp * 0.6);
                float groove = smoothstep(0.82, 0.97, band) * (1.0 - smoothstep(0.97, 1.0, band));
                float ripple = sin(dot(p.xz, float2(0.83, 0.55)) * 5.5 + warp * 7.0) * 0.5 + 0.5;
                float grain = vnoise(p * 9.0);
                return -groove * _Strata + ripple * _Ripples * 0.6 + grain * _Grain * 0.35 + warp * 0.4;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 p = i.positionWS;
                float3 n = normalize(i.normalWS);

                // Bump from the height field's gradient, kept tangent to the surface.
                const float e = 0.03;
                float h0 = surfaceHeight(p);
                float3 grad = float3(surfaceHeight(p + float3(e, 0, 0)) - h0,
                                     surfaceHeight(p + float3(0, e, 0)) - h0,
                                     surfaceHeight(p + float3(0, 0, e)) - h0) / e;
                grad -= n * dot(grad, n);
                n = normalize(n - grad * _Bump * 0.08);

                float big = fbm(p * _NoiseScale * 0.25, 3);
                float strataTone = sin(p.y * _StrataScale * 6.2831 + big * 4.0) * 0.5 + 0.5;
                float tone = saturate(big * 1.1 - 0.05 + (strataTone - 0.5) * 0.5 * _Strata);
                float speck = vnoise(p * 23.0);
                half3 albedo = lerp(_ShadeColor.rgb, _BaseColor.rgb, tone);
                albedo *= 1.0 + (speck - 0.5) * 0.18 * _Grain;
                albedo *= lerp(1.0, 0.92, saturate(-h0) * _Strata);

                // Dust and shadow gather at the base of vertical surfaces.
                float vertical = 1.0 - abs(n.y);
                float baseAo = lerp(1.0, 0.45 + 0.55 * saturate(p.y / 1.4), _BaseDarken * vertical);

                InputData inputData = (InputData)0;
                inputData.positionWS = p;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
                inputData.shadowCoord = TransformWorldToShadowCoord(p);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.alpha = 1;
                s.metallic = 0;
                s.smoothness = _Smoothness;
                s.occlusion = baseAo;
                s.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, i.fogFactor);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Universal Render Pipeline/Lit"
}
