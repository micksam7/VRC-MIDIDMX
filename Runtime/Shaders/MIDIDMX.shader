Shader "Micca/MIDIDMX"
{
    Properties
    {
        _MainTex ("Pass Through Texture", 2D) = "black" {}
        _MaskingTex ("Pass Through Mask", 2D) = "black" {}
        _MaskingEnable ("Masking Strength", Range(0,1)) = 0
        [KeywordEnum(VRSL, VRSL9, MDMX, MDMX0, VRSLV)] _Mode ("Mode", Int) = 0
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

            #pragma shader_feature_local _MODE_VRSL _MODE_VRSL9 _MODE_MDMX _MODE_MDMX0 _MODE_VRSLV

            #define MDMXSPACINGX 128
            #define MDMXSPACINGY 128

            //old mdmx from somna that tried to keep some VRSL compatibility
            #define MDMX0SPACINGX 13
            #define MDMX0SPACINGY 315

            #define VRSLSPACINGX 13
            #define VRSLSPACINGY 120

            //vrsl vertical mode
            #define VRSLVSPACINGX 13
            #define VRSLVSPACINGY 67.5 //.5 ????

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
            sampler2D _MaskingTex;
            float4 _MainTex_ST;
            float4 _MaskingTex_ST;
            float _MaskingEnable;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            uint getChannel(float2 coord) {
                uint t = 0;

                //todo: fix this macro mess
                #if defined(_MODE_VRSL) || defined(_MODE_VRSL9) || defined(_MODE_VRSLV)
                    #ifdef _MODE_VRSLV
                        const int sx = VRSLVSPACINGX;
                        const int sy = VRSLVSPACINGY;
                    #else
                        const int sx = VRSLSPACINGX;
                        const int sy = VRSLSPACINGY;
                    #endif
                #else
                    #ifdef _MODE_MDMX0
                        const int sx = MDMX0SPACINGX;
                        const int sy = MDMX0SPACINGY;
                    #else
                        const int sx = MDMXSPACINGX;
                        const int sy = MDMXSPACINGY;
                    #endif
                #endif

                uint x = floor(coord.x * sx);
                uint y = floor(coord.y * sy);
                t = y * sx;
                t += x;

                //VRSL compat
                #if !defined(_MODE_MDMX) && !defined(_MODE_MDMX0)
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
                uint block = channel / BLOCKCHANNELS;
                channel -= block * BLOCKCHANNELS;

                [forcecase]
                switch (block) {
                    case 0:
                        col = _Block0[channel];
                        break;
                    case 1:
                        col = _Block1[channel];
                        break;
                    case 2:
                        col = _Block2[channel];
                        break;
                    case 3:
                        col = _Block3[channel];
                        break;
                    case 4:
                        col = _Block4[channel];
                        break;
                    case 5:
                        col = _Block5[channel];
                        break;
                    case 6:
                        col = _Block6[channel];
                        break;
                    case 7:
                        col = _Block7[channel];
                        break;
                    default:
                        col = 0;
                        break;
                }

                col /= 255.;

                return col;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = 0;

                #ifdef _MODE_VRSL9
                    uint channel = getChannel(i.uv);

                    col.r = _Block0[channel] / 255.;

                    channel += 1536;
                    col.g = getBufferBlock(channel);

                    channel += 1536;
                    col.b = getBufferBlock(channel);
                #else
                    uint channel = getChannel(i.uv);
                    col = getBufferBlock(channel);
                #endif

                float mask = tex2Dlod(_MaskingTex,float4(i.uv,0,0)).r * _MaskingEnable;
                col = lerp(col,tex2Dlod(_MainTex,float4(i.uv,0,0)),mask);

                col.a = 1;

                return col;
            }
            ENDCG
        }
    }
}

