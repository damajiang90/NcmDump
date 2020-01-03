using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class FileItem : MonoBehaviour
{
    Image _iconFolder;
    Image _iconFile;
    Text _pathText;
    FileData _fileData;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClickButton);
        _pathText = transform.Find("Text").GetComponent<Text>();
        _iconFolder = transform.Find("IconFolder").GetComponent<Image>();
        _iconFile = transform.Find("FileFolder").GetComponent<Image>();
    }

    private void OnDestroy()
    {
        
    }

    void OnClickButton()
    {
        if(_fileData != null)
        {
            if(_fileData.isDir)
            {
                Main.ins.SetDir(_fileData.path);
            }
        }
    }

    public void SetData(FileData fileData)
    {
        this._fileData = fileData;
        _pathText.text = Path.GetFileName(fileData.path);
        _iconFolder.enabled = fileData.isDir;
        _iconFile.enabled = !fileData.isDir;
    }
}

public class FileData
{
    public bool isDir;
    public string path;
}

