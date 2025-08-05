# MIDIDMX Protocol

MIDIDMX is an absolute reckless abuse of MIDI as a transmission medium. Here be dragons.

## Basic Communication Principle

Currently, MIDI is one of the only ways to get data into a world in real time in stock VRChat. (OSC support in worlds is .. basically non-existant.)

MIDI control in VRChat is intended for controlling in-world instruments and other data, as such is really only limited to low bandwidth, low latency applications.

Here we're using it as a low latency, _somewhat_ high bandwidth application. As such, it's very, _very_ easy to overflow the MIDI buffer. And currently overflowing it can [cause crashes](https://feedback.vrchat.com/bug-reports/p/sdk-vrcportmidi-index-outside-of-bounds-error).

Don't let that canny title fool you, it's not just an exception, it **causes client crashes** and strange client behavior.

As such, buffer management is **critically important**.

Data out of a world is more complicated. We have a few small options like Spout and maybe avatar contacts with OSC stuff, but the easiest and most fool-proof method is just writing to the VRChat Debug log (unfortunately).

## Buffer Management

The max buffer size of VRChat is 128 MIDI _messages_. Anything after that produces data loss and _possible client instability_.

MIDIDMX is programmed to write `MIDIREADY` to the VRChat debug log every time a MidiControlMessage is sent with a channel of `15`, message of `127`, and value of `127`. This is our _watchdog message_.

The connecting application _must_ watch the log and pause sending more data until the debug log contains a new `MIDIREADY` line.

Generally 100 MIDI messages is a safe amount before you should issue a watchdog message and pause data flow.

## MIDIDMX Messages

MIDIDMX uses MIDI Note On and MIDI Note Off to send data, and uses MIDI Control messages for general house keeping and flow control.
VRChat does not care if a Note On message is never followed by a Note Off message.

MIDI messages are limited to a 4-bit _Channel_ value, a 7-bit _Note_ value, and a 7-bit _velocity_ value.

We can also get 1 additional bit by using Note On to prepend a `0` to our message, and Note Off to prepend a `1`. This gives us 19 total bits to work with per message.

We take those 19 bits and split it into a 11 bit Channel, and an 8 bit Value. This gives us 2048 possible DMX Channels (more with bank switching) with a full DMX Value.

The implementation is best done in bitwise operations, expressed as code:
```C#
int dmxChannel; //input DMX channel [with universes are combined in]
int dmxValue; //input DMX value

// Goal:
// c is channel
// v is value
// Message: cccccccccccvvvvvvvv

// if True, this is a Note Off event ( Cccccccccccvvvvvvvv )
bool midiNoteType = dmxChannel > 1024 ? true : false;
// shift channel 6 bits to the left and limit to only the remaining 4 bits. ( cCCCCccccccvvvvvvvv )
int midiChannel = ((dmxChannel >> 6) & 0xF);
// shift channel 1 bit to the right, limit to 6 bits; shift value 7 bits to the left and limit to 1 bit; add both together
// ( cccccCCCCCCVvvvvvvv )
int midiNoteNumber = (((dmxChannel << 1) & 0x7F) + ((dmxValue >> 7) & 0x1));
// limit value to 7 bits. ( cccccccccccvVVVVVVV )
int midiVelocity = (dmxValue & 0x7F); 
```

## Control Messages and Knocking

Control Messages are standard MIDI messages, limited to the very last channel `15` and very last note `127`.
The value/velocity is the only part of the message that changes.

### Value `0` thru `7` - Bank switching
If you have more than 4 Universes of DMX (2048 Channels), you must utilize bank switching. Each bank is 2048 channels wide and selectable by sending 0 through 7 to select the bank (for 8 banks total).
Each bank switch will print `MIDIBLOCK` into the VRChat debug log, followed by the bank number, to confirm the bank switch.
You should ensure bank 0 is selected upon connection.

### Value `100` - Clear world buffer.
Sending this message will clear the entire saved buffer of MIDIDMX. Use this if you need to blank the grid. It's good practice to send this upon connection.

### Value `127` - Watchdog/Acknowledge
Sending `127` will print a `MIDIREADY` message into the VRChat debug log. Every block of data you send should end with this message. Use this to ensure the buffer is clear before sending more data.

### Knocking / Enable MIDIDMX
MIDIDMX uses a _Knock_ to indicate that a connected device is a MIDIDMX sender and not just a random MIDI device. This keeps worlds from behaving unpredictably as well as eliminate the need for an in-world toggle.
The knock sequence must be sent as follows: `101`, `120`, `107`

After knocking, you should _Switch the Bank_ to 0, _Cear the World Buffer_, and send a _Watchdog_ message to be sure it's clear to send data.

## Implementations and Notes
[HNode has a pretty good implementation](https://github.com/Happyrobot33/HNode/blob/main/Assets/Plugin/Exporters/MIDIDMX.cs) if you want a starting point.
