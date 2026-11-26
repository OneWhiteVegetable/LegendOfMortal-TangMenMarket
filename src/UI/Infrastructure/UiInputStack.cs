using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XingShangMod.UI.Infrastructure
{
    internal interface IUiCancelLayer
    {
        bool IsCancelLayerActive { get; }
        bool CancelFromInput();
    }

    internal sealed class UiFallbackInputModule : StandaloneInputModule
    {
        internal bool Retiring;
    }

    internal sealed class UiInputStack : IDisposable
    {
        private readonly List<IUiCancelLayer> _layers = new List<IUiCancelLayer>();
        private GameObject _ownedEventSystem;
        private UiFallbackInputModule _ownedInputModule;
        private EventSystem _hostEventSystem;
        private BaseInputModule _hostInputModule;
        private bool _inputSessionActive;
        private int _lastInputFrame = -1;
        private bool _disposed;

        internal void Register(IUiCancelLayer layer)
        {
            if (layer == null)
                throw new ArgumentNullException("layer");
            if (!_layers.Contains(layer))
                _layers.Add(layer);
        }

        internal void Activate(IUiCancelLayer layer)
        {
            if (layer == null)
                return;
            _layers.Remove(layer);
            _layers.Add(layer);
            EnsureEventSystem();
        }

        internal void Unregister(IUiCancelLayer layer)
        {
            _layers.Remove(layer);
        }

        internal void Tick()
        {
            if (_disposed || _layers.Count == 0 || _lastInputFrame == Time.frameCount)
                return;
            _lastInputFrame = Time.frameCount;
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            bool cancel = false;
            if (!escape)
            {
                try
                {
                    cancel = Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.JoystickButton1);
                }
                catch (Exception)
                {
                    cancel = Input.GetKeyDown(KeyCode.JoystickButton1);
                }
            }
            bool direction = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.DownArrow);
            if (direction || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0))
                UiFocusOrigin.FromKeyboard();
            if (!escape && !cancel && !direction)
                return;

            for (int i = _layers.Count - 1; i >= 0; i--)
            {
                IUiCancelLayer layer = _layers[i];
                if (layer == null || !layer.IsCancelLayerActive)
                    continue;
                if (escape || cancel)
                    layer.CancelFromInput();
                else if (layer is UiScreen)
                    ((UiScreen)layer).FocusForNavigation();
                break;
            }
        }

        internal void EnsureEventSystem()
        {
            if (_inputSessionActive && _hostEventSystem != null)
            {
                _hostEventSystem.enabled = true;
                if (_hostInputModule != null)
                    _hostInputModule.enabled = true;
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null && _ownedEventSystem != null)
                eventSystem = _ownedEventSystem.GetComponent<EventSystem>();
            if (eventSystem == null)
            {
                _ownedEventSystem = new GameObject("XingShangMod.EventSystem", typeof(EventSystem));
                UnityEngine.Object.DontDestroyOnLoad(_ownedEventSystem);
                eventSystem = _ownedEventSystem.GetComponent<EventSystem>();
            }

            BaseInputModule inputModule = null;
            foreach (BaseInputModule candidate in eventSystem.GetComponents<BaseInputModule>())
            {
                UiFallbackInputModule fallback = candidate as UiFallbackInputModule;
                if (fallback != null && fallback.Retiring)
                    continue;
                inputModule = candidate;
                break;
            }
            _hostEventSystem = eventSystem;
            if (inputModule == null)
            {
                _ownedInputModule = eventSystem.gameObject.AddComponent<UiFallbackInputModule>();
                inputModule = _ownedInputModule;
            }
            _hostInputModule = inputModule;
            _inputSessionActive = true;
            eventSystem.enabled = true;
            inputModule.enabled = true;
        }

        internal void CloseAll()
        {
            if (!_inputSessionActive)
                return;
            if (_hostInputModule != null)
                _hostInputModule.enabled = true;
            if (_hostEventSystem != null)
                _hostEventSystem.enabled = true;
            _inputSessionActive = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            CloseAll();
            _disposed = true;
            _layers.Clear();
            if (_ownedInputModule != null &&
                (_ownedEventSystem == null || _ownedInputModule.gameObject != _ownedEventSystem))
            {
                _ownedInputModule.Retiring = true;
                _ownedInputModule.enabled = false;
                UnityEngine.Object.Destroy(_ownedInputModule);
            }
            if (_ownedEventSystem != null)
            {
                // Destroy is deferred. Unregister this fallback now so a new front end opened
                // in the same frame cannot adopt the EventSystem that is about to disappear.
                _ownedEventSystem.SetActive(false);
                UnityEngine.Object.Destroy(_ownedEventSystem);
            }
            _ownedInputModule = null;
            _ownedEventSystem = null;
            _hostEventSystem = null;
            _hostInputModule = null;
        }
    }
}
