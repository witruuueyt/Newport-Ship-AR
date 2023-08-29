using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Toggle : MonoBehaviour
{
    public GameObject targetObject;
    public GameObject targetObject1;

    public void ToggleObject()
    {
        targetObject.SetActive(!targetObject.activeSelf);
        targetObject1.SetActive(!targetObject1.activeSelf);
    }

}