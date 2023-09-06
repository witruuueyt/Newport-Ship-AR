using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sea : MonoBehaviour
{

    public GameObject objectA; 
    public GameObject objectB; 

    void OnEnable()
    {
        
        if (objectA != null)
        {
            objectB.SetActive(false);
        }
    }

    void OnDisable()
    {
        
        if (objectB != null)
        {
            objectA.SetActive(false);
        }
    }


}
