Shader "Mossmere/ReturnBeaconGlow"
{
    Properties
    {
        _Color("Beacon color",Color)=(0.2,1,0.85,1)
        _Beam("Light column",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Beam;
            CBUFFER_END
            Varyings vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output;}
            half4 frag(Varyings input):SV_Target
            {
                float2 uv=floor(input.uv*64)/64;
                float radius=length((uv-.5)*2);
                float pulse=.75+.25*sin(_Time.y*2.5);
                float soft=(1-smoothstep(.05,1,radius))*.13;
                float ring=(1-smoothstep(.025,.09,abs(radius-(.67+.04*sin(_Time.y*2)))))*.85;
                float inner=(1-smoothstep(.02,.055,abs(radius-.39)))*.5;
                float column=pow(saturate(1-abs(uv.x-.5)*2),3)*pow(saturate(1-uv.y),.65)*.48;
                float alpha=lerp((soft+ring+inner)*pulse,column*pulse,step(.5,_Beam));
                return half4(_Color.rgb,alpha*_Color.a);
            }
            ENDHLSL
        }
    }
}
