using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatorControl : MonoBehaviour
{
    public Animator animator;
    public GameObject dock;
    public GameObject anchor;
    public GameObject bluesea;
    public GameObject greensea;
    public GameObject closesail;
    public GameObject opensail;

    public void PlayAnimation()
    {
        animator.SetTrigger("Play");
        dock.SetActive(false);
        anchor.SetActive(true);
        bluesea.SetActive(false);
        greensea.SetActive(true);
        closesail.SetActive(false);
        opensail.SetActive(true);
    }

    public void StopAnimation()
    {
        animator.SetBool("Stop", true);
        dock.SetActive(true);
        anchor.SetActive(false);
        bluesea.SetActive(true);
        greensea.SetActive(false);
        closesail.SetActive(true);
        opensail.SetActive(false);
    }
}
