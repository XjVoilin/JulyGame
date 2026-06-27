using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCommon;
using UnityEngine;

namespace JulyGame
{
    public interface IUISystem : ISupportMultipleSource<IUIWindowProvider>
    {
        #region UIRoot Physical Stage

        Camera UICamera { get; }
        Transform StagingRoot { get; }
        bool IsMaskActive { get; }
        Transform GetLayer(UILayer layer);
        Canvas GetLayerCanvas(UILayer layer);
        void ShowMask();
        void HideMask();

        #endregion

        UIOpenOptions GetWindowConfig(int windowId);

        #region Open

        void Open(int windowId, object data = null, CancellationToken ct = default);
        UniTask<UIView> OpenAsync(int windowId, object data = null, CancellationToken ct = default);
        UniTask<UIView> OpenAsync(UIOpenOptions options, CancellationToken ct = default);
        UniTask<FrameworkResult<UIView>> TryOpenAsync(UIOpenOptions options, CancellationToken ct = default);

        #endregion

        #region Close

        void Close(int windowId, bool destroy = true, UIAnimationType? animationType = null);
        void Close(UIView view, bool destroy = true, UIAnimationType? animationType = null);
        UniTask CloseAsync(int windowId, bool destroy = true, UIAnimationType? animationType = null,
            CancellationToken ct = default);
        UniTask CloseAsync(UIView view, bool destroy = true, UIAnimationType? animationType = null,
            CancellationToken ct = default);
        void CloseAll(bool destroy = false);
        void CloseLayer(UILayer layer, bool destroy = false, int excludeWindowId = -1);
        bool GoBack();

        #endregion

        #region Query

        bool IsOpen(int windowId);
        bool TryGet(int windowId, out UIView view);
        bool TryGetUIInfo(int windowId, out UIInfo info);
        int GetStackDepth();
        int GetLayerUICount(UILayer layer);

        #endregion

        #region Tip

        void ShowTip(string message, float duration = 2f);

        #endregion

        #region Preload

        UniTask PreloadAsync(string windowName, CancellationToken ct = default);
        UniTask<bool[]> PreloadBatchAsync(string[] windowNames, CancellationToken ct = default);
        void ReleasePreload(string windowName);
        bool IsPreloaded(string windowName);

        #endregion
    }
}
