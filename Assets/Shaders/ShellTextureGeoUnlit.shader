Shader "Custom/ShellTextureGeoUnlit"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            struct DrawVertex
            {
               float3 position;
               float3 normal;
               float2 uv;
               float4 color;
            };

            struct DrawTriangle
            {
               DrawVertex drawVerticies[3];
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            StructuredBuffer<DrawTriangle> _DrawTrianglesBuffer : register(t0);

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o;
                DrawTriangle tri = _DrawTrianglesBuffer[vertexID / 3];
                DrawVertex v = tri.drawVerticies[vertexID % 3];

                o.vertex = UnityObjectToClipPos(v.position);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv).x;
                clip(tex - i.color.x);
                return _Color * tex;
            }
            ENDCG
        }
    }
}
