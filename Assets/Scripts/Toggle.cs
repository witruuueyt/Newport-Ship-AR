using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Toggle : MonoBehaviour
{
    public GameObject targetObject;

    //private void Start()
    //{
        
    //    targetObject.SetActive(gameObject.activeSelf);
    //}
    public void ToggleObject()
    {
        targetObject.SetActive(!targetObject.activeSelf);
    }

}
