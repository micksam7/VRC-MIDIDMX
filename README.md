# VRC-MIDIDMX
A method for implementing realtime DMX control into a VRChat world over MIDI interfaces.

Supports [VRSL](https://github.com/AcChosen/VR-Stage-Lighting), [VRSL 9 Universe Mode](https://github.com/AcChosen/VR-Stage-Lighting), and MDMX

## For World Builders
When a compatible gridnode connects, MIDIDMX automatically replaces the DMX texture with it's own.

If you're using [VideoTXL](https://github.com/vrctxl/VideoTXL), drop the prefab into scene and upload.

Other setups may require a toggle or another method. Please submit an issue or PR if you run into a case like this!

## For light programmers
Requires a compatible gridnode, such as [HNode](https://github.com/Happyrobot33/HNode), and a midi loopback.

For loopback, [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) with **feedback detection turned off** is recommended.

Ensure you have VRChat launched with the launch option `--midi=` set to your loopback device.
