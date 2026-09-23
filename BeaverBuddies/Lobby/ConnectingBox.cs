using BeaverBuddies.Util;
using System;
using Timberborn.Common;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// "Connecting to Kyler…" with Cancel (D21): the game's own dialog box (Core/DialogBox) with only its Cancel button,
    /// shown from Join co-op game or an accepted invite until the host's waiting room opens, the save arrives (a hosted
    /// save; the scene change takes it away) or an error is shown. It replaced "Joined! Receiving map..." (Steam only),
    /// and a direct join now says something too.
    /// </summary>
    internal sealed class ConnectingBox : IPanelController
    {
        private readonly PanelStack _panelStack;
        private readonly VisualElement _root;
        private readonly Action _onCancel;
        private bool closed;
        private bool popWhenOnTop;

        private ConnectingBox(VisualElementLoader loader, PanelStack panelStack, string message, Action onCancel)
        {
            _panelStack = panelStack;
            _onCancel = onCancel;
            _root = loader.LoadVisualElement("Core/DialogBox");
            _root.Q<Label>("Message").text = message;
            _root.Q<Button>("ConfirmButton")?.ToggleDisplayStyle(false);
            _root.Q<Button>("InfoButton")?.ToggleDisplayStyle(false);
            Button cancel = _root.Q<Button>("CancelButton");
            cancel.text = RegisteredLocalizationService.T(CommonLocKeys.CancelKey);
            cancel.clicked += OnUICancelled;
        }

        public static ConnectingBox Show(VisualElementLoader loader, PanelStack panelStack, string message, Action onCancel)
        {
            var box = new ConnectingBox(loader, panelStack, message, onCancel);
            panelStack.PushDialog(box);
            return box;
        }

        public VisualElement GetPanel() => _root;

        // Enter does nothing here: there is nothing to confirm.
        public bool OnUIConfirmed() => false;

        public void OnUICancelled()
        {
            if (closed)
            {
                // Closed while something covered it, and its owner no longer polls it: Cancel and Esc still take it away
                // (1.4.0-rc5 review, A2).
                Poll();
                return;
            }
            Close();
            _onCancel?.Invoke();
        }

        /// <summary>Takes the box away (now, or once whatever covers it has gone: PanelStack pops only its top panel).</summary>
        public void Close()
        {
            closed = true;
            if (_panelStack.IsPanelOnTop(this)) _panelStack.Pop(this);
            else popWhenOnTop = true;
        }

        /// <summary>Called every frame by its owner, to finish a close that had to wait.</summary>
        public void Poll()
        {
            if (!popWhenOnTop || !_panelStack.IsPanelOnTop(this)) return;
            popWhenOnTop = false;
            _panelStack.Pop(this);
        }

        public bool IsClosed => closed && !popWhenOnTop;
    }
}
