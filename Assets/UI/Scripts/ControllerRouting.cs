using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ControllerRouting : MonoBehaviour
{
    public static ControllerRouting Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [Header("Cài đặt chung")]
    [SerializeField] private UIDocument _mainDocument;

    [System.Serializable]
    public struct PageEntry
    {
        public PageID id;
        public VisualTreeAsset asset;
    }
    
    [Header("Danh sách trang")]
    [SerializeField] private List<PageEntry> _pages;

    private IDisposable _currentController;

    private void Start()
    {
        GoToPage(PageID.Login);
    }

    public void GoToPage(PageID pageId)
    {
        var pageEntry = _pages.Find(p => p.id == pageId);
        if (pageEntry.asset == null)
        {
            Debug.LogError($"Lỗi: Không tìm thấy trang '{pageId}'!");
            return;
        }

        // 1. Dọn dẹp Controller cũ
        if (_currentController != null)
        {
            _currentController.Dispose(); 
            _currentController = null;
        }

        // 2. Setup UI mới
        var root = _mainDocument.rootVisualElement;
        root.Clear();

        var newPageRoot = pageEntry.asset.CloneTree();
        newPageRoot.style.flexGrow = 1; 
        root.Add(newPageRoot);

    
        _currentController = ControllerFactory.CreateController(pageId, newPageRoot);
    }
}