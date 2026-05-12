Shader "Custom/2D/SpriteVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color           ("Tint", Color)              = (1,1,1,1)
        _VignetteStrength("Vignette Strength", Range(0,1)) = 0.5
        _VignetteRadius  ("Vignette Radius",  Range(0,1)) = 0.75
        _VignetteSoftness("Vignette Softness",Range(0.001,1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "RenderType"       = "Transparent"
            "IgnoreProjector"  = "True"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas"= "True"
            "RenderPipeline"   = "UniversalPipeline"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _VignetteStrength;
                float  _VignetteRadius;
                float  _VignetteSoftness;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.color       = IN.color * _Color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // distance from sprite center, in UV space (0 at center, ~0.707 at corner)
                float2 centered = IN.uv - 0.5;
                float  dist     = length(centered);

                // 0 inside the radius, smoothly rising to 1 past it
                float vignette = smoothstep(_VignetteRadius,
                                            _VignetteRadius + _VignetteSoftness,
                                            dist);

                // darken RGB; leave alpha alone so the sprite's shape isn't affected
                tex.rgb *= 1.0 - vignette * _VignetteStrength;
                return tex;
            }
            ENDHLSL
        }
    }
}
