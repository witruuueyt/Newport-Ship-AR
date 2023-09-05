using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextManager : MonoBehaviour
{
    public string fileName; //文件名
    private string filePath; //文件路径
    public TMP_InputField inputField; //包含玩家输入的TMP Input Field组件
    public TMP_Text displayText; //要显示文本的TMP Text组件

    private void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, fileName); //获取文件路径
        LoadText(); //加载文本
    }

    public void SaveText()
    {
        string inputText = inputField.text; //获取TMP Input Field中的文本输入
        File.WriteAllText(filePath, inputText); //将文本写入文件
    }

    public void LoadText()
    {
        if (File.Exists(filePath))
        {
            string loadedText = File.ReadAllText(filePath); //从文件读取文本
            displayText.text = loadedText; //将文本显示在TMP Text组件中
        }
        else
        {
            Debug.LogError("File not found!"); //如果文件不存在，输出错误信息
        }
    }
}
