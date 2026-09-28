
on windows 11 and vrc unity-6


0-32:

mostly control codes

most don't add to buffer

8, 10, 13, 32 are the only ones that work



32-55295:

perfectly fine, 1:1



55296-57344:

always returns 65533



57344-65535:

works



65535+:

always returns 65535









message start: 0xFFFD

message end: 0xFFFF

all other values: value + 0x0400 to bring it completely out of possible keyboard space





'packet' format:
\[start]\[dmx start position from 0 for U1:C1, to 16383 for U31:C512]\[size of message, up to 1024]((dmx values))\[end]

