using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    internal static class ButtonInserter
    {
        /// <summary>
        /// A copy of <paramref name="previousButtonName"/>'s button, with its classes, just below it (or the copy already
        /// there). The copy is initialised as the game initialises the buttons it loads: its click sound (the classes'
        /// --click-sound) and clicks with any modifier held. A NineSliceButton: it draws the same nine-slice background as
        /// the game's LocalizableButton, whose initialiser needs a localization key (the mod sets its text itself).
        /// </summary>
        public static Button DuplicateOrGetButton(VisualElement root, string previousButtonName, string buttonName, Action<Button> init,
            VisualElementInitializer initializer)
        {
            var button = root.Q<Button>(buttonName);
            if (button != null)
            {
                return button;
            }
            var previousButton = root.Q<Button>(previousButtonName);
            var classList = previousButton.classList;
            button = new NineSliceButton();
            button.name = buttonName;
            button.classList.AddRange(classList);
            var parent = previousButton.parent;
            var index = parent.IndexOf(previousButton);
            previousButton.parent.Insert(index + 1, button);

            init(button);
            initializer?.InitializeVisualElement(button);

            return button;
        }
    }
}
