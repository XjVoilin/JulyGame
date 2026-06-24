#if JULYGF_DEBUG
using System;
using JulyArch;
using TMPro;

namespace JulyGame
{
    public class GMSystem : SystemBase, IGMSystem
    {
        private readonly GMRegistry _registry = new();
        private GMOverlayRoot _overlayRoot;

        public void Register(Type type)
        {
            _registry.Register(type);
        }

        public void Build(TMP_FontAsset font = null)
        {
            if (font != null) GMUGUIPanel.OverrideFont = font;
            if (_overlayRoot != null) return;
            _overlayRoot = GMOverlayRoot.Create(_registry.Categories);
        }

        protected override void OnShutdown()
        {
            if (_overlayRoot != null)
            {
                UnityEngine.Object.Destroy(_overlayRoot.gameObject);
                _overlayRoot = null;
            }

            _registry.Clear();
        }
    }
}
#endif
