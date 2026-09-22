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
    /// The game's own settlement-name box (Game/SettlementNameBox, the box a solo new game shows once its start is
    /// placed), shown on the Game Mode page before a waiting room opens (D8). The name is checked as the game checks it
    /// (GameSaveRepository.CreateDirectoryForSettlement, with the game's own messages) and goes into the new game's
    /// configuration, so the made world never asks. Its "change start location" parts are hidden (they need a world),
    /// its Start! reads Next, and a Cancel goes back.
    /// </summary>
    internal sealed class SettlementNamePanel : IPanelController
    {
        private const int CharacterLimit = 50;
        private static readonly string TakenNameLocKey = "Saving.TakenName";
        private static readonly string InvalidNameLocKey = "Saving.InvalidName";

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

            _root = loader.LoadVisualElement("Game/SettlementNameBox");
            _root.Q<Button>("RelocateButton")?.ToggleDisplayStyle(false);
            _root.Q<Button>("ResetStartLocation")?.ToggleDisplayStyle(false);
            _input = _root.Q<TextField>("Input");
            _input.maxLength = CharacterLimit;
            _input.SetValueWithoutNotify(initialName ?? "");
            _input.Q<TextElement>()?.SetConfirmCancelActions(inputService, () => OnUIConfirmed(), OnUICancelled);

            Button confirm = _root.Q<Button>("ConfirmButton");
            confirm.text = RegisteredLocalizationService.T(CommonLocKeys.NavigationNextKey);
            confirm.RegisterCallback<ClickEvent>(_ => OnUIConfirmed());

            // A Cancel beside it, drawn as its neighbour is (the game's box can't be cancelled: a world is waiting).
            var cancel = new NineSliceButton { text = RegisteredLocalizationService.T(CommonLocKeys.CancelKey) };
            cancel.AddToClassList("menu-button");
            cancel.style.marginRight = 4;
            confirm.parent.Insert(confirm.parent.IndexOf(confirm), cancel);
            initializer.InitializeVisualElement(cancel);
            cancel.clicked += OnUICancelled;
        }

        /// <summary>Asks for the settlement's name; <paramref name="onNamed"/> gets it once the game accepts it.</summary>
        public static void Show(PanelStack panelStack, GameSaveRepository gameSaveRepository, DialogBoxShower dialogBoxShower,
            VisualElementLoader loader, VisualElementInitializer initializer, InputService inputService, string initialName,
            Action<string> onNamed)
        {
            var panel = new SettlementNamePanel(panelStack, gameSaveRepository, dialogBoxShower, loader, initializer, inputService,
                initialName, onNamed);
            panelStack.PushOverlay(panel);
            panel._input.Focus();
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
