using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuUISwitch : MonoBehaviour
{
    public GameObject MainMenuUI;

    public void ToggleObject()
    {
        MainMenuUI.SetActive(!MainMenuUI.activeSelf);
    }

}
