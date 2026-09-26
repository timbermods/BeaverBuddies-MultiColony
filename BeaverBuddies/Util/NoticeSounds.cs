using System;
using System.Collections.Generic;
using Timberborn.CoreSound;
using Timberborn.RootProviders;
using Timberborn.SoundSystem;
using UnityEngine;

namespace BeaverBuddies.Util
{
    /// <summary>
    /// The mod's notice sounds, both built into the game as the Speaker's chimes (Timberborn 1.1.2.4, the SpeakerSound
    /// Chime1 and Chime2 blueprints): one as a trade message that needs an answer appears, one as a chat message from
    /// another player arrives. Played flat (2D) through the game's interface volume, as a Speaker set to non-spatial
    /// plays them, and each at most once a second, so a burst chimes once. Display only: a sound that cannot play is
    /// skipped, and never stops what asked for it.
    /// </summary>
    public class NoticeSounds
    {
        public const string TradeSound = "Environment.Buildings.Speaker.Chime_01";
        public const string ChatSound = "Environment.Buildings.Speaker.Chime_02";
        const float MinIntervalSeconds = 1;
        // The game's own interface sounds' priority (GameUISoundController).
        const int Priority = 10;

        readonly ISoundSystem _soundSystem;
        readonly RootObjectProvider _rootObjectProvider;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        readonly HashSet<string> broken = new HashSet<string>();
        GameObject emitter;

        public NoticeSounds(ISoundSystem soundSystem, RootObjectProvider rootObjectProvider)
        {
            _soundSystem = soundSystem;
            _rootObjectProvider = rootObjectProvider;
        }

        public void Play(string sound)
        {
            if (broken.Contains(sound)) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(sound, out float last) && now - last < MinIntervalSeconds) return;
            lastPlayed[sound] = now;
            try
            {
                if (emitter == null) emitter = _rootObjectProvider.CreateRootObject("BeaverBuddiesNoticeSounds");
                _soundSystem.SetCustomMixer(emitter, sound, MixerNames.UIMixerNameKey);
                _soundSystem.PlaySound2D(emitter, sound, Priority);
            }
            catch (Exception error)
            {
                broken.Add(sound);
                Plugin.LogWarning($"The notice sound {sound} could not play and is off for this game: {error.Message}");
            }
        }
    }
}
