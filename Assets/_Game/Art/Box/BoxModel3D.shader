Shader "BubbleFruit/BoxModel3D"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Gloss ("Gloss", Range(8,96)) = 38
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };
            fixed4 _Color;
            float _Gloss;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                // Restrained baked-style light: the mesh only supplies a real
                // rotating edge and must still match the flat mobile artwork.
                float3 lightDir = normalize(float3(-0.45, 0.72, -0.54));
                float diffuse = saturate(dot(normal, lightDir));
                float light = lerp(0.72, 1.08, diffuse);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPosition);
                float3 halfDir = normalize(lightDir + viewDir);
                float specular = pow(saturate(dot(normal, halfDir)), _Gloss) * 0.08;
                float rim = pow(1.0 - saturate(dot(normal, viewDir)), 3.0) * 0.05;
                fixed4 color = _Color;
                color.rgb = saturate(color.rgb * light + specular + rim);
                return color;
            }
            ENDCG
        }
    }
}
