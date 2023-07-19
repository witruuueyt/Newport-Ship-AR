using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ToggleText : MonoBehaviour
{
    public TMP_Text textBox; 
    public TMP_Text textButton;
    private bool isTextVisible = false; 

    
    public void ToggleTextBox()
    {
        isTextVisible = !isTextVisible; 

        if (isTextVisible)
        {
            textBox.gameObject.SetActive(true); 
            textBox.text = "Hello and welcome to the Newport Medieval Ship Centre! This guide gives you what you need for a self-guided tour, but we have volunteers who can answer any questions you may have (or direct you to someone who can!) Many visitors expect to see the Medieval Ship displayed in all its glory, but the truth is you can’t… for now, anyway. Not all the timbers have completed the conservation process, so the ship can’t be rebuilt yet. You will, however, see thousands of conserved timbers and artefacts safely tucked away in our two stores!";
            textButton.text = "Close";
        }
        else
        {
            textBox.gameObject.SetActive(false);
            textButton.text = "Story";
        }
    }
}
