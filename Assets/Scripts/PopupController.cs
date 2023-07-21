using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PopupController : MonoBehaviour
{
    public GameObject popupPanel; // 弹出界面的对象

    private bool isPopupVisible = false; // 弹出界面的显示状态

    private void Update()
    {
        // 当点击屏幕的任意位置时
        if (Input.GetMouseButtonDown(0))
        {
            // 检测是否点击到弹出界面以外的地方
            if (isPopupVisible && !IsPointerOverUIObject())
            {
                // 隐藏弹出界面
                HidePopup();
            }
        }
    }

    // 显示弹出界面
    public void ShowPopup()
    {
        popupPanel.SetActive(true);
        isPopupVisible = true;
    }

    // 隐藏弹出界面
    public void HidePopup()
    {
        popupPanel.SetActive(false);
        isPopupVisible = false;
    }

    // 检测是否点击到UI元素
    private bool IsPointerOverUIObject()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
