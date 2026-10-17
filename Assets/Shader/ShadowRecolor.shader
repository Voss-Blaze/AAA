Shader "ShadowTrace/RecolorWhiteEyes"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlackThreshold ("Black Threshold", Range(0, 1)) = 0.15
        _WhiteThreshold ("White Threshold", Range(0, 1)) = 0.8
        [HideInInspector] _OutlineOnly ("Outline Only", Float) = 0
        [HDR] _OutlineColor ("Outline Color", Color) = (1,0.65,0.12,1)
        _OutlineWidth ("Outline Width (Pixels)", Float) = 10
        _GlowRadius ("Glow Radius (Pixels)", Float) = 24
        _GlowIntensity ("Glow Intensity", Float) = 3
        _GlowOpacity ("Glow Opacity", Range(0,1)) = 0.5
        [HideInInspector] _OutlineOpacity ("Outline Opacity", Float) = 1
        [HideInInspector] _SpriteUVRect ("Sprite UV Rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _RendererColor;
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float _BlackThreshold;
                float _WhiteThreshold;
                float _OutlineOnly;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _GlowRadius;
                float _GlowIntensity;
                float _GlowOpacity;
                float _OutlineOpacity;
                float4 _SpriteUVRect;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #ifdef UNITY_INSTANCING_ENABLED
                    input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteFlip);
                #endif

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                // SpriteRenderer supplies a separate tint for each instance sharing this material.
                output.color = input.color * _RendererColor;

                #ifdef UNITY_INSTANCING_ENABLED
                    output.color *= unity_SpriteColor;
                #endif
                return output;
            }

            half SpriteAlpha(float2 uv)
            {
                half inside = step(_SpriteUVRect.x, uv.x) * step(_SpriteUVRect.y, uv.y) *
                    step(uv.x, _SpriteUVRect.z) * step(uv.y, _SpriteUVRect.w);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * inside;
            }

            half NeighbourAlpha(float2 uv, float radius)
            {
                float2 delta = _MainTex_TexelSize.xy * radius;
                half alpha = SpriteAlpha(uv + float2(delta.x, 0));
                alpha = max(alpha, SpriteAlpha(uv - float2(delta.x, 0)));
                alpha = max(alpha, SpriteAlpha(uv + float2(0, delta.y)));
                alpha = max(alpha, SpriteAlpha(uv - float2(0, delta.y)));
                alpha = max(alpha, SpriteAlpha(uv + delta * 0.7071));
                alpha = max(alpha, SpriteAlpha(uv - delta * 0.7071));
                alpha = max(alpha, SpriteAlpha(uv + float2(delta.x, -delta.y) * 0.7071));
                return max(alpha, SpriteAlpha(uv + float2(-delta.x, delta.y) * 0.7071));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_OutlineOnly > 0.5)
                {
                    half centre = SpriteAlpha(input.uv);
                    half outline = _OutlineWidth > 0 ? saturate(NeighbourAlpha(input.uv, _OutlineWidth) - centre) : 0;
                    half halo = 0;
                    if (_GlowRadius > 0 && _GlowOpacity > 0)
                    {
                        halo = max(halo, saturate(NeighbourAlpha(input.uv, _OutlineWidth + _GlowRadius / 3) - centre) * 0.6);
                        halo = max(halo, saturate(NeighbourAlpha(input.uv, _OutlineWidth + _GlowRadius * 2 / 3) - centre) * 0.3);
                        halo = max(halo, saturate(NeighbourAlpha(input.uv, _OutlineWidth + _GlowRadius) - centre) * 0.1);
                    }
                    half alpha = saturate(max(outline, halo * _GlowOpacity) * _OutlineColor.a * _OutlineOpacity);
                    return half4(_OutlineColor.rgb * max(1, _GlowIntensity), alpha);
                }
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half brightness = dot(source.rgb, half3(0.299, 0.587, 0.114));
                half eyeMask = smoothstep(_BlackThreshold,
                    max(_WhiteThreshold, _BlackThreshold + 0.001), brightness);
                // Apply tint only to dark pixels. Bright eyes stay white, even when selected.
                half3 color = lerp(input.color.rgb, half3(1, 1, 1), eyeMask);
                return half4(color, source.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
