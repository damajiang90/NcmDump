using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesktopTool;
using System.IO;
using UnityEngine.UI;
using System.Security.AccessControl;
using System;

public class Main : MonoBehaviour
{
    const string CurDirPathKey = "CurDirPathKey";
    const string RecursionDumpKey = "RecursionDumpKey";

    static Main _ins;
    public static Main ins
    {
        get
        {
            return _ins;
        }
    }
    
    public Transform uiRoot;

    bool _recursionDump;
    public bool recursionDump
    {
        get { return _recursionDump; }
        set
        {
            if(_recursionDump != value)
            {
                _recursionDump = value;
                PlayerPrefs.SetInt(RecursionDumpKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
    string _defaultDirPath;
    string _curDirPath;

    InputField _curDirInputField;
    Toggle _recursionToggle;
    GameObject _stateTip;
    Text _stateTipText;
    Prompt _prompt;
    UILoopScrollView _listView;
    GameObject _itemTemp;

    public string curDirPath
    {
        get => _curDirPath;
        set 
        {
            if(_curDirPath != value)
            {
                _curDirPath = value;
                _curDirInputField.text = _curDirPath;
                PlayerPrefs.SetString(CurDirPathKey, _curDirPath);
                PlayerPrefs.Save();
            }
        }
    }

    void Awake()
    {
        _ins = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        InitData();
        InitUI();
    }

    void OnDestroy()
    {
        ReleaseDump();
    }

    private void Update()
    {
        UpdateDump();
    }

    void InitData()
    {
        _recursionDump = PlayerPrefs.GetInt(RecursionDumpKey, 0) != 0;
        _defaultDirPath = Application.persistentDataPath;
        _curDirPath = PlayerPrefs.GetString(CurDirPathKey, _defaultDirPath);
        if(!Directory.Exists(_curDirPath))
        {
            _curDirPath = _defaultDirPath;
        }
    }
    
    void InitUI()
    {
        _recursionToggle = uiRoot.Find("RecursionToggle").GetComponent<Toggle>();
        _recursionToggle.isOn = _recursionDump;
        _recursionToggle.onValueChanged.AddListener(OnRecursionToggleChanged);
        _curDirInputField = uiRoot.Find("CurDirInputField").GetComponent<InputField>();
        _curDirInputField.text = _curDirPath;
        _curDirInputField.onEndEdit.AddListener(OnCurDirInputEnd);
        _prompt = uiRoot.Find("Prompt").gameObject.AddComponent<Prompt>();
        uiRoot.Find("ParentItemButton").GetComponent<Button>().onClick.AddListener(OnClickParentButton);
        var itemTrans = uiRoot.Find("ScrollView/Item") as RectTransform;
        _itemTemp = itemTrans.gameObject;
        var size = itemTrans.rect.size;
        _itemTemp.SetActive(false);
        _listView = uiRoot.Find("ScrollView").gameObject.AddComponent<UILoopScrollView>();
        _listView.Init(LoopScrollLayoutType.Vertical, Vector2.zero, size, _itemTemp, OnUpdateItemData);
        uiRoot.Find("DumpButton").GetComponent<Button>().onClick.AddListener(OnClickDumpButton);
        _stateTip = uiRoot.Find("StateTip").gameObject;
        _stateTipText = _stateTip.transform.Find("Text").GetComponent<Text>();
        _stateTip.SetActive(false);
        if(!RefreshDir())
        {
            ResetCurPath(_defaultDirPath);
        }
    }

    void OnRecursionToggleChanged(bool value)
    {
        _recursionDump = value;
    }

    void OnCurDirInputEnd(string text)
    {
        if(Directory.Exists(text))
        {
            string lastPath = curDirPath;
            curDirPath = text;
            if(!RefreshDir())
            {
                ResetCurPath(lastPath);
            }
        }
        else
        {
            _curDirInputField.text = curDirPath;
        }
    }

    void OnClickParentButton()
    {
        string lastPath = _curDirPath;
        DirectoryInfo parentInfo = Directory.GetParent(lastPath);
        if (parentInfo != null && !parentInfo.FullName.Equals(_curDirPath))
        {
            curDirPath = parentInfo.FullName;
            if(!RefreshDir())
            {
                ResetCurPath(lastPath);
            }
        }
    }

    bool CheckValidAccessDir(string path)
    {
        DirectoryInfo dir = new DirectoryInfo(path);
        return CheckValidAccessDir(dir);
    }

    bool CheckValidAccessDir(DirectoryInfo dir)
    {
        return dir.Exists && !dir.Attributes.HasFlag(FileAttributes.System);
    }

    bool CheckValidAccessFile(string path)
    {
        FileInfo fileInfo = new FileInfo(path);
        return CheckValidAccessFile(fileInfo);
    }

    bool CheckValidAccessFile(FileInfo fileInfo)
    {
        return fileInfo.Exists && !fileInfo.Attributes.HasFlag(FileAttributes.System);
    }

    public void SetDir(string path)
    {
        if(CheckValidAccessDir(path))
        {
            string lastPath = curDirPath;
            curDirPath = path;
            if (!RefreshDir())
            {
                ResetCurPath(lastPath);
            }
        }
    }

    public bool RefreshDir()
    {
        try
        {
            DirectoryInfo curDir = new DirectoryInfo(_curDirPath);
            if (curDir.Exists)
            {
                _listView.ScrollRect.StopMovement();
                List<FileData> dataList = new List<FileData>();
                DirectoryInfo[] dirs = curDir.GetDirectories("*", SearchOption.TopDirectoryOnly);
                foreach (var dir in dirs)
                {
                    if (CheckValidAccessDir(dir))
                    {
                        dataList.Add(new FileData() { isDir = true, path = dir.FullName });
                    }
                }
                FileInfo[] files = curDir.GetFiles("*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (CheckValidAccessFile(file))
                    {
                        dataList.Add(new FileData() { isDir = false, path = file.FullName });
                    }
                }
                _listView.Show(dataList, 0);
                return true;
            }
        }
        catch(Exception e)
        {
            Debug.LogWarning(e);
        }
        return false;
    }

    void ResetCurPath(string path)
    {
        _curDirInputField.text = path;
        curDirPath = path;
    }

    void OnUpdateItemData(int index, object data, GameObject go)
    {
        FileItem item = go.GetComponent<FileItem>();
        if(item == null)
        {
            item = go.AddComponent<FileItem>();
        }
        item.SetData(data as FileData);
    }

    int _curDumpIndex;
    List<string> _curDumpFileList = new List<string>();
    Queue<Action> _dumpEventQueue = new Queue<Action>();
    NeteaseCrypto _neteaseFile;
    object _neteaseFileLock = new object();

    void OnClickDumpButton()
    {
        ResetDump();
        string[] ncmFiles = Directory.GetFiles(_curDirPath, "*.ncm", _recursionDump ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        if (ncmFiles.Length > 0)
        {
            foreach (var ncmFile in ncmFiles)
            {
                _curDumpFileList.Add(ncmFile);
            }
            ResumeDump();
        }
        else
        {
            _prompt.ShowOneBtn("当前目录没有ncm文件", null);
        }
    }

    void ResumeDump()
    {
        if(_curDumpIndex < _curDumpFileList.Count)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(DumpQueue, _curDumpFileList[_curDumpIndex]);
        }
        else
        {
            _prompt.SetActive(false);
        }
    }

    void DumpQueue(object obj)
    {
        CloseLastNeteaseFile();
        try
        {
            var path = obj as string;
            var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _neteaseFile = new NeteaseCrypto(fs);
            var parent = Directory.GetParent(path);
            if(parent != null)
            {
                string fileName = path;
                int index = fileName.LastIndexOf('.');
                if(index != -1)
                {
                    fileName = fileName.Substring(0, index);
                }
                _neteaseFile.FileName = fileName;
                _neteaseFile.Dump();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
        }
        finally
        {
            CloseLastNeteaseFile();
        }
        
        EnqueueDump(NextDump);
    }

    void NextDump()
    {
        if(_curDumpIndex < _curDumpFileList.Count - 1)
        {
            _curDumpIndex++;
            ResumeDump();
        }
        else
        {
            ResetDump();
            RefreshDir();
            _prompt.ShowOneBtn("转换完成", null);
        }
    }

    void CloseLastNeteaseFile()
    {
        if(_neteaseFile != null)
        {
            lock (_neteaseFileLock)
            {
                if(_neteaseFile != null)
                {
                    _neteaseFile.CloseFile();
                    _neteaseFile = null;
                }
            }
        }
    }

    void ResetDump()
    {
        _curDumpIndex = 0;
        _curDumpFileList.Clear();
        lock(_dumpEventQueue)
        {
            _dumpEventQueue.Clear();
        }
        CloseLastNeteaseFile();
    }

    void EnqueueDump(Action action)
    {
        lock (_dumpEventQueue)
        {
            _dumpEventQueue.Enqueue(action);
        }
    }

    void UpdateDump()
    {
        if (_dumpEventQueue.Count > 0)
        {
            lock (_dumpEventQueue)
            {
                if(_dumpEventQueue.Count > 0)
                {
                    var action = _dumpEventQueue.Dequeue();
                    action.Invoke();
                }
            }
        }
        if(_curDumpIndex < _curDumpFileList.Count)
        {
            string path = _curDumpFileList[_curDumpIndex];
            string fileName = Path.GetFileName(path);
            SetStateTip(string.Format("正在转换:\n{0}", fileName));
        }
        else
        {
            SetStateTip(null);
        }
    }

    void ReleaseDump()
    {
        ResetDump();
    }

    string _lastStateTip;
    void SetStateTip(string tip)
    {
        if(_lastStateTip != tip)
        {
            _lastStateTip = tip;
            if (!string.IsNullOrEmpty(tip))
            {
                _stateTipText.text = tip;
                _stateTip.SetActive(true);
            }
            else
            {
                _stateTip.SetActive(false);
            }
        }
    }
}
