using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPortal : MonoBehaviour
{
    public Transform aim;
    public Transform player;
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("working");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("portal");
            player.transform.position = aim.position;

        }



    }
}
