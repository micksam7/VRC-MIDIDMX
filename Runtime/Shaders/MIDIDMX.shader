Shader "Micca/MIDIDMX"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [KeywordEnum(VRSL, VRSL9, MDMX)] _Mode ("Mode", Int) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #pragma shader_feature_local _MODE_VRSL _MODE_VRSL9 _MODE_MDMX

            #define MDMXSPACINGX 128
            #define MDMXSPACINGY 128

            #define VRSLSPACINGX 13
            #define VRSLSPACINGY 120

            #define BLOCKCHANNELS 2048

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            uint getChannel(float2 coord) {
                uint t = 0;

                #if defined(_MODE_VRSL) || defined(_MODE_VRSL9)
                const int sx = VRSLSPACINGX;
                const int sy = VRSLSPACINGY;
                #else
                const int sx = MDMXSPACINGX;
                const int sy = MDMXSPACINGY;
                #endif

                uint x = floor(coord.x * sx);
                uint y = floor(coord.y * sy);
                t = y * sx;
                t += x;

                //VRSL compat
                #if defined(_MODE_VRSL) || defined(_MODE_VRSL9)
                t -= floor(t / 520) * 8; //520 channel seperation instead of 512
                #endif
                
                return t;
            }

            cbuffer Block0 {
                float _Block0[BLOCKCHANNELS];
            }

            cbuffer Block1 {
                float _Block1[BLOCKCHANNELS];
            }

            cbuffer Block2 {
                float _Block2[BLOCKCHANNELS];
            }

            cbuffer Block3 {
                float _Block3[BLOCKCHANNELS];
            }

            cbuffer Block4 {
                float _Block4[BLOCKCHANNELS];
            }

            cbuffer Block5 {
                float _Block5[BLOCKCHANNELS];
            }

            cbuffer Block6 {
                float _Block6[BLOCKCHANNELS];
            }

            cbuffer Block7 {
                float _Block7[BLOCKCHANNELS];
            }

            float getBufferBlock(uint channel) {
                float col = 0;
                [branch]
                if (channel + 1 <= BLOCKCHANNELS * 1) {
                    col = _Block0[channel] / 255.;
                } else if (channel + 1 <= BLOCKCHANNELS * 2) {
                    channel -= BLOCKCHANNELS;
                    col = _Block1[channel] / 255.;
                } else if (channel + 1 <= BLOCKCHANNELS * 3) {
                    channel -= BLOCKCHANNELS * 2;
                    col = _Block2[channel] / 255.;
                } else if (channel + 1 <= BLOCKCHANNELS * 4) {
                    channel -= BLOCKCHANNELS * 3;
                    col = _Block3[channel] / 255.;
                } else if (channel + 1 <= BLOCKCHANNELS * 5) {
                    channel -= BLOCKCHANNELS * 4;
                    col = _Block4[channel] / 255.;
                } else if (channel + 1 <= BLOCKCHANNELS * 6) {
                    channel -= BLOCKCHANNELS * 5;
                    col = _Block5[channel] / 255.;
                } else if (channel + 1 <= BLOCKCHANNELS * 7) {
                    channel -= BLOCKCHANNELS * 6;
                    col = _Block6[channel] / 255.;
                } else if (channel + 1 <= BLOCKCHANNELS * 8) {
                    channel -= BLOCKCHANNELS * 7;
                    col = _Block7[channel] / 255.;
                } else {
                    col = 0;
                }
                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = 0;

                #if defined(_MODE_VRSL) || defined(_MODE_MDMX)
                uint channel = getChannel(i.uv);
                col = getBufferBlock(channel);
                #endif

                #ifdef _MODE_VRSL9
                uint channel = getChannel(i.uv);

                col.r = _Block0[channel] / 255.;

                channel += 1536;
                col.g = getBufferBlock(channel);

                channel += 1536;
                col.b = getBufferBlock(channel);
                #endif

                col.a = 1;

                return col;
            }
            ENDCG
        }
    }
}

