using BeaverBuddies.Util;
using System;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.FileSystem;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.InputSystem;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// Names the settlement before a waiting room opens (D8), in the game's own text-input dialog (Core/InputBox, the box
    /// the game renames a beaver or a building with). It needs only CoreStyle, which every scene loads, so it looks the same
    /// in the main menu as the game's own dialogs there. The in-game settlement box (Game/SettlementNameBox) was used
    /// until 1.4.0-beta22: its frame and layout come from the Game scene's style sheets, and in the main menu it drew
    /// without them (the first playtest of the waiting room).
    /// The name is checked as the game checks it (GameSaveRepository.CreateDirectoryForSettlement, with the game's own
    /// messages); a taken or invalid name keeps the box open. It goes into the new game's configuration, so the made world
    /// never asks.
    /// </summary>
    internal sealed class SettlementNamePanel : IPanelController
    {
        // The game's own limit for a settlement's name (SettlementNameBoxShower).
        private const int CharacterLimit = 50;
        private const string MessageLocKey = "Saving.NameSettlement";
        private static readonly string TakenNameLocKey = "Saving.TakenName";
        private static readonly string InvalidNameLocKey = "Saving.InvalidName";

        public static readonly string[] ClassesUsed = { "sliced-border", "sliced-border--nontransparent", "box", "box__text",
            "box__content-margin", "text-field", "box__input", "menu-button", "menu-button--medium" };

        private readonly PanelStack _panelStack;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly Action<string> _onNamed;
        private readonly VisualElement _root;
        private readonly TextField _input;
        // Enter can arrive twice (the field's own confirm and the panel stack's): the box closes once.
        private bool closed;

        private SettlementNamePanel(PanelStack panelStack, GameSaveRepository gameSaveRepository, DialogBoxShower dialogBoxShower,
            VisualElementLoader loader, VisualElementInitializer initializer, InputService inputService, string initialName,
            Action<string> onNamed)
        {
            _panelStack = panelStack;
            _gameSaveRepository = gameSaveRepository;
            _dialogBoxShower = dialogBoxShower;
            _onNamed = onNamed;

            // Loaded and initialised as InputBoxShower.Create does (its OK and Cancel are the game's localized buttons).
            _root = loader.LoadVisualElement("Core/InputBox");
            Label message = _root.Q<Label>("Message");
            message.text = RegisteredLocalizationService.T(MessageLocKey);
            // The box's own text class (box__text) sits left; a one-line question reads better centred, as DialogBox does.
            message.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            _input = _root.Q<TextField>("Input");
            _input.maxLength = CharacterLimit;
            _input.SetValueWithoutNotify(initialName ?? "");
            _input.Q<TextElement>()?.SetConfirmCancelActions(inputService, () => OnUIConfirmed(), OnUICancelled);

            // OK reads Next: the waiting room comes after it, as the New Game pages' Next leads on.
            Button confirm = _root.Q<Button>("ConfirmButton");
            confirm.text = RegisteredLocalizationService.T(CommonLocKeys.NavigationNextKey);
            confirm.RegisterCallback<ClickEvent>(_ => OnUIConfirmed());
            _root.Q<Button>("CancelButton").RegisterCallback<ClickEvent>(_ => OnUICancelled());
        }

        /// <summary>Asks for the settlement's name; <paramref name="onNamed"/> gets it once the game accepts it.</summary>
        public static void Show(PanelStack panelStack, GameSaveRepository gameSaveRepository, DialogBoxShower dialogBoxShower,
            VisualElementLoader loader, VisualElementInitializer initializer, InputService inputService, string initialName,
            Action<string> onNamed)
        {
            var panel = new SettlementNamePanel(panelStack, gameSaveRepository, dialogBoxShower, loader, initializer, inputService,
                initialName, onNamed);
            // A dialog, as the game shows its input box: over the page, which waits under it.
            panelStack.PushDialog(panel);
            panel._input.Focus();
            panel._input.SelectAll();
        }

        public VisualElement GetPanel() => _root;

        public bool OnUIConfirmed()
        {
            string name = _input.text;
            if (closed || string.IsNullOrEmpty(name)) return true;
            switch (_gameSaveRepository.CreateDirectoryForSettlement(name))
            {
                case DirectoryCreationResult.OK:
                    closed = true;
                    if (_panelStack.IsPanelOnTop(this)) _panelStack.Pop(this);
                    _onNamed(name);
                    break;
                case DirectoryCreationResult.NameTaken:
                    _dialogBoxShower.Create().SetLocalizedMessage(TakenNameLocKey).Show();
                    break;
                default:
                    _dialogBoxShower.Create().SetLocalizedMessage(InvalidNameLocKey).Show();
                    break;
            }
            return true;
        }

        public void OnUICancelled()
        {
            if (closed) return;
            closed = true;
            if (_panelStack.IsPanelOnTop(this)) _panelStack.Pop(this);
        }
    }
}
