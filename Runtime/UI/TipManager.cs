using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JulyGame
{
    /// <summary>
    /// Toast Tip 管理器 — 由 UISystem 持有，负责 Tip 对象池、容器、显示逻辑。
    /// 首次 ShowTip 时懒加载预制体，加载期间的消息会被丢弃。
    /// </summary>
    internal sealed class TipManager
    {
        private const string TipPrefabPath = "UITipItem";
        private const int PoolMaxSize = 10;
        private const float DefaultDuration = 2f;
        private const float FadeOutDuration = 0.3f;
        private const float Spacing = 10f;
        private const float MoveUpDuration = 0.2f;
        private const float EnterOffset = 50f;
        private const float EnterDuration = 0.2f;

        private readonly Func<IResourceSystem> _resourceResolver;

        private Transform _container;
        private GameObject _tipPrefab;
        private IDisposable _prefabHandle;

        private readonly Queue<UITipItem> _pool = new();
        private readonly List<UITipItem> _activeTips = new();

        private bool _loading;
        private bool _initialized;

        internal TipManager(Func<IResourceSystem> resourceResolver)
        {
            _resourceResolver = resourceResolver ?? throw new ArgumentNullException(nameof(resourceResolver));
        }

        internal void Initialize()
        {
            CreateContainer();
        }

        internal void Show(string message, float duration = 0)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (!_initialized)
            {
                if (!_loading) LoadPrefabAsync();
                return;
            }

            for (int i = 0; i < _activeTips.Count; i++)
            {
                if (_activeTips[i] != null && _activeTips[i].Message == message)
                {
                    _activeTips[i].RestartTimer();
                    return;
                }
            }

            var tip = GetFromPool();
            if (tip == null) return;

            MoveUpExisting(tip.GetHeight() + Spacing);

            var showDuration = duration > 0 ? duration : DefaultDuration;
            tip.Show(message, showDuration, FadeOutDuration, OnTipComplete,
                EnterOffset, EnterDuration);

            _activeTips.Add(tip);
        }

        internal void Shutdown()
        {
            _activeTips.Clear();
            _pool.Clear();
            _prefabHandle?.Dispose();
            _prefabHandle = null;
            _tipPrefab = null;
            _initialized = false;
            _loading = false;

            if (_container != null)
            {
                Object.Destroy(_container.gameObject);
                _container = null;
            }
        }

        private void CreateContainer()
        {
            var go = new GameObject("[TipContainer]");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            canvas.vertexColorAlwaysGammaSpace = true;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>().enabled = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 100);

            _container = go.transform;
        }

        private async void LoadPrefabAsync()
        {
            if (string.IsNullOrEmpty(TipPrefabPath)) return;

            _loading = true;

            try
            {
                var resource = _resourceResolver();
                if (resource == null)
                {
                    Debug.LogWarning("[TipManager] ResourceSystem not available");
                    _loading = false;
                    return;
                }

                var handle = await resource.LoadAssetAsync<GameObject>(TipPrefabPath);
                if (handle == null || !handle.IsValid)
                {
                    Debug.LogWarning($"[TipManager] Failed to load tip prefab: {TipPrefabPath}");
                    _loading = false;
                    return;
                }

                _prefabHandle = handle.MarkPermanent();
                _tipPrefab = handle.Asset;
                WarmupPool();
                _initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TipManager] Prefab load error: {ex.Message}");
            }
            finally
            {
                _loading = false;
            }
        }

        private void WarmupPool()
        {
            for (int i = 0; i < 2; i++)
            {
                var item = CreateItem();
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                    _pool.Enqueue(item);
                }
            }
        }

        private UITipItem CreateItem()
        {
            if (_tipPrefab == null || _container == null) return null;

            var go = Object.Instantiate(_tipPrefab, _container);
            var tip = go.GetComponent<UITipItem>();
            if (tip == null)
                tip = go.AddComponent<UITipItem>();
            go.SetActive(false);
            return tip;
        }

        private UITipItem GetFromPool()
        {
            while (_pool.Count > 0)
            {
                var item = _pool.Dequeue();
                if (item != null) return item;
            }
            return CreateItem();
        }

        private void MoveUpExisting(float offset)
        {
            for (int i = 0; i < _activeTips.Count; i++)
            {
                var tip = _activeTips[i];
                if (tip != null && tip.gameObject.activeSelf)
                    tip.MoveUp(offset, MoveUpDuration);
            }
        }

        private void OnTipComplete(UITipItem tip)
        {
            _activeTips.Remove(tip);

            if (tip == null) return;
            tip.gameObject.SetActive(false);
            tip.Reset();

            if (_pool.Count < PoolMaxSize)
                _pool.Enqueue(tip);
            else
                Object.Destroy(tip.gameObject);
        }
    }
}
