using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UILoopScrollViewIndex : UILoopScrollViewBase
{
    Action<int, GameObject> m_updateItemCallback;
    Func<int, Vector2> m_customItemSizeGetter;
    Func<int, GameObject> m_customItemTempGetter;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnDestroy()
    {
        ResetCallback();
        base.OnDestroy();
    }

    protected override void UpdateItem(int itemIndex, GameObject itemObject)
    {
        if(m_updateItemCallback != null)
        {
            try
            {
                m_updateItemCallback.Invoke(itemIndex, itemObject);
            }
            catch(Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    protected override Vector2 GetItemSize(int index)
    {
        if(m_customItemSizeGetter != null)
        {
            return m_customItemSizeGetter.Invoke(index);
        }
        return m_defaultItemSize;
    }

    protected override GameObject GetItemTemp(int itemIndex)
    {
        if(m_customItemTempGetter != null)
        {
            return m_customItemTempGetter.Invoke(itemIndex);
        }
        return m_defaultItemTemp;
    }

    void ResetCallback()
    {
        m_updateItemCallback = null;
        m_customItemSizeGetter = null;
        m_customItemTempGetter = null;
    }

    public void Init(LoopScrollLayoutType layoutType,
        Vector2 spacing,
        Vector2 defaultItemSize,
        GameObject itemTemplate,
        Action<int, GameObject> updateItemCallback = null,
        Func<int, Vector2> customItemSizeGetter = null,
        Func<int, GameObject> customItemTempGetter = null,
        bool gridGroupAlignReverse = false)
    {
        m_updateItemCallback = updateItemCallback;
        m_customItemSizeGetter = customItemSizeGetter;
        m_customItemTempGetter = customItemTempGetter;
        InitScrollView(layoutType, spacing, defaultItemSize, itemTemplate, gridGroupAlignReverse);
    }

    /// <summary>
    /// 显示/刷新列表
    /// </summary>
    /// <param name="dataCount"></param>
    /// <param name="targetIndex">默认-1则保持原有滚动进度，否则移动到指定索引处</param>
    public void Show(int dataCount, int targetIndex = -1)
    {
        UpdateData(dataCount, targetIndex);
    }
}
