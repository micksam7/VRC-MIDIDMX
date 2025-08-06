![MIDIDMX, of screaming really loudly over MIDI.](Runtime/Propaganda/logo-cropped.png)
A method for implementing local realtime DMX control into a VRChat world over MIDI interfaces.

Currently this project assists local light programming and is unsynced. You'll still need to do the usual VRSL video stream for everyone else. A synced mode may come later if [it's requested](https://github.com/micksam7/VRC-MIDIDMX/issues/2).

Supports [VRSL](https://github.com/AcChosen/VR-Stage-Lighting), [VRSL 9 Universe Mode](https://github.com/AcChosen/VR-Stage-Lighting), and MDMX.

For gridnode developers and the curious, [the protocol is documented here](PROTOCOL.md).

## For World Builders
### ➡️ [Add to Creator Companion](https://vpm.micksam7.com/) ⬅️

When a compatible gridnode connects, MIDIDMX automatically replaces the DMX texture with it's own.

If you're using [VideoTXL](https://github.com/vrctxl/VideoTXL), drop the prefab into scene and upload.

Other setups may require a toggle or another method. Please submit an issue or PR if you run into a case like this!

## For Light Programmers
Requires a compatible gridnode, such as [HNode](https://github.com/Happyrobot33/HNode), and a midi loopback.

For loopback, [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) with **feedback detection turned off** is recommended.

Ensure you have VRChat launched with the launch option `--midi=` set to your loopback device.

## Udon API

There's a small API for getting the current connected state of MIDIDMX.

You can get a callback when a connection is made or broken with:

`mididmx._Register(this,_nameof(callbackFunctionHere))`

And get the current status with:

`mididmx.GetState();`

## Frequently Asked Questions

### How do I use this in editor?
If your gridnode supports editor log watching, you can test in editor by going to `VRChat SDK -> Utilities -> Midi` and picking your input MIDI device.

### MIDI isn't working!
- Ensure you have your midi loopback device or software enabled before launching VRChat and your gridnode software.
- Make sure you spelled your device name correctly in the [VRChat launch options](https://docs.vrchat.com/docs/launch-options).
- If you're using a software loopback, make sure feedback detection is **disabled**. Some applications do not support disabling this. [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) does.
