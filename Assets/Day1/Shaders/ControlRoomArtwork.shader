Shader "Emerge/Day1/ControlRoomArtwork"
{
    Properties
    {
        _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OcclusionAlpha ("Occlusion alpha", Range(0,1)) = 1
        _RemoveWhite ("Remove white matte", Float) = 1
        _Backdrop ("Blurred backdrop", Float) = 0
        _BlurRadius ("Blur radius", Range(0,.1)) = .025
        _OpenNorthDoor ("Open north passage", Float) = 0
        _UseOcclusionFocus ("Soft local fade", Float) = 0
        _OcclusionFocus ("Player silhouette center and radii", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            float _OcclusionAlpha, _RemoveWhite, _Backdrop, _BlurRadius, _OpenNorthDoor, _UseOcclusionFocus;
            float4 _OcclusionFocus;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float2 world:TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy; return o;
            }
            float matte(float3 c)
            {
                // Samples are linear in the project's Linear color space.
                return 1-smoothstep(.86,.98,min(c.r,min(c.g,c.b)));
            }
            fixed4 frag(v2f i):SV_Target
            {
                float4 c=tex2D(_MainTex,i.uv);
                if (_Backdrop > .5)
                {
                    float3 sum=0; float total=0;
                    [unroll] for(int x=-2;x<=2;x++)
                    [unroll] for(int y=-2;y<=2;y++)
                    {
                        float4 s=tex2D(_MainTex,i.uv+float2(x,y)*_BlurRadius);
                        float w=exp(-.5*(x*x+y*y))*matte(s.rgb);
                        sum+=s.rgb*w; total+=w;
                    }
                    c.rgb=lerp(float3(.034,.044,.065),sum/max(total,.0001)*.32,.65);
                    c.a=saturate(total*.22)*.72;
                }
                else
                {
                    c.a*=lerp(1,matte(c.rgb),_RemoveWhite);
                    if (_OpenNorthDoor > .5)
                    {
                        float2 pixel=float2(i.uv.x*5000,(1-i.uv.y)*6000);
                        // Keep the original arch/frame; open only the brown leaf for the passage.
                        if(pixel.x>2310 && pixel.x<2744 && pixel.y>1166 && pixel.y<1635) c.a=0;
                        if(pixel.x>535 && pixel.x<594 && pixel.y>3300 && pixel.y<3790) c.a=0;
                        if(pixel.x>4398 && pixel.x<4465 && pixel.y>3298 && pixel.y<3845) c.a=0;
                        if(pixel.x>2140 && pixel.x<2850 && pixel.y>5460 && pixel.y<5550) c.a=0;
                        if(pixel.x>1118 && pixel.x<1304 && pixel.y>1780 && pixel.y<2110) c.a=0;
                    }
                }
                c*=i.color;
                float focus=1-smoothstep(.55,1,length((i.world-_OcclusionFocus.xy)/max(_OcclusionFocus.zw,.01)));
                c.a*=lerp(_OcclusionAlpha,lerp(1,_OcclusionAlpha,focus),_UseOcclusionFocus); return c;
            }
            ENDCG
        }
    }
}
