Shader "How2Shop/Shelf Items/Picture"
{
 Properties { _Artwork("Picture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   Tags { "LightMode"="SRPDefaultUnlit" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_Artwork); SAMPLER(sampler_Artwork);
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   V vert(A a){ V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color*_Color;return o; }
   half4 frag(V i):SV_Target { return SAMPLE_TEXTURE2D(_Artwork,sampler_Artwork,i.uv)*i.color; }
   ENDHLSL
  }
 }
}

