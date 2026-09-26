using BeaverBuddies.Colonies;
using BeaverBuddies.IO;
using System;
using Timberborn.BaseComponentSystem;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.TemplateSystem;
using static BeaverBuddies.SingletonManager;

namespace BeaverBuddies.Events
{
    public interface IReplayContext
    {
        T GetSingleton<T>();
    }

    /// <summary>
    /// A replayed action names something this game does not have (a building from a mod only another player runs), found
    /// before any of the action was played. A guest that meets one leaves quietly (ReplayService): it can no longer keep
    /// up with the host, but nothing has gone wrong for anyone else.
    /// </summary>
    public class MissingContentException : Exception
    {
        public MissingContentException(string message) : base(message) { }
    }

    public abstract class ReplayEvent : IComparable<ReplayEvent>
    {
        public static readonly string LocalPlayerID = GuidPatcher.RealNewGuid().ToString();

        public int ticksSinceLoad;
        public int? randomS0Before;
        // All four words of Unity's random state before the host played this event, hashed (see DesyncCheck).
        public int? randomStateHashBefore;
        /// <summary>
        /// Who did this: 0 for the host, a guest's connection number for a guest. Written by the host (a guest's own
        /// value is replaced when the host receives it) and sent on with the event. Only separate colonies read it.
        /// </summary>
        public int player;
        /// <summary>
        /// The colony slot the actor plays, written by the host just before it plays the event, so every computer's
        /// replay uses the same one. -1 when not known (outside a hosted session).
        /// </summary>
        public int slot = -1;
        /// <summary>
        /// A guest's own tag on an action it sends ("who:number"). The host keeps it when it plays the action and sends
        /// it on, so the guest recognises its own action coming back, or being refused, and times the round trip. Null on
        /// the host's own actions. Nothing in the game's simulation reads it.
        /// </summary>
        public string requestId;

        public string type => GetType().Name;

        public int CompareTo(ReplayEvent other)
        {
            if (other == null)
                return 1;
            //return timeInFixedSecs.CompareTo(other.timeInFixedSecs);
            return ticksSinceLoad.CompareTo(other.ticksSinceLoad);
        }

        public abstract void Replay(IReplayContext context);

        /// <summary>
        /// What this action touches, for separate colonies: shared, named entities, a placement, or a list that is cut
        /// down to the actor's own part. Every event type declares one (RuntimeChecks fails otherwise); null means
        /// "not declared". May be called on the host before Replay, so it must only read.
        /// </summary>
        public virtual ColonyScope GetColonyScope() => null;

        /// <summary>
        /// Whether playing this changes what a save would hold. A player who joins is sent the save the host started
        /// from and only the actions played after they connected, so once an action that changes the game has been
        /// played nobody else can join (see ReplayService). False only for events a late joiner can do without:
        /// greetings, the host's session choices (sent to every joiner anyway), heartbeats, notices, the speed.
        /// A method, not a property: properties are written into the event's JSON.
        /// </summary>
        public virtual bool ChangesGame() => true;

        public override string ToString()
        {
            return type;
        }

        public virtual string ToActionString()
        {
            return $"Doing: {type}";
        }

        public static EntityComponent GetEntityComponent(IReplayContext context, string entityID)
        {
            if (!Guid.TryParse(entityID, out Guid guid))
            {
                Plugin.LogWarning($"Could not parse guid: {entityID}");
                return null;
            }
            var entity = context.GetSingleton<EntityRegistry>().GetEntity(guid);
            if (entity == null)
            {
                Plugin.LogWarning($"Could not find entity: {entityID}");
            }
            return entity;
        }

        public static T GetComponent<T>(IReplayContext context, string entityID)
        {
            var entity = GetEntityComponent(context, entityID);
            if (entity == null) return default;
            var component = entity.GetComponent<T>();
            if (component == null)
            {
                Plugin.LogWarning($"Could not find component {typeof(T)} on entity {entityID}");
            }
            return component;
        }

        public static string GetEntityID(BaseComponent component)
        {
            return component?.GetComponent<EntityComponent>()?.EntityId.ToString();
        }

        protected BuildingSpec GetBuilding(IReplayContext context, string buildingName)
        {
            BuildingSpec result = null;
            // The game throws for a name it does not know. The host refuses a guest's action naming a building the host
            // does not have (ColonyRulesService.AllowOnHost), so only a guest meets one here: the host used a building from
            // a mod this guest does not run. It is the first thing a replay asks, before anything of the action is played,
            // so this guest can leave without harm to anyone (MissingContentException; ReplayService).
            try { result = context.GetSingleton<BuildingService>()?.GetBuildingTemplate(buildingName); }
            catch (ArgumentException) { }
            if (result == null)
                throw new MissingContentException($"The host used the building {buildingName}, which this game doesn't have " +
                    "(it comes from a mod that isn't installed here).");
            return result;
        }

        public static string GetBuildingName(EntitySetup.Builder entitySetupBuilder)
        {
            var spec = entitySetupBuilder.Template.GetSpec<BuildingSpec>();
            return spec?.GetSpec<TemplateSpec>()?.TemplateName;
        }

        public static ReplayService GetReplayServiceIfReady()
        {
            // If we haven't loaded yet, we're not ready
            if (!ReplayService.IsLoaded) return null;

            var replayService = GetSingleton<ReplayService>();
            if (replayService == null || replayService.IsDesynced) return null;
            return replayService;
        }
        

        /// <summary>
        /// Helper method to make overriding recorded actions in game easier.
        /// </summary>
        /// <param name="getEvent">
        /// A function that returns the event to record, or null
        /// if we should skip recording and do the default method behavior.
        /// </param>
        /// <returns>True if the method should use default behavior</returns>
        /// <remarks>
        /// Every prefix that records through this (directly, through DoEntityPrefix or an event's own DoPrefix helper)
        /// carries [HarmonyPriority(Priority.First)]. A prefix that replaces the game's method runs last
        /// (Priority.Last), after other mods' prefixes; a recording prefix runs first, before them. Here the local
        /// player's action is recorded and skipped, and it is played later on every computer in the same tick, when
        /// this lets the method run. Another mod's prefix that ran before this one would act at the click, on this
        /// computer alone, and if it returned false Harmony would skip this prefix too: the action would never be
        /// sent. Running first also refuses what the colony rules refuse (below) before any other mod acts on it.
        /// RuntimeChecks finds the recording prefixes from their code and fails on one that does not run first.
        /// </remarks>
        public static bool DoPrefix(Func<ReplayEvent> getEvent)
        {
            // If we're already replaying events, just let the original method run.
            // This handles nested calls (e.g., Replay() calls Unlock() which triggers this prefix again)
            if (ReplayService.HasReplayFailure) return false;
            if (ReplayService.IsReplayingEvents) return true;
            // The simulation itself calling the method (RunAsSimulation), the same on every computer: not a click.
            if (simulationCalls > 0) return true;

            // If the replay service is not available, just use default behavior
            ReplayService replayService = GetReplayServiceIfReady();
            if (replayService == null) return true;

            // Get the event and if it's null, just use default behavior
            ReplayEvent message = getEvent();
            if (message == null) return true;

            // Separate colonies: an action on the other colony is refused here with a notice. The host would refuse
            // it anyway; this only explains it at once. Refused means not recorded and not done.
            if (ColonyRulesService.RefuseLocally(message)) return false;

            // A line per action, with detailed logging on. Every line goes through Unity's logger, which records a
            // stack trace each time: a dragged path is dozens of actions in one frame.
            if (Settings.Debug || Settings.VerboseLogging) Plugin.Log(message.ToActionString());

            // Record the event
            replayService.RecordEvent(message);

            // Return based on the EventIO's desired behavior
            return EventIO.ShouldPlayPatchedEvents;
        }

        // Game code that calls a method players' clicks are recorded from, from inside the simulation itself (a
        // spring-return lever switching itself off in the tick): the call runs at once on every computer, as in single
        // player, instead of each computer taking it for a click of its own and sending it to the host. Only for calls
        // every computer makes at the same point of the same tick; see Fixes/TickTimingFixes. Main thread only.
        private static int simulationCalls;

        /// <summary>Runs <paramref name="action"/> as the simulation's own call: recording prefixes let it through.</summary>
        public static void RunAsSimulation(Action action)
        {
            EnterSimulationCall();
            try { action(); }
            finally { ExitSimulationCall(); }
        }

        public static void EnterSimulationCall() => simulationCalls++;

        public static void ExitSimulationCall() => simulationCalls = Math.Max(0, simulationCalls - 1);

        public static bool DoEntityPrefix(BaseComponent component, Func<string, ReplayEvent> doRecord)
        {
            return DoPrefix(() =>
            {
                string entityID = GetEntityID(component);
                // If this is happening to a non-entity (e.g. prefab),
                // just let the base method handle it
                if (entityID == null) return null;
                var message = doRecord(entityID);
                // Advisory "Editing" notice for other players; presentation only.
                if (message != null && component.HasComponent<Building>())
                    BeaverBuddies.Activity.PlayerActivityService.NotifyLocalEdit(entityID);
                return message;
            });
        }
    }

}
