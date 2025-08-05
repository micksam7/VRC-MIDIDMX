using UdonSharp;
using UnityEngine;
using VRC.SDK3.Midi;
using VRC.SDKBase;
using VRC.Udon;
using System.Collections.Generic;
using System;

//micca code
//this udon was written to keep the number of non-extern ops low
//so most of the work is shuffled off to a shader

public enum MIDIDMXMode : int
{
    VRSL,
    VRSLNineUniverse,
    MDMX
};

//Attempts to execute after video players [to replace DMX texture]
//If this doesn't work, you may need to come up with your own solution [definitely ping me about it though!]
[DefaultExecutionOrder(1)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MIDIDMX : UdonSharpBehaviour
{
    public MIDIDMXMode mode = 0;
    public RenderTexture DMXTexture;
    public Material MIDIDMXRenderMat;

    private int dataBlock = 0;

    private float lastUpdate = 0;

    bool state = false;
    bool previousState = false;
    int knockState = 0;

    Component[] eventObjects = new Component[0];
    string[] eventCallbacks = new string[0];

    //float for final shader
    [System.NonSerialized]
    private float[][] data = {
        new float[2048], new float[2048], new float[2048], new float[2048],
        new float[2048], new float[2048], new float[2048], new float[2048], };

    void Start()
    {
        MIDIDMXRenderMat.SetInt("_Mode", (int)mode);
    }

    /// <summary>
    /// Gets current state of MIDIDMX.
    /// True if MIDIDMX is active.
    /// False if MIDIDMX is inactive or lost the data stream.
    /// </summary>
    /// <returns></returns>
    public bool GetState()
    {
        return state;
    }

    /// <summary>
    /// Register a callback when MIDIDMX's state changes.
    /// Use GetState() to check what state MIDIDMX is currently in.
    /// </summary>
    /// <param name="obj">UdonBehavior to call back to</param>
    /// <param name="callback">Function to call within that UdonBehavior</param>
    public void _RegisterEvent(Component obj, string callback)
    {
        eventObjects = Add(eventObjects, obj);
        eventCallbacks = Add(eventCallbacks, callback);
    }

    void ProcessEvents()
    {
        for (int i = 0; i < eventObjects.Length; i++)
        {
            if (!eventObjects[i]) continue;
            UdonBehaviour behaviour = (UdonBehaviour) eventObjects[i];
            if (Utilities.IsValid(behaviour))
                behaviour.SendCustomEvent(eventCallbacks[i]);
        }
    }

    //midi packet: 4 bit + 7 bit + 7 bit - decode into: 10 bit address, 8 bit value
    //we do weird shit here to reduce the number of midi commands going into client
    //because of _fun_ buffer issues [see below in midicontrolchange]
    public override void MidiNoteOn(int channel, int number, int velocity)
    {
        int address = (channel << 6) + ((number >> 1) & 0xFF);
        velocity += (number << 7) & 0xFF;
        //Debug.Log($"MIDION: {address} = {velocity}");
        data[dataBlock][address] = velocity;
    }

    //other half of the block
    public override void MidiNoteOff(int channel, int number, int velocity)
    {
        int address = (channel << 6) + ((number >> 1) & 0xFF) + 1024;
        velocity += (number << 7) & 0xFF;
        //Debug.Log($"MIDIOFF: {address} = {velocity}");
        data[dataBlock][address] = velocity;
    }

    public override void MidiControlChange(int channel, int number, int value)
    {
        //all control messages are channel 15 and note 127
        if (channel != 15 || number != 127) return;

        //knocking to start MIDIDMX
        if (!state)
        {
            if (knockState == 0 && value == 101)
            {
                knockState = 1;
            }
            else if (knockState == 1 && value == 120)
            {
                knockState = 2;
            }
            else if (knockState == 2 && value == 107)
            {
                knockState = 3;
                state = true;
                Debug.Log("[MIDIDMX] state and ready.");
                dataBlock = 0;
                ClearChannels();
            }
            else
            {
                knockState = 0;
                return;
            }
        }

        //bank swapping for more than 2k of channels [requires 9 Universe VRSL or MDMX]
        if (value >= 0 && value < 8)
        {
            dataBlock = value;
            Debug.Log("MIDIBLOCK" + value.ToString());
        }

        //clear all channels
        if (value == 100)
        {
            ClearChannels();
        }

        //Because of a _very_ fun bug in the portmidi C# adapter [that VRC uses], we need this to ensure we can keep sending data
        //So we spam the logs and the grid reads it to make sure vrc is ready for more, and the client is still alive
        //Otherwise, we can overflow the midi buffer and cause a very nasty client crash. :)
        if (value == 127)
        {
            lastUpdate = Time.fixedTime;
            Debug.Log("MIDIREADY");
        }
    }

    private void ClearChannels()
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = new float[2048];
        }
    }

    void Update()
    {
        //Only update if we're getting the ping packet
        //Otherwise we release the texture [assuming script order is right :)]
        if (state && lastUpdate > Time.fixedTime - 5)
        {
            //unrolllllllllllllllllll
            //tho those ids should be ints tbh
            MIDIDMXRenderMat.SetFloatArray("_Block0", data[0]);
            MIDIDMXRenderMat.SetFloatArray("_Block1", data[1]);
            MIDIDMXRenderMat.SetFloatArray("_Block2", data[2]);
            MIDIDMXRenderMat.SetFloatArray("_Block3", data[3]);
            MIDIDMXRenderMat.SetFloatArray("_Block4", data[4]);
            MIDIDMXRenderMat.SetFloatArray("_Block5", data[5]);
            MIDIDMXRenderMat.SetFloatArray("_Block6", data[6]);
            MIDIDMXRenderMat.SetFloatArray("_Block7", data[7]);

            VRCGraphics.Blit(null, DMXTexture, MIDIDMXRenderMat); //replaces the video texture
        }
        else
        {
            state = false;
            knockState = 0;
        }

        if (previousState != state)
        {
            previousState = state;
            ProcessEvents();
        }
    }



    //util functions
    private string[] Add(string[] inputArray, string toAdd)
    {
        string[] output = new string[inputArray.Length + 1];
        Array.Copy(inputArray, output, inputArray.Length);
        output[inputArray.Length] = toAdd;

        return output;
    }

    private Component[] Add(Component[] inputArray, Component toAdd)
    {
        Component[] output = new Component[inputArray.Length + 1];
        Array.Copy(inputArray, output, inputArray.Length);
        output[inputArray.Length] = toAdd;

        return output;
    }
}
