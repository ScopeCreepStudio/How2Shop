Shader "How2Shop/Sprites/Editable Packaging Colour"
{
 Properties
 {
  [MainColor] _PackagingColor("Packaging colour", Color) = (1,0,0,1)
  _Strength("Recolour amount", Range(0,1)) = 1
  _SourceColor("Original packaging colour (selection)", Color) = (1,0,0,1)
  _HueRange("Colour selection width", Range(0.01,0.3)) = .10
  _MinSaturation("Protect neutral labels", Range(0,0.5)) = .16
  _MinValue("Protect dark outlines", Range(0,0.3)) = .025
  [HideInInspector] _Artwork("Artwork", 2D) = "white" {}
  [HideInInspector] _MainTex("Sprite", 2D) = "white" {}
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True"}
  Blend SrcAlpha OneMinusSrcAlpha
  Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_Artwork); SAMPLER(sampler_Artwork);
   CBUFFER_START(UnityPerMaterial)
   half4 _PackagingColor,_SourceColor;
   half _Strength,_HueRange,_MinSaturation,_MinValue;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
   float3 ToHSV(float3 c){float4 K=float4(0,-1.0/3,2.0/3,-1);float4 p=lerp(float4(c.bg,K.wz),float4(c.gb,K.xy),step(c.b,c.g));float4 q=lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));float d=q.x-min(q.w,q.y);return float3(abs(q.z+(q.w-q.y)/(6*d+1e-10)),d/(q.x+1e-10),q.x);}
   float3 ToRGB(float3 c){float3 p=abs(frac(c.xxx+float3(0,2.0/3,1.0/3))*6-3);return c.z*lerp(float3(1,1,1),saturate(p-1),c.y);}
   half4 frag(V i):SV_Target{
    half4 tex=SAMPLE_TEXTURE2D(_Artwork,sampler_Artwork,i.uv);
    float3 hsv=ToHSV(tex.rgb),source=ToHSV(_SourceColor.rgb),target=ToHSV(_PackagingColor.rgb);
    float delta=abs(hsv.x-source.x);delta=min(delta,1-delta);
    float mask=(1-smoothstep(_HueRange*.65,_HueRange,delta))*smoothstep(_MinSaturation,_MinSaturation+.08,hsv.y)*smoothstep(_MinValue,_MinValue+.03,hsv.z)*_Strength;
    float3 changed=float3(frac(hsv.x+target.x-source.x),saturate(hsv.y*target.y/max(source.y,.001)),saturate(hsv.z*target.z/max(source.z,.001)));
    tex.rgb=lerp(tex.rgb,ToRGB(changed),mask);
    return tex*i.color;
   }
   ENDHLSL
  }
 }
}
