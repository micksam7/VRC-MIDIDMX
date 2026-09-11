using UdonSharp;
using UnityEngine;
using VRC.SDK3.Midi;
using VRC.SDKBase;
using VRC.Udon;
using System;
using System.Text.RegularExpressions;
using VRC;




#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor.Build;
using UnityEditor;
using UdonSharpEditor;
#endif

//micca code
//midi event handling is written to keep the number of non-extern ops low
//so most of the work is shuffled off to a shader

public enum MIDIDMXMode : int
{
    VRSL,
    VRSLNineUniverse,
    MDMX,
    MDMXOld,
    VRSLVertical
};

//Attempts to execute after video players [to replace DMX texture]
//If this doesn't work, you may need to come up with your own solution [definitely ping me about it though!]
//See funny reflection used for the vrsl readback
[DefaultExecutionOrder(1)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MIDIDMX : UdonSharpBehaviour
{
    const int BLOCK_SIZE = 2048; //size of blocks, do not change
    const int CHAR_OFFSET = 1024; //offset for wm_char support, do not change

    [Header("DMX Configuration")]
    public MIDIDMXMode mode = 0;
    public RenderTexture DMXTexture;
    public Material MIDIDMXRenderMat;

    public UdonSharpBehaviour vrslReadback;
    [NonSerialized] public RenderTexture storedTexture;
    [NonSerialized] public RenderTexture internalTexture;

    [Header("Multi-Input Data Masking")]
    public bool enableMask = false;
    public Material conversionMat;
    public uint maskIndex = 0;
    public Texture[] masks;

    [Header("Logo Status")]
    public Material logoMat;

    private int dataBlock = 0;

    private float lastUpdate = 0;

    bool state = false;
    bool previousState = false;
    int knockState = 0;

    bool isChar = false;

    Component[] eventObjects = new Component[0];
    string[] eventCallbacks = new string[0];

    //float for final shader
    [NonSerialized]
    private float[][] data = {
        new float[BLOCK_SIZE], new float[BLOCK_SIZE], new float[BLOCK_SIZE], new float[BLOCK_SIZE],
        new float[BLOCK_SIZE], new float[BLOCK_SIZE], new float[BLOCK_SIZE], new float[BLOCK_SIZE], };

    string inputBuffer;

    void Start()
    {
        if (vrslReadback != null) {
            storedTexture = (RenderTexture) vrslReadback.GetProgramVariable("texture");
        }

        internalTexture = new RenderTexture(DMXTexture);
        internalTexture.name = "MIDIDMX Temporary Texture"; //only for editor really

        MIDIDMXRenderMat.SetInt("_Mode", (int)mode);

        if (logoMat != null)
        {
            logoMat.SetColor("_Color", Color.white);
        }

        state = false;

        Debug.Log("[MIDIDMX] MIDIDMX:CHAR is available in this world. https://github.com/micksam7/VRC-MIDIDMX");
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
        if (isChar) return;

        int address = (channel << 6) + ((number >> 1) & 0xFF);
        velocity += (number << 7) & 0xFF;
        //Debug.Log($"MIDION: {address} = {velocity}");
        data[dataBlock][address] = velocity;
    }

    //other half of the block
    public override void MidiNoteOff(int channel, int number, int velocity)
    {
        if (isChar) return;

        int address = (channel << 6) + ((number >> 1) & 0xFF) + 1024;
        velocity += (number << 7) & 0xFF;
        //Debug.Log($"MIDIOFF: {address} = {velocity}");
        data[dataBlock][address] = velocity;
    }

    public override void MidiControlChange(int channel, int number, int value)
    {
        if (isChar) return;

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
                MidiStart();
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
            data[i] = new float[BLOCK_SIZE];
        }
    }

    void Update()
    {
        //WM_CHAR support start [aka keyboard emulation]
        //as usual, we need to avoid running as much udon as possible
        //so this is engineered to rely on externs as much as is reasonable
        //because of the already high cpu overhead, we're aiming to copy entire chunks into the shader cbuffer
        //so we aren't able to do individual channels, but the protocol allows a little bit of flexibility
        //so if someone wants to go crazy on the sender with packing groups of changing channels together, it's possible

        //keep a buffer on the offchance messages span over a few frames
        inputBuffer = inputBuffer + Input.inputString;

        Debug.Log($"Buffer: {inputBuffer}");

        //reset buffer to first occurance of "DMXSEND" if the buffer is lomg
        if (inputBuffer.Length > 102400) {
            const char seperator = (char)0xFFFD;
            int split = inputBuffer.LastIndexOf(seperator);
            inputBuffer = inputBuffer.Substring(split);

            //if it's still too long, discard it entirely. oh well.
            if (inputBuffer.Length > 102400)
            {
                inputBuffer = "";
            }
        }

        //find any matches
        MatchCollection matches = Regex.Matches(inputBuffer,@"\uFFFD(.?)(.?)(.*?)\uFFFF",RegexOptions.Singleline);
        for (int i = 0; i < matches.Count; i++) //can't use foreach because of udonsharp limitations
        {
            Match match = matches[i];
            int startIndex = match.Captures[0].Value[0] - CHAR_OFFSET;
            int bufferSize = match.Captures[1].Value[0] - CHAR_OFFSET;
            string buffer = match.Captures[2].Value;
            buffer = Regex.Replace(buffer,@"([^\u0400-\uFFFF])",""); //remove any characters outside of our working range [ie user keyboard input]
            if (buffer.Length != bufferSize)
            {
                Debug.Log($"[MIDIDMX] Discarded a message because of length mismatch: {buffer.Length} != {bufferSize}");
                continue; //discard because there's extra or missing data in it somewhere
            }

            int startBlock = startIndex / BLOCK_SIZE;
            int endBlock = (startIndex + bufferSize) / BLOCK_SIZE;
            if (startBlock < 0 || startBlock > 8 || endBlock - startBlock > 1)
            {
                Debug.Log($"[MIDIDMX] Discarded a message because of an invalid start range and/or length: {startIndex} {bufferSize}");
                continue; //out of range or something
            }
            startIndex -= startBlock*BLOCK_SIZE;

            //if someone decides to give us a message that goes across blocks ... ugh fine.
            if (endBlock != startBlock) {
                //double copy
                int split = (startIndex + bufferSize) % BLOCK_SIZE;
                Array.Copy(buffer.ToCharArray(), 0, data[startBlock], startIndex, BLOCK_SIZE - startIndex);
                Array.Copy(buffer.ToCharArray(), split, data[endBlock], 0, split - bufferSize);
            } else {
                Array.Copy(buffer.ToCharArray(), 0, data[startBlock], startIndex, buffer.Length);
            }
        }

        //update if we got data this frame
        if (matches.Count > 0)
        {
            isChar = true;
            lastUpdate = Time.fixedTime;
            if (!state)
            {
                MidiStart();
            }

            //sends a log message so senders can tell when the buffer is done processing and can throttle themselves down if needed
            Debug.Log("MIDIDMX:CHARREADY");
        }
        
        //Only update if we're getting the ping packet
        //Otherwise we release the texture [assuming script order is right :)]
        if (state && lastUpdate > Time.fixedTime - 5)
        {
            //tho those ids should be ints tbh
            MIDIDMXRenderMat.SetFloatArray("_Block0", data[0]);
            MIDIDMXRenderMat.SetFloatArray("_Block1", data[1]);
            MIDIDMXRenderMat.SetFloatArray("_Block2", data[2]);
            MIDIDMXRenderMat.SetFloatArray("_Block3", data[3]);
            MIDIDMXRenderMat.SetFloatArray("_Block4", data[4]);
            MIDIDMXRenderMat.SetFloatArray("_Block5", data[5]);
            MIDIDMXRenderMat.SetFloatArray("_Block6", data[6]);
            MIDIDMXRenderMat.SetFloatArray("_Block7", data[7]);

            MIDIDMXRenderMat.SetFloat("_MaskingEnable", enableMask ? 1f : 0f);

            MIDIDMXRenderMat.SetFloat("_CharInput", isChar ? 1f : 0f);
            
            if (enableMask && maskIndex < masks.Length) {
                if (conversionMat != null)
                    VRCGraphics.Blit(null, DMXTexture, conversionMat);

                VRCGraphics.Blit(DMXTexture, internalTexture, MIDIDMXRenderMat); 
                MIDIDMXRenderMat.SetTexture("_MaskingTex", masks[maskIndex]);
            } else
            {
                VRCGraphics.Blit(null, internalTexture, MIDIDMXRenderMat); //generates the dmx gridnode
            }

            VRCGraphics.Blit(internalTexture, DMXTexture); //replaces the video texture
        }
        else
        {
            MidiEnd();
        }

        //Process registered events
        if (previousState != state)
        {
            previousState = state;
            ProcessEvents();
        }
    }

    //Enable/Disable
    void MidiStart() {
        knockState = 3;
        state = true;
        Debug.Log("[MIDIDMX] Unlocked and ready.");
        dataBlock = 0;
        ClearChannels();

        if (vrslReadback != null) {
            vrslReadback.SetProgramVariable("texture",internalTexture);
        }

        if (logoMat != null)
        {
            logoMat.SetColor("_Color", Color.green);
        }
    }

    void MidiEnd() {
        state = false;
        knockState = 0;
        isChar = false;
        
        if (vrslReadback != null) {
            vrslReadback.SetProgramVariable("texture",storedTexture);
        }

        if (logoMat != null)
        {
            logoMat.SetColor("_Color", Color.white);
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

#if UNITY_EDITOR && !COMPILER_UDONSHARP
[CustomEditor(typeof(MIDIDMX))]
[CanEditMultipleObjects]
public class MIDIDMX_Editor : Editor
{
    SerializedProperty vrslReadback;
    SerializedProperty mode;
    SerializedProperty MIDIDMXRenderMat;

    int modeSaved = 0;

    void OnEnable()
    {
        vrslReadback = serializedObject.FindProperty("vrslReadback");
        mode = serializedObject.FindProperty("mode");
        MIDIDMXRenderMat = serializedObject.FindProperty("MIDIDMXRenderMat");
        modeSaved = mode.intValue;
    }

    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

        if (GUILayout.Button("Find Components"))
        {
            FindComponents();
        }

        if (mode.intValue != modeSaved) {
            modeSaved = mode.intValue;
            Material mat = (Material) MIDIDMXRenderMat.objectReferenceValue;
            mat.SetInt("_Mode", mode.intValue);
            EditorUtility.SetDirty(mat);
            Debug.Log("[MIDIDMX] Updated material");
        }

        DrawDefaultInspector();
    }

    void FindComponents() {
        //Find VRSL Readback
        {
            //we love reflection
            Type assembly = Type.GetType("VRSL.VRSL_GPUReadBack, Assembly-CSharp");

            if (assembly == null) {
                Debug.Log("[MIDIDMX] VRSL Readback not installed.");
                vrslReadback.objectReferenceValue = null;
            } else {
                UnityEngine.Object component = FindObjectOfType(assembly);

                if (component == null) {
                    Debug.Log("[MIDIDMX] No VRSL Readback found in scene.");
                    vrslReadback.objectReferenceValue = null;
                } else {
                    Debug.Log("[MIDIDMX] Found VRSL Readback. Configuring MIDIDMX to swap it's texture.");
                    vrslReadback.objectReferenceValue = (UdonSharpBehaviour) component;
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
