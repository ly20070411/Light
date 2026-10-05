Shader "Hidden/Light/FlatMapVisibleMask"
{
    Properties { _FlatMapSelected ("Selected foreground", Float) = 0 }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _FlatMapSelected;
            struct vertexInput { float4 vertex : POSITION; };
            struct vertexOutput { float4 position : SV_POSITION; };
            vertexOutput vert(vertexInput input)
            { vertexOutput output; output.position = UnityObjectToClipPos(input.vertex); return output; }
            fixed4 frag(vertexOutput input) : SV_Target
            { return fixed4(_FlatMapSelected, _FlatMapSelected, _FlatMapSelected, 1); }
            ENDCG
        }
    }
}
