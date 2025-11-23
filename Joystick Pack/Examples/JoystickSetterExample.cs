using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JoystickSetterExample : MonoBehaviour
{
    public WorldSpaceJoystick variableJoystick;
    public Text valueText;
    public Image background;
    public Sprite[] axisSprites;

    public void ModeChanged(int index)
    {
        // WorldSpaceJoystick does not support dynamic mode switching
        // The joystick type is fixed in the prefab configuration
        Debug.Log("Mode switching not supported for WorldSpaceJoystick");
    }

    public void AxisChanged(int index)
    {
        // WorldSpaceJoystick does not support axis options
        // The joystick handles both axes automatically
        if (index < axisSprites.Length)
            background.sprite = axisSprites[index];
        Debug.Log("Axis selection not supported for WorldSpaceJoystick");
    }

    public void SnapX(bool value)
    {
        // WorldSpaceJoystick does not support snap options
        Debug.Log("SnapX not supported for WorldSpaceJoystick");
    }

    public void SnapY(bool value)
    {
        // WorldSpaceJoystick does not support snap options
        Debug.Log("SnapY not supported for WorldSpaceJoystick");
    }

    private void Update()
    {
        valueText.text = "Current Value: " + variableJoystick.Direction;
    }
}