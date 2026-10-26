Shader "How2Shop/NewBakeryFreezer/Glass"
{
    Properties { _Color("Glass tint",Color)=(.28,.65,.85,.16) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct A { float4 vertex:POSITION; };
            struct V { float4 position:SV_POSITION; };
            V vert(A a){V o;o.position=TransformObjectToHClip(a.vertex.xyz);return o;}
            half4 frag(V i):SV_Target{return _Color;}
            ENDHLSL
        }
    }
}
