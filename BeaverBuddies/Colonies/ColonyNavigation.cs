using BeaverBuddies.Activity;
using BeaverBuddies.Util;
using System;
using Timberborn.CameraSystem;
using Timberborn.GameDistricts;
using Timberborn.InputSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Getting around a shared map: the Home key brings the camera back to this player's own colony (its biggest
    /// district center, the one the batch control window opens on; in a shared game, the biggest), and a click on a player's name in the connection
    /// panel takes the camera to where that player is (their cursor, or what they have selected). Display only: the
    /// camera is each computer's own.
    /// </summary>
    public class ColonyNavigation : RegisteredSingleton, IPostLoadableSingleton, IInputProcessor
    {
        public const string HomeKeyBindingId = "BeaverBuddies.KeyBind.GoHome";

        private readonly InputService _inputService;
        private readonly CameraService _cameraService;
        private readonly CameraTargeter _cameraTargeter;
        private readonly ColonyViewService _colonyViewService;
        private readonly ColonyRulesService _colonyRulesService;

        public static ColonyNavigation Instance => SingletonManager.GetSingleton<ColonyNavigation>();

        public ColonyNavigation(InputService inputService, CameraService cameraService, CameraTargeter cameraTargeter,
            ColonyViewService colonyViewService, ColonyRulesService colonyRulesService)
        {
            _inputService = inputService;
            _cameraService = cameraService;
            _cameraTargeter = cameraTargeter;
            _colonyViewService = colonyViewService;
            _colonyRulesService = colonyRulesService;
        }

        public void PostLoad() => _inputService.AddInputProcessor(this);

        public bool ProcessInput()
        {
            if (!_inputService.IsKeyDown(HomeKeyBindingId)) return false;
            GoHome();
            return true;
        }

        /// <summary>The camera to this player's own colony (its biggest district). False when there is none.</summary>
        public bool GoHome()
        {
            try
            {
                DistrictCenter home = _colonyViewService.MainDistrict();
                if (!home) return false;
                _cameraTargeter.CenterCameraOn(home.GetComponent<SelectableObject>());
                return true;
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not go home: " + error.Message);
                return false;
            }
        }

        /// <summary>The camera to where another player is: their cursor on the map, else what they have selected.</summary>
        public void GoToPlayer(int playerId, string name)
        {
            PlayerActivityService activity = SingletonManager.GetSingleton<PlayerActivityService>();
            try
            {
                if (activity != null && activity.TryLocate(playerId, out Vector3 point))
                {
                    _cameraService.MoveTargetTo(point);
                    return;
                }
                _colonyRulesService.ShowNotice(string.Format(RegisteredLocalizationService.T("BeaverBuddies.Panel.PlayerNotOnMap"), name), warning: false);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not go to a player: " + error.Message);
            }
        }
    }
}
