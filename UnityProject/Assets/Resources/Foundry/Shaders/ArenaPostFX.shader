Shader "Hidden/Foundry/PostFX"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Vignette;
            float _Saturation;
            float _Contrast;
            float _Exposure;
            float3 _Tint;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                float luma = dot(color.rgb, float3(0.299, 0.587, 0.114));
                color.rgb = lerp(float3(luma, luma, luma), color.rgb, _Saturation);
                color.rgb = (color.rgb - 0.5) * _Contrast + 0.5;
                color.rgb = max(0, color.rgb) * _Tint * _Exposure;
                color.rgb = (color.rgb * (2.51 * color.rgb + 0.03)) / (color.rgb * (2.43 * color.rgb + 0.59) + 0.14);
                color.rgb = saturate(color.rgb);
                float2 d = i.uv - 0.5;
                color.rgb *= saturate(1.0 - dot(d, d) * _Vignette);
                return color;
            }
            ENDCG
        }
    }
}
