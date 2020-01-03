using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 排版方向类型
/// </summary>
public enum LoopScrollLayoutType
{
    /// <summary>
    /// 只横向
    /// </summary>
    Horizontal,
    /// <summary>
    /// 只纵向
    /// </summary>
    Vertical,
    /// <summary>
    /// 在纵向后横向排列
    /// </summary>
    HorizontalAfterVertical,
    /// <summary>
    /// 在横向后纵向排列
    /// </summary>
    VerticalAfterHorizontal
}

/// <summary>
/// 让无限滚动列表的item能支持额外的选中拖起功能
/// LoopItemSetter实现自动属性即可，生成item时会在UILoopScrollView中自动设置
/// OnPointerDown中实现调用LoopItemSetter设置为自身即可
/// CallOnBeginDrag，CallOnDrag，CallOnEndDrag模拟回调拖起事件
/// </summary>
public interface ILoopScrollDragItem : IPointerDownHandler
{
    Action<ILoopScrollDragItem> LoopDragItemSetter { get; set; }
    void CallOnBeginDrag(PointerEventData eventData);
    void CallOnDrag(PointerEventData eventData);
    void CallOnEndDrag(PointerEventData eventData);
}

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(ScrollRect))]
public abstract class UILoopScrollViewBase : UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>
    /// item容器
    /// </summary>
    struct ItemContainer
    {
        /// <summary>
        /// 理论占据空间（不代表实际渲染空间）
        /// </summary>
        public Rect rect;
        /// <summary>
        /// 处于组排版数据时，实际的组内大小，纵组横排时表示实际宽度，横组纵排时表示实际高度
        /// </summary>
        public float realSize;
        /// <summary>
        /// item的trans
        /// </summary>
        public RectTransform trans;
    }

    #region Base
    protected ScrollRect m_scrollRect;
    public ScrollRect ScrollRect { get { return m_scrollRect; } }
    protected RectTransform m_viewRect;
    public RectTransform viewRect { get { if(!m_viewRect) m_viewRect = m_scrollRect.viewport; if(!m_viewRect) m_viewRect = m_scrollRect.transform as RectTransform; return m_viewRect; } }
    public RectTransform content
    {
        get { return m_scrollRect.content; }
        set
        {
            if(m_scrollRect.content != value)
            {
                m_scrollRect.content = value;
                if(value)
                {
                    ReCalculateContainer();
                }
            }
        }
    }
    public RectTransform viewport { get { return m_scrollRect.viewport; } set { m_scrollRect.viewport = value; } }
    public ScrollRect.MovementType movementType { get { return m_scrollRect.movementType; } set { m_scrollRect.movementType = value; } }
    public float elasticity { get { return m_scrollRect.elasticity; } set { m_scrollRect.elasticity = value; } }
    public bool inertia { get { return m_scrollRect.inertia; } set { m_scrollRect.inertia = value; } }
    public float decelerationRate { get { return m_scrollRect.decelerationRate; } set { m_scrollRect.decelerationRate = value; } }
    public float scrollSensitivity { get { return m_scrollRect.scrollSensitivity; } set { m_scrollRect.scrollSensitivity = value; } }
    public Vector2 velocity { get { return m_scrollRect.velocity; } set { m_scrollRect.velocity = value; } }
    public bool vertical { get { return m_scrollRect.vertical; } set { m_scrollRect.vertical = value; } }
    public bool horizontal { get { return m_scrollRect.horizontal; } set { m_scrollRect.horizontal = value; } }
    public float verticalNormalizedPosition { get { return m_scrollRect.verticalNormalizedPosition; } set { m_scrollRect.verticalNormalizedPosition = value; } }
    public float horizontalNormalizedPosition { get { return m_scrollRect.horizontalNormalizedPosition; } set { m_scrollRect.horizontalNormalizedPosition = value; } }
    public Vector2 normalizedPosition { get { return m_scrollRect.normalizedPosition; } set { m_scrollRect.normalizedPosition = value; } }
    public Scrollbar verticalScrollbar { get { return m_scrollRect.verticalScrollbar; } set { m_scrollRect.verticalScrollbar = value; } }
    public Scrollbar horizontalScrollbar { get { return m_scrollRect.horizontalScrollbar; } set { m_scrollRect.horizontalScrollbar = value; } }
    public ScrollRect.ScrollbarVisibility verticalScrollbarVisibility { get { return m_scrollRect.verticalScrollbarVisibility; } set { m_scrollRect.verticalScrollbarVisibility = value; } }
    public ScrollRect.ScrollbarVisibility horizontalScrollbarVisibility { get { return m_scrollRect.horizontalScrollbarVisibility; } set { m_scrollRect.horizontalScrollbarVisibility = value; } }
    public float verticalScrollbarSpacing { get { return m_scrollRect.verticalScrollbarSpacing; } set { m_scrollRect.verticalScrollbarSpacing = value; } }
    public float horizontalScrollbarSpacing { get { return m_scrollRect.horizontalScrollbarSpacing; } set { m_scrollRect.horizontalScrollbarSpacing = value; } }
    public ScrollRect.ScrollRectEvent onValueChanged { get { return m_scrollRect.onValueChanged; } set { m_scrollRect.onValueChanged = value; } }
    protected RectTransform m_cachedTrans;
    public RectTransform CachedTrans { get { return m_cachedTrans; } }
    bool m_localValueChangedInit;
    protected bool IsViewEnabled { get { return content; } }

    protected abstract void UpdateItem(int itemIndex, GameObject itemObject);
    protected abstract Vector2 GetItemSize(int itemIndex);
    protected abstract GameObject GetItemTemp(int itemIndex);

    protected override void Awake()
    {
        base.Awake();
        m_scrollRect = GetComponent<ScrollRect>();
        if(Application.isPlaying)
        {
            CacheBase();
        }
    }

    protected override void Start()
    {
        base.Start();
        if(Application.isPlaying)
        {
            UpdateScrollHVEnable();
        }
    }

    protected override void OnDestroy()
    {
        if(m_localValueChangedInit)
        {
            if(m_scrollRect)
            {
                onValueChanged.RemoveListener(LocalOnValueChanged);
            }
            m_localValueChangedInit = false;
        }
        base.OnDestroy();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        RestoreLastDragInfo();
        base.OnDisable();
    }

    protected virtual void LateUpdate()
    {
        CheckViewRectChanged();
    }

    void CacheBase()
    {
        if(!m_cachedTrans)
        {
            m_cachedTrans = transform as RectTransform;
        }
        ResetContentAnchor();
        if(!m_localValueChangedInit)
        {
            onValueChanged.AddListener(LocalOnValueChanged);
            m_localValueChangedInit = true;
        }
    }

    void ResetContentAnchor()
    {
        if(content)
        {
            content.pivot = Vector2.up;
            content.anchorMin = content.anchorMax = Vector2.up;
        }
    }

    void LocalOnValueChanged(Vector2 value)
    {
        TryUpdateCurItems();
    }
    #endregion

    #region Pool
    /// <summary>
    /// poolrect
    /// </summary>
    protected RectTransform m_itemPoolRect;
    /// <summary>
    /// item模板
    /// </summary>
    protected GameObject m_defaultItemTemp;
    /// <summary>
    /// 缓存池
    /// </summary>
    protected Dictionary<GameObject, Stack<RectTransform>> m_itemPoolMap;
    /// <summary>
    /// 临时item缓存池，用于存放切换过程中产生的回收节点，待结束后没用上就真正放入缓存池（可以减少过程中反复放入/取出缓存池的消耗）
    /// </summary>
    protected Dictionary<GameObject, Stack<RectTransform>> m_tempItemPoolMap;

    void InitPool()
    {
        m_itemPoolMap = new Dictionary<GameObject, Stack<RectTransform>>();
        m_tempItemPoolMap = new Dictionary<GameObject, Stack<RectTransform>>();
        GameObject poolRoot = new GameObject("Pool", typeof(RectTransform));
        m_itemPoolRect = poolRoot.transform as RectTransform;
        poolRoot.SetActive(false);
        m_itemPoolRect.SetParent(CachedTrans, false);
    }

    Stack<RectTransform> GetTempItemPool(GameObject tempGo)
    {
        if(tempGo)
        {
            Stack<RectTransform> pool;
            if(!m_tempItemPoolMap.TryGetValue(tempGo, out pool))
            {
                m_tempItemPoolMap[tempGo] = pool = new Stack<RectTransform>();
            }
            return pool;
        }
        return null;
    }

    Stack<RectTransform> GetItemPool(GameObject tempGo)
    {
        if(tempGo)
        {
            Stack<RectTransform> pool;
            if(!m_itemPoolMap.TryGetValue(tempGo, out pool))
            {
                m_itemPoolMap[tempGo] = pool = new Stack<RectTransform>();
            }
            return pool;
        }
        return null;
    }

    RectTransform GetItemRectTrans(int index)
    {
        RectTransform trans;
        GameObject temp = GetItemTemp(index);
        if(temp != null)
        {
            Stack<RectTransform> pool;
            if((pool = GetTempItemPool(temp)) == null || pool.Count == 0)
            {
                pool = GetItemPool(temp);
            }
            if(pool != null && pool.Count > 0)
            {
                trans = pool.Pop();
            }
            else
            {
                GameObject itemGO = Instantiate(temp);
                trans = itemGO.transform as RectTransform;
                trans.SetParent(m_itemPoolRect, false);
                trans.anchorMin = trans.anchorMax = Vector2.up;
                trans.pivot = Vector2.zero;
            }
            trans.SetParent(content, false);
            trans.gameObject.SetActive(true);
            return trans;
        }
        return null;
    }

    void RecycleItemRectTrans(int index, RectTransform trans)
    {
        trans.SetParent(m_itemPoolRect, false);
        GameObject temp = GetItemTemp(index);
        var pool = GetItemPool(temp);
        if(pool != null)
        {
            pool.Push(trans);
        }
        else
        {
            Debug.LogWarningFormat("获取不到有效的item模板:{0}", index);
            Destroy(trans.gameObject);
        }
    }

    /// <summary>
    /// 回收item到临时缓存池
    /// </summary>
    /// <param name="index"></param>
    void RecycleItemRectTransToTempPool(int index)
    {
        var trans = m_curItemContainers[index].trans;
        if(trans)
        {
            var goTemp = GetItemTemp(index);
            var tempPool = GetTempItemPool(goTemp);
            if(tempPool != null)
            {
                tempPool.Push(trans);
            }
            else
            {
                Debug.LogWarningFormat("临时回收获取不到有效的item模板:{0}", index);
                Destroy(trans.gameObject);
            }
        }
    }

    /// <summary>
    /// 临时缓存池到正式缓存池的移交
    /// </summary>
    void RecycleAllTempPoolToPool()
    {
        if(m_tempItemPoolMap != null && m_tempItemPoolMap.Count > 0)
        {
            foreach(var pair in m_tempItemPoolMap)
            {
                var go = pair.Key;
                var pool = GetItemPool(go);
                var stack = pair.Value;
                while(stack.Count > 0)
                {
                    var trans = stack.Pop();
                    trans.SetParent(m_itemPoolRect, false);
                    if(pool != null)
                    {
                        pool.Push(trans);
                    }
                    else
                    {
                        Destroy(trans.gameObject);
                    }
                }
            }
        }
    }

    public void ClearAllPoolObject()
    {
        if(m_itemPoolMap != null)
        {
            foreach(var pool in m_itemPoolMap.Values)
            {
                foreach(var trans in pool)
                {
                    if(trans)
                    {
                        Destroy(trans.gameObject);
                    }
                }
                pool.Clear();
            }
        }
        if(m_tempItemPoolMap != null)
        {
            foreach(var pool in m_tempItemPoolMap.Values)
            {
                foreach(var trans in pool)
                {
                    if(trans)
                    {
                        Destroy(trans.gameObject);
                    }
                }
                pool.Clear();
            }
        }
    }
    #endregion

    #region 排版/数据
    static ItemContainer[] sEmptyContainer = new ItemContainer[0];
    private LoopScrollLayoutType m_layoutType;
    /// <summary>
    /// 排版类型
    /// </summary>
    public LoopScrollLayoutType LayoutType { get { return m_layoutType; } }
    private bool m_gridGroupAlignReverse;
    /// <summary>
    /// 分组排版时，一组大小不一的item是否反向靠拢
    /// 默认false，Layout为HorizontalAfterVertical则向右靠拢，Layout为VerticalAfterHorizontal则向上靠拢
    /// </summary>
    public bool GridGroupAlignReverse { get { return m_gridGroupAlignReverse; } }
    private Vector2 m_layoutSpacing;
    /// <summary>
    /// Border
    /// </summary>
    public Vector2 LayoutSpacing { get { return m_layoutSpacing; } }
    protected Vector2 m_defaultItemSize;
    /// <summary>
    /// 默认item大小
    /// </summary>
    public Vector2 DefaultItemSize { get { return m_defaultItemSize; } }
    /// <summary>
    /// 相对content的能显示的区域
    /// </summary>
    Rect m_contentViewRect;
    Vector2 m_prevContentPosition;
    Vector2 m_curContentDelta;
    /// <summary>
    /// 当前item容器列表
    /// </summary>
    ItemContainer[] m_curItemContainers = sEmptyContainer;
    /// <summary>
    /// 当前数据量
    /// </summary>
    int m_dataCount;
    public int DataCount
    {
        get
        {
            return m_dataCount;
        }
    }
    /// <summary>
    /// 当前显示开始索引
    /// </summary>
    int m_curShowStartIndex = -1;
    /// <summary>
    /// 当前显示开始索引
    /// </summary>
    public int CurShowStartIndex
    {
        get
        {
            return m_curShowStartIndex;
        }
    }
    /// <summary>
    /// 当前显示结束索引
    /// </summary>
    int m_curShowEndIndex = -1;
    /// <summary>
    /// 当前显示结束索引
    /// </summary>
    public int CurShowEndIndex
    {
        get
        {
            return m_curShowEndIndex;
        }
    }
    /// <summary>
    /// 预定寻找索引，可使下次搜寻快速定位
    /// </summary>
    int m_preFindIndex = -1;
    /// <summary>
    /// 上一次检查的ViewRect
    /// </summary>
    Rect m_lastCheckViewRect;

    int LayoutGroupResetIndex
    {
        get
        {
            return m_layoutType == LoopScrollLayoutType.HorizontalAfterVertical ? 0 : 1;
        }
    }

    int LayoutRealSizeIndex
    {
        get
        {
            return m_layoutType == LoopScrollLayoutType.Horizontal || m_layoutType == LoopScrollLayoutType.HorizontalAfterVertical ? 0 : 1;
        }
    }

    protected void InitScrollView(LoopScrollLayoutType layoutType,
        Vector2 spacing,
        Vector2 defaultItemSize,
        GameObject itemTemplate,
        bool gridGroupAlignReverse)
    {
        CacheBase();
        m_layoutType = layoutType;
        m_layoutSpacing = spacing;
        m_defaultItemSize = defaultItemSize;
        m_defaultItemTemp = itemTemplate;
        m_gridGroupAlignReverse = gridGroupAlignReverse;
        InitPool();
        UpdateScrollHVEnable();
    }

    protected void UpdateScrollHVEnable()
    {
        horizontal = m_layoutType == LoopScrollLayoutType.Horizontal || m_layoutType == LoopScrollLayoutType.HorizontalAfterVertical;
        vertical = m_layoutType == LoopScrollLayoutType.Vertical || m_layoutType == LoopScrollLayoutType.VerticalAfterHorizontal;
    }

    protected void CheckViewRectChanged()
    {
        var rect = viewRect.rect;
        if(rect != m_lastCheckViewRect)
        {
            ReCalculateContainer();
        }
    }

    protected void UpdateContentViewRect()
    {
        m_lastCheckViewRect = viewRect.rect;
        ResetContentAnchor();
        Vector3[] fourCornersArray = new Vector3[4];
        viewRect.GetWorldCorners(fourCornersArray);
        Vector3 leftBottom = content.InverseTransformPoint(fourCornersArray[0]);
        Vector3 rightTop = content.InverseTransformPoint(fourCornersArray[2]);
        m_contentViewRect = new Rect((Vector2)leftBottom + content.anchoredPosition, rightTop - leftBottom);
    }

    protected void ReCalculateContainer()
    {
        if(!IsViewEnabled)
        {
            return;
        }
        RecycleCurShowContainer();
        ResetCurShowIndex();
        if(m_curItemContainers.Length < m_dataCount)
        {
            Array.Resize(ref m_curItemContainers, m_dataCount);
        }
        UpdateContentViewRect();
        Vector2 curPos = Vector2.zero, size = Vector2.zero, groupSize = Vector2.zero;
        Queue<int> curGroupIndexQueue = m_layoutType == LoopScrollLayoutType.HorizontalAfterVertical || m_layoutType == LoopScrollLayoutType.VerticalAfterHorizontal ? new Queue<int>() : null;
        int groupResetIndex = LayoutGroupResetIndex;
        int layoutRealSizeIndex = LayoutRealSizeIndex;
        for(int i = 0; i < m_dataCount; i++)
        {
            size = GetItemSize(i);
            var rect = new Rect(curPos, size);
            rect.y -= size.y;
            m_curItemContainers[i].rect = rect;
            m_curItemContainers[i].realSize = size[layoutRealSizeIndex];
            groupSize.x = Mathf.Max(groupSize.x, size.x);
            groupSize.y = Mathf.Max(groupSize.y, size.y);
            Vector2 nextSize = i < m_dataCount - 1 ? GetItemSize(i + 1) : Vector2.zero;
            bool isLastItem = i == m_dataCount - 1;
            if(curGroupIndexQueue != null)
            {
                curGroupIndexQueue.Enqueue(i);
            }
            bool newGroup = MoveItemPos(ref curPos, ref groupSize, size, nextSize, isLastItem);
            if(curGroupIndexQueue != null && (newGroup || isLastItem))
            {
                switch(m_layoutType)
                {
                    case LoopScrollLayoutType.HorizontalAfterVertical:
                        while(curGroupIndexQueue.Count > 0)
                        {
                            m_curItemContainers[curGroupIndexQueue.Dequeue()].rect.width = groupSize.x;
                        }
                        break;
                    case LoopScrollLayoutType.VerticalAfterHorizontal:
                        float y = rect.y + size.y - groupSize.y;
                        while(curGroupIndexQueue.Count > 0)
                        {
                            var index = curGroupIndexQueue.Dequeue();
                            var oldRect = m_curItemContainers[index].rect;
                            oldRect.y = y;
                            oldRect.height = groupSize.y;
                            m_curItemContainers[index].rect = oldRect;
                        }
                        break;
                }
            }
            if(newGroup)
            {
                groupSize[groupResetIndex] = 0;
            }
        }
        Vector2 contentSize = new Vector2(curPos.x, -curPos.y);
        switch(m_layoutType)
        {
            case LoopScrollLayoutType.Vertical:
                contentSize.x = m_contentViewRect.width;
                break;
            case LoopScrollLayoutType.Horizontal:
                contentSize.y = m_contentViewRect.height;
                break;
            case LoopScrollLayoutType.HorizontalAfterVertical:
                contentSize.x += groupSize.x;
                contentSize.y = m_contentViewRect.height;
                break;
            case LoopScrollLayoutType.VerticalAfterHorizontal:
                contentSize.x = m_contentViewRect.width;
                contentSize.y += groupSize.y;
                break;
            default:
                break;
        }
        content.sizeDelta = contentSize;
        horizontalNormalizedPosition = Mathf.Clamp01(horizontalNormalizedPosition);
        verticalNormalizedPosition = Mathf.Clamp01(verticalNormalizedPosition);
        TryUpdateCurItems();
    }

    bool MoveItemPos(ref Vector2 curPos, ref Vector2 groupSize, Vector2 curSize, Vector2 nextSize, bool isLastItem)
    {
        bool newGroup = false;
        switch(m_layoutType)
        {
            case LoopScrollLayoutType.Vertical:
                // 垂直方向 向下移动
                curPos.y -= curSize.y;
                if(!isLastItem)
                {
                    curPos.y -= m_layoutSpacing.y;
                }
                break;
            case LoopScrollLayoutType.Horizontal:
                // 水平方向 向右移动
                curPos.x += curSize.x;
                if(!isLastItem)
                {
                    curPos.x += m_layoutSpacing.x;
                }
                break;
            case LoopScrollLayoutType.VerticalAfterHorizontal:
                curPos.x += curSize.x;
                if(!isLastItem)
                {
                    curPos.x += m_layoutSpacing.x;
                }
                if(!isLastItem && curPos.x + nextSize.x >= m_contentViewRect.width)
                {//宽度超了 换行
                    curPos.x = 0;
                    curPos.y -= m_layoutSpacing.y;
                    curPos.y -= groupSize.y;
                    newGroup = true;
                }
                break;
            case LoopScrollLayoutType.HorizontalAfterVertical:
                curPos.y -= curSize.y;
                if(!isLastItem)
                {
                    curPos.y -= m_layoutSpacing.y;
                }
                if(!isLastItem && curPos.y - nextSize.y <= -m_contentViewRect.height)
                {//高度超了 换列
                    curPos.y = 0;
                    curPos.x += m_layoutSpacing.x;
                    curPos.x += groupSize.x;
                    newGroup = true;
                }
                break;
        }
        return newGroup;
    }

    float GetHorizontalPosScale(float posX)
    {
        float contentX = content.sizeDelta.x;
        float viewWidth = m_contentViewRect.width;
        if(contentX > viewWidth)
        {
            return Mathf.Clamp01(posX / (contentX - viewWidth));
        }
        return 0;
    }

    float GetVerticalPosScale(float posY)
    {
        float contentY = content.sizeDelta.y;
        float viewHeight = m_contentViewRect.height;
        if(contentY > viewHeight)
        {
            return Mathf.Clamp01(1 + posY / (contentY - viewHeight));
        }
        return 1;
    }

    protected void RecycleCurShowContainer()
    {
        if(m_curItemContainers != null)
        {
            for(int i = Mathf.Max(0, m_curShowStartIndex), iMax = Mathf.Min(m_curItemContainers.Length - 1, m_curShowEndIndex); i <= iMax; i++)
            {
                var trans = m_curItemContainers[i].trans;
                if(trans)
                {
                    RecycleItemRectTrans(i, trans);
                }
                m_curItemContainers[i].trans = null;
            }
        }
    }

    protected void ResetCurShowIndex()
    {
        m_curShowStartIndex = -1;
        m_curShowEndIndex = -1;
        m_preFindIndex = -1;
    }

    protected bool CheckItemShouldShow(Rect contentViewRect, int index)
    {
        return contentViewRect.Overlaps(m_curItemContainers[index].rect);
    }

    /// <summary>
    /// 填充当前应该填充的items
    /// </summary>
    protected void TryUpdateCurItems()
    {
        if(m_dataCount <= 0)
        {
            return;
        }
        var curCheckViewRect = new Rect(m_contentViewRect.position - content.anchoredPosition, m_contentViewRect.size);
        if(curCheckViewRect.width < 0 || curCheckViewRect.height < 0)
        {
            return;
        }
        int preFindIndex = m_preFindIndex;
        m_preFindIndex = -1;
        if(preFindIndex < 0 || preFindIndex >= m_dataCount)
        {
            if(m_curShowStartIndex == -1 || m_curShowEndIndex == -1)
            {
                float scale = 0;
                if(horizontal)
                {
                    scale = -content.anchoredPosition.x / content.sizeDelta.x;
                }
                else if(vertical)
                {
                    scale = content.anchoredPosition.y / content.sizeDelta.y;
                }
                preFindIndex = Mathf.Clamp((int)((m_dataCount - 1) * scale), 0, m_dataCount - 1);
            }
            else
            {
                preFindIndex = (m_curShowStartIndex + m_curShowEndIndex) / 2;
            }
        }
        bool curShown = CheckItemShouldShow(curCheckViewRect, preFindIndex);
        int startFound = 0;
        int endFound = m_dataCount - 1;
        if(curShown)
        {//寻找起点显示，往两边找到第一个不显示的则为起终点
            for(int i = preFindIndex - 1; i >= 0; i--)
            {
                if(!CheckItemShouldShow(curCheckViewRect, i))
                {
                    startFound = i + 1;
                    break;
                }
            }
            for(int i = preFindIndex + 1; i < m_dataCount; i++)
            {
                if(!CheckItemShouldShow(curCheckViewRect, i))
                {
                    endFound = i - 1;
                    break;
                }
            }
        }
        else
        {//寻找起点不显示，变动极大，往两边找到显示的则往那一侧继续找到对应点
            int minFoundIndex = -1;
            int maxFoundIndex = -1;
            for(int i = 1, iMax = Mathf.Max(preFindIndex, m_dataCount - preFindIndex - 1); i <= iMax; i++)
            {
                if(preFindIndex >= i && CheckItemShouldShow(curCheckViewRect, preFindIndex - i))
                {
                    minFoundIndex = preFindIndex - i;
                    break;
                }
                if(preFindIndex + i < m_dataCount && CheckItemShouldShow(curCheckViewRect, preFindIndex + i))
                {
                    maxFoundIndex = preFindIndex + i;
                    break;
                }
            }
            if(minFoundIndex != -1)
            {
                endFound = minFoundIndex;
                for(int i = endFound - 1; i >= 0; i--)
                {
                    if(!CheckItemShouldShow(curCheckViewRect, i))
                    {
                        startFound = i + 1;
                        break;
                    }
                }
            }
            else if(maxFoundIndex != -1)
            {
                startFound = maxFoundIndex;
                for(int i = startFound + 1; i < m_dataCount; i++)
                {
                    if(!CheckItemShouldShow(curCheckViewRect, i))
                    {
                        endFound = i - 1;
                        break;
                    }
                }
            }
            else
            {
                startFound = -1;
                endFound = -1;
#if UNITY_EDITOR
                Debug.LogWarning("没有找到可显示的Item:" + name);
#endif
            }
        }
        FillItems(startFound, endFound);
    }

    void SetItemRectTrans(int index)
    {
        var rectTrans = GetItemRectTrans(index);
        rectTrans.SetParent(content, false);
        var rect = m_curItemContainers[index].rect;
        if(m_layoutType == LoopScrollLayoutType.HorizontalAfterVertical)
        {
            var realSize = m_curItemContainers[index].realSize;
            var realX = m_gridGroupAlignReverse ? rect.x + (rect.width - realSize) : rect.x;
            rectTrans.anchoredPosition = new Vector2(realX, rect.y);
            rectTrans.sizeDelta = new Vector2(realSize, rect.height);
        }
        else if(m_layoutType == LoopScrollLayoutType.VerticalAfterHorizontal)
        {
            var realSize = m_curItemContainers[index].realSize;
            var realY = m_gridGroupAlignReverse ? rect.y : rect.y + (rect.height - realSize);
            rectTrans.anchoredPosition = new Vector2(rect.x, realY);
            rectTrans.sizeDelta = new Vector2(rect.width, realSize);
        }
        else
        {
            rectTrans.anchoredPosition = rect.position;
            rectTrans.sizeDelta = rect.size;
        }
        m_curItemContainers[index].trans = rectTrans;
        DoUpdateItem(index, rectTrans);
    }

    void FillItems(int startFoundIndex, int endFoundIndex)
    {
        if(startFoundIndex == -1 || endFoundIndex == -1)
        {//没有显示
            RecycleCurShowContainer();
        }
        else if(m_curShowStartIndex == -1 || m_curShowEndIndex == -1)
        {
            for(int i = startFoundIndex; i <= endFoundIndex; i++)
            {
                SetItemRectTrans(i);
            }
        }
        else
        {
            bool recycleDirty = false;
            if(startFoundIndex > m_curShowStartIndex)
            {//移除当前显示起点到目标显示起点
                for(int i = m_curShowStartIndex, iMax = Mathf.Min(startFoundIndex - 1, m_curShowEndIndex); i <= iMax; i++)
                {
                    RecycleItemRectTransToTempPool(i);
                }
                recycleDirty = true;
            }
            if(endFoundIndex < m_curShowEndIndex)
            {//移除目标显示终点到当前显示终点
                for(int i = Mathf.Max(endFoundIndex + 1, m_curShowStartIndex); i <= m_curShowEndIndex; i++)
                {
                    RecycleItemRectTransToTempPool(i);
                }
                recycleDirty = true;
            }
            if(startFoundIndex < m_curShowStartIndex)
            {//添加目标显示起点到当前显示起点
                for(int i = startFoundIndex, iMax = Mathf.Min(m_curShowStartIndex - 1, endFoundIndex); i <= iMax; i++)
                {
                    SetItemRectTrans(i);
                }
            }
            if(endFoundIndex > m_curShowEndIndex)
            {//添加当前显示终点到目标显示终点
                for(int i = Mathf.Max(m_curShowEndIndex + 1, startFoundIndex); i <= endFoundIndex; i++)
                {
                    SetItemRectTrans(i);
                }
            }
            if(recycleDirty)
            {
                RecycleAllTempPoolToPool();
            }
        }
        m_curShowStartIndex = startFoundIndex;
        m_curShowEndIndex = endFoundIndex;
    }

    void DoUpdateItem(int itemIndex, RectTransform itemTrans)
    {
        if(itemTrans)
        {
            UpdateItem(itemIndex, itemTrans.gameObject);
            var dragItem = itemTrans.GetComponent<ILoopScrollDragItem>();
            if(dragItem != null)
            {
                if(dragItem.LoopDragItemSetter == null)
                {
                    dragItem.LoopDragItemSetter = OnSetDragItem;
                }
            }
        }
    }

    /// <summary>
    /// index模式下的更新数据
    /// </summary>
    /// <param name="dataCount"></param>
    /// <param name="targetIndex">默认-1保持原有进度</param>
    protected void UpdateData(int dataCount, int targetIndex = -1)
    {
        m_dataCount = dataCount;
        ReCalculateContainer();
        if(targetIndex != -1)
        {
            MoveToIndex(targetIndex);
        }
    }

    /// <summary>
    /// 移动到目标索引处
    /// </summary>
    /// <param name="targetIndex"></param>
    public void MoveToIndex(int targetIndex)
    {
        if(IsViewEnabled && targetIndex >= 0 && targetIndex < m_dataCount)
        {
            StopMovement();
            m_preFindIndex = targetIndex;
            Rect rect = m_curItemContainers[targetIndex].rect;
            if(horizontal)
            {
                horizontalNormalizedPosition = GetHorizontalPosScale(rect.x);
            }
            else if(vertical)
            {
                verticalNormalizedPosition = GetVerticalPosScale(rect.yMax);
            }
        }
    }

    /// <summary>
    /// 刷新当前显示的item
    /// </summary>
    public void RefreshShowItems()
    {
        if(m_curShowStartIndex != -1 && m_curShowEndIndex != -1)
        {
            for(int i = m_curShowStartIndex; i <= m_curShowEndIndex; i++)
            {
                DoUpdateItem(i, m_curItemContainers[i].trans);
            }
        }
    }

    /// <summary>
    /// 清除显示数据
    /// </summary>
    public virtual void ClearShowData()
    {
        UpdateData(0, 0);
    }

    /// <summary>
    /// 停止移动
    /// </summary>
    public void StopMovement()
    {
        m_scrollRect.StopMovement();
    }

    /// <summary>
    /// 清除数据，清除容器，重置索引，清空缓存池
    /// 此步骤执行后等同于刚初始化的LoopScrollView
    /// </summary>
    public void ClearAll()
    {
        RestoreLastDragInfo();
        ClearShowData();
        RecycleCurShowContainer();
        ResetCurShowIndex();
        m_curItemContainers = sEmptyContainer;
        ClearAllPoolObject();
    }
    #endregion

    #region drag
    RectTransform m_lastDragContent;
    Scrollbar m_lastDragHorizontalScrollbar;
    Scrollbar m_lastDragVerticalScrollbar;
    /// <summary>
    /// 当前预备拖起的item
    /// </summary>
    ILoopScrollDragItem m_curPreDragItem;
    float m_dragItemThreshold = 70f;
    /// <summary>
    /// 拖起角度阈值
    /// </summary>
    public float DragItemThreshold { get { return m_dragItemThreshold; } set { m_dragItemThreshold = value; } }
    bool m_isDragingItem;
    public bool IsDragingItem { get { return m_isDragingItem; } }

    void OnSetDragItem(ILoopScrollDragItem item)
    {
        m_curPreDragItem = item;
    }

    void ResetLastDragInfo()
    {
        StopMovement();
        m_lastDragContent = content;
        m_lastDragHorizontalScrollbar = horizontalScrollbar;
        m_lastDragVerticalScrollbar = verticalScrollbar;

        content = null;
        horizontalScrollbar = null;
        verticalScrollbar = null;
    }

    void RestoreLastDragInfo()
    {
        m_isDragingItem = false;
        m_curPreDragItem = null;
        if(m_lastDragContent)
        {
            content = m_lastDragContent;
            m_lastDragContent = null;
        }
        if(m_lastDragHorizontalScrollbar)
        {
            horizontalScrollbar = m_lastDragHorizontalScrollbar;
            m_lastDragHorizontalScrollbar = null;
        }
        if(m_lastDragVerticalScrollbar)
        {
            verticalScrollbar = m_lastDragVerticalScrollbar;
            m_lastDragVerticalScrollbar = null;
        }
    }

    void CheckBeginDragItem(PointerEventData eventData)
    {
        if(!m_isDragingItem && m_curPreDragItem != null)
        {
            Vector2 checkAxis = horizontal ? Vector2.right : Vector2.up;
            var delta = eventData.delta;
            var angle = Mathf.Abs(Vector2.Angle(delta, checkAxis));
            if(angle >= m_dragItemThreshold && angle <= 180 - m_dragItemThreshold)
            {
                m_isDragingItem = true;
                ResetLastDragInfo();
                m_curPreDragItem.CallOnBeginDrag(eventData);
            }
        }
    }

    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        CheckBeginDragItem(eventData);
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        CheckBeginDragItem(eventData);
        if(m_isDragingItem && m_curPreDragItem != null)
        {
            m_curPreDragItem.CallOnDrag(eventData);
        }
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        if(m_isDragingItem && m_curPreDragItem != null)
        {
            m_curPreDragItem.CallOnEndDrag(eventData);
        }
        RestoreLastDragInfo();
    }
    #endregion
}

public class UILoopScrollView : UILoopScrollViewBase
{
    Action<int, object, GameObject> m_updateItemCallback;
    Func<object, Vector2> m_customItemSizeGetter;
    Func<object, GameObject> m_customItemTempGetter;

    protected IList m_dataList;
    public IList DataList
    {
        get
        {
            return m_dataList;
        }
    }

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
        if(m_updateItemCallback != null && m_dataList != null && itemIndex < m_dataList.Count)
        {
            try
            {
                m_updateItemCallback.Invoke(itemIndex, m_dataList[itemIndex], itemObject);
            }
            catch(Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

    protected override Vector2 GetItemSize(int itemIndex)
    {
        if(m_customItemSizeGetter != null && m_dataList != null && itemIndex < m_dataList.Count)
        {
            return m_customItemSizeGetter.Invoke(m_dataList[itemIndex]);
        }
        return m_defaultItemSize;
    }

    protected override GameObject GetItemTemp(int itemIndex)
    {
        if(m_customItemTempGetter != null && m_dataList != null && itemIndex < m_dataList.Count)
        {
            return m_customItemTempGetter.Invoke(m_dataList[itemIndex]);
        }
        return m_defaultItemTemp;
    }

    void ResetCallback()
    {
        m_updateItemCallback = null;
        m_customItemSizeGetter = null;
        m_customItemTempGetter = null;
    }

    /// <summary>
    /// 初始化（通过数据回调）
    /// </summary>
    /// <param name="layoutType"></param>
    /// <param name="spacing">spacing</param>
    /// <param name="defaultItemSize">默认item大小</param>
    /// <param name="itemTemplate">默认item模板</param>
    /// <param name="updateItemCallback">更新item回调</param>
    /// <param name="customItemSizeGetter">可变itemSize则传入item大小获取器</param>
    /// <param name="customItemTempGetter">可变item模板则传入item模板获取器</param>
    /// <param name="gridGroupAlignReverse">分组排版时，一组大小不一的item是否反向靠拢,默认false，Layout为HorizontalAfterVertical则向右靠拢，Layout为VerticalAfterHorizontal则向上靠拢</param>
    public void Init(LoopScrollLayoutType layoutType,
        Vector2 spacing,
        Vector2 defaultItemSize,
        GameObject itemTemplate,
        Action<int, object, GameObject> updateItemCallback = null,
        Func<object, Vector2> customItemSizeGetter = null,
        Func<object, GameObject> customItemTempGetter = null,
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
    /// <param name="dataList"></param>
    /// <param name="targetIndex">默认-1则保持原有滚动进度，否则移动到指定索引处</param>
    public void Show(IList dataList, int targetIndex = -1)
    {
        m_dataList = dataList;
        UpdateData(dataList != null ? dataList.Count : 0, targetIndex);
    }

    /// <summary>
    /// 清除显示数据
    /// </summary>
    public override void ClearShowData()
    {
        base.ClearShowData();
        m_dataList = null;
    }
}
