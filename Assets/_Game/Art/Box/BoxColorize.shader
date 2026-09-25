Shader "BubbleFruit/BoxColorize"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv);
                fixed luminance = dot(source.rgb, fixed3(0.299, 0.587, 0.114));
                // Preserve the sculpted bevels without bleaching bright colors
                // into the neon-white look of the old boxes.
                // The source art is a clean value mask. Keep the tint saturated:
                // shadows define the rim/slots but must never turn the box muddy.
                // A wider value range makes the inner tray, front wall and
                // bevel highlights read as separate sculpted planes.
                // Strong, clean value separation: upper rims stay bright while
                // recessed cavity and bottom depth become decisively darker.
                fixed shapedLuminance = smoothstep(0.08, 0.90, luminance);
                fixed shade = lerp(0.50, 1.30, shapedLuminance);
                fixed directional = saturate((1.0 - input.uv.x) * 0.55 + input.uv.y * 0.45);
                shade *= lerp(0.88, 1.13, directional);
                shade *= lerp(0.82, 1.02, smoothstep(0.08, 0.38, input.uv.y));
                fixed3 colored = input.color.rgb * shade;
                fixed highlight = smoothstep(0.72, 1.0, luminance);
                colored = lerp(colored, fixed3(1,1,1), highlight * 0.22);
                fixed alpha = source.a * input.color.a;
                return fixed4(colored * alpha, alpha);
            }
            ENDCG
        }
    }
}
