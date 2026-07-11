Shader "Custom/2D/MorningSunShaft"
{
    // Drop this shader on a Material, apply that Material to a Quad in your scene.
    // Size and position the Quad so its left edge aligns with your window.
    // Set AspectRatio to the Quad's width / height in world units.
    // Use Blend One One (additive) — rays glow on top of any scene below.

    Properties
    {
        _RayColor      ("Ray Color",              Color)              = (1, 0.87, 0.31, 1)
        _RayBrightness ("Ray Brightness",         Range(0, 3))        = 1.0
        _BreathSpeed   ("Breath Speed",           Range(0.1, 3))      = 1.0
        _DustOpacity   ("Dust Opacity",           Range(0, 1))        = 0.55
        _DustSize      ("Dust Size (UV units)",   Range(0.001, 0.03)) = 0.006
        _AmbientGlow   ("Ambient Glow",           Range(0, 1))        = 0.18
        _WindowY       ("Window Y (0=top, UV)",   Range(0, 0.5))      = 0.19
        _AspectRatio   ("Quad Aspect Ratio (W/H)",Float)              = 3.33
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline"    = "UniversalPipeline"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RayColor;
                float  _RayBrightness;
                float  _BreathSpeed;
                float  _DustOpacity;
                float  _DustSize;
                float  _AmbientGlow;
                float  _WindowY;
                float  _AspectRatio;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // sUV: aspect-ratio-corrected UV where (0,0) is the top-left corner.
            // sUV.x = uv.x * AspectRatio, sUV.y = 1 - uv.y
            // Both axes are in units of "scene height fractions" so the space is isotropic
            // — a circle in sUV looks like a circle on screen.

            // Ray params:
            //   originY  – Y of the ray's left-edge anchor in sUV (0 = very top)
            //   angleDeg – screen-space angle from horizontal, degrees (positive = down)
            //   halfH    – half-thickness of the ray core, sUV units
            //   blurH    – softness radius beyond halfH, sUV units
            //   xReach   – fraction of Quad width the ray reaches (0..1)
            //   brightness – peak alpha contribution
            //   delay    – breathe animation phase offset, seconds
            float ComputeRay(float2 sUV, float originY, float angleDeg,
                             float halfH, float blurH, float xReach,
                             float brightness, float delay)
            {
                float  theta    = radians(angleDeg);
                float2 dir      = float2(cos(theta), sin(theta));
                float2 perpDir  = float2(-dir.y, dir.x);

                float2 d        = sUV - float2(0.0, originY);
                float  along    = dot(d, dir);
                float  pDist    = abs(dot(d, perpDir));

                float  xLimit   = xReach * _AspectRatio;
                float  alongMax = xLimit / max(cos(theta), 0.001);

                float gate     = step(0.001, along) * step(along, alongMax);
                float cross    = saturate(1.0 - smoothstep(halfH * 0.5, halfH + blurH, pDist));
                float fadeOut  = 1.0 - smoothstep(alongMax * 0.40, alongMax, along);
                float fadeIn   = smoothstep(0.0, 0.010, along);
                float breath   = saturate(0.8 + 0.2 * cos((_Time.y - delay) * HALF_PI * _BreathSpeed));

                return brightness * cross * fadeOut * fadeIn * breath * gate;
            }

            // Dust mote: a tiny point drifting from startSUV to startSUV+driftSUV over dur seconds.
            // Positions are in sUV space. Opacity envelope: fades in at 12%, holds, fades at 85%.
            float DustMote(float2 sUV, float2 startSUV, float2 driftSUV,
                           float dur, float delay, float size)
            {
                float  phase = frac((_Time.y - delay) / dur);
                float2 pos   = startSUV + driftSUV * phase;
                float  dist  = length(sUV - pos);
                float  spot  = 1.0 - smoothstep(0.0, size, dist);
                float  env   = smoothstep(0.0, 0.12, phase) * (1.0 - smoothstep(0.85, 1.0, phase));
                return spot * env;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Unity Quad UV: (0,0)=bottom-left. Remap to (0,0)=top-left, then apply AR.
                float2 uv  = float2(IN.uv.x, 1.0 - IN.uv.y);
                float  AR  = _AspectRatio;
                float2 sUV = float2(uv.x * AR, uv.y);

                // ── RAYS ──
                // Six overlapping beams from left edge, tuned to match the v5 mockup.
                // angles 26-38°, brightness 0.18-0.38, varying thickness and softness.
                float rays = 0.0;
                rays += ComputeRay(sUV, 0.162, 26.0, 0.067, 0.033, 0.75, 0.22, 0.0);
                rays += ComputeRay(sUV, 0.162, 28.0, 0.029, 0.010, 0.68, 0.38, 0.4);
                rays += ComputeRay(sUV, 0.224, 32.0, 0.043, 0.019, 0.60, 0.25, 0.8);
                rays += ComputeRay(sUV, 0.176, 30.0, 0.014, 0.007, 0.55, 0.32, 1.2);
                rays += ComputeRay(sUV, 0.267, 35.0, 0.057, 0.038, 0.50, 0.18, 0.6);
                rays += ComputeRay(sUV, 0.257, 38.0, 0.019, 0.010, 0.42, 0.20, 1.6);
                rays = saturate(rays * _RayBrightness);

                // ── AMBIENT GLOW (radial bloom at the window source point) ──
                float2 winSUV = float2(0.0, _WindowY);
                float  gDist  = length(sUV - winSUV);
                float  glow   = exp(-gDist * 3.0) * _AmbientGlow;
                glow *= saturate(0.8 + 0.2 * cos(_Time.y * HALF_PI * _BreathSpeed));

                // ── DUST MOTES (12 motes) ──
                // All positions in sUV = pixel_coord / scene_height.
                // Source: v5 mockup (700x210 scene). sUV = (px_x/210, px_y/210).
                // Each mote drifts ~30° down-right across the ray bundle.
                float dSize = _DustSize;
                float dust  = 0.0;
                dust += DustMote(sUV, float2(0.038, 0.133), float2(0.333, 0.190),  9.0,  0.0, dSize);
                dust += DustMote(sUV, float2(0.119, 0.152), float2(0.405, 0.233), 11.0,  1.5, dSize);
                dust += DustMote(sUV, float2(0.238, 0.181), float2(0.310, 0.181),  8.0,  3.2, dSize);
                dust += DustMote(sUV, float2(0.067, 0.114), float2(0.429, 0.248), 12.0,  0.7, dSize);
                dust += DustMote(sUV, float2(0.181, 0.200), float2(0.286, 0.167), 10.0,  4.8, dSize);
                dust += DustMote(sUV, float2(0.333, 0.210), float2(0.262, 0.152),  9.5,  2.1, dSize);
                dust += DustMote(sUV, float2(0.095, 0.171), float2(0.371, 0.214), 13.0,  6.0, dSize);
                dust += DustMote(sUV, float2(0.262, 0.143), float2(0.343, 0.200),  8.5,  1.0, dSize);
                dust += DustMote(sUV, float2(0.429, 0.229), float2(0.238, 0.138), 11.0,  3.5, dSize);
                dust += DustMote(sUV, float2(0.200, 0.124), float2(0.381, 0.219), 10.0,  7.2, dSize);
                dust += DustMote(sUV, float2(0.048, 0.219), float2(0.324, 0.186), 14.0,  2.8, dSize);
                dust += DustMote(sUV, float2(0.371, 0.171), float2(0.276, 0.157),  9.0,  5.5, dSize);
                dust = saturate(dust * _DustOpacity);

                // ── OVERALL FADE ──
                // Effect dissolves from full intensity at the window side (uv.x=0)
                // to absolute zero at the far edge (uv.x=1). Curve starts at 15%
                // so the bright source area is left untouched.
                float overallFade = 1.0 - smoothstep(0.15, 1.0, uv.x);
                overallFade = overallFade * overallFade; // square for a quicker tail

                // ── COMBINE ──
                float3 rayLight  = _RayColor.rgb * (rays + glow);
                float3 dustLight = float3(1.0, 0.961, 0.706) * dust;
                return half4((rayLight + dustLight) * overallFade, 1.0);
            }
            ENDHLSL
        }
    }
}
