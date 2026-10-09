Shader "MoonLander/SpacecraftOutline"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            sampler2D _MainTex; float4 _MainTex_TexelSize; fixed4 _Color;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                // A small screen-aware footprint keeps thin contours visible when zooming out.
                float2 stepUV=max(_MainTex_TexelSize.xy,fwidth(i.uv)*0.4);
                fixed4 source=tex2D(_MainTex,i.uv);
                for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++)
                    source=max(source,tex2D(_MainTex,i.uv+float2(x,y)*stepUV));
                fixed coverage=saturate((max(source.r,max(source.g,source.b))-0.025)/0.975);
                return fixed4(i.color.rgb, source.a*coverage*i.color.a);
            }
            ENDHLSL
        }
    }
}
