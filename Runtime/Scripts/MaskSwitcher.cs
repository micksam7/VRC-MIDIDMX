
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MaskSwitcher : UdonSharpBehaviour
{
    public MIDIDMX mididmx;
    public Button toggleUI;

    public void _Toggle()
    {
        mididmx.enableMask = !mididmx.enableMask;

        if (mididmx.enableMask)
        {
            if (toggleUI != null)
                toggleUI.GetComponent<Image>().color = Color.green;
        }
        else
        {
            if (toggleUI != null)
                toggleUI.GetComponent<Image>().color = Color.grey;
        }
    }
}
