using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

// What a player does with a received frame it cannot read: one whose "$type" the binder refuses (see
// FrameTypeChecks), one from a mod this game does not have, one with no type at all, or a group of actions that
// holds an empty entry or another group. The frames are read by the real ClientEventIO and ServerEventIO over a real
// TimberClient and TimberServer (standing in for the network, nothing is connected), from ReadEvents, which runs
// inside a tick with nothing to catch an exception, so it must never throw.
internal static class UnreadableFrameChecks
{
    public static void Run(Assembly mod, Action<string, Action> test)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var eventType = mod.GetType("BeaverBuddies.Events.ReplayEvent", true);
        var groupedType = mod.GetType("BeaverBuddies.GroupedEvent", true);
        var heartbeatType = mod.GetType("BeaverBuddies.HeartbeatEvent", true);
        var automationType = mod.GetType("BeaverBuddies.Events.AutomationEvent", true);
        var jsonType = mod.GetType("BeaverBuddies.IO.JsonSettings", true);
        var clientIOType = mod.GetType("BeaverBuddies.IO.ClientEventIO", true);
        var serverIOType = mod.GetType("BeaverBuddies.IO.ServerEventIO", true);
        var net = Assembly.Load("TimberNet");
        var netBase = net.GetType("TimberNet.TimberNetBase", true);
        var jObject = jsonType.BaseType.Assembly.GetType("Newtonsoft.Json.Linq.JObject", true);

        var pluginLogger = mod.GetType("BeaverBuddies.Plugin", true).GetField("logger", all);
        var loggerType = mod.GetType("BeaverBuddies.Util.Logging.ILogger", true);
        // Runs with a logger that keeps what is logged, instead of Unity's native one.
        List<string> Logged(Action run)
        {
            object previous = pluginLogger.GetValue(null);
            object logger = DispatchProxy.Create(loggerType, typeof(RecordingLoggerProxy));
            pluginLogger.SetValue(null, logger);
            try { run(); } finally { pluginLogger.SetValue(null, previous); }
            return ((RecordingLoggerProxy)logger).Lines;
        }

        string Write(object replayEvent) =>
            (string)jsonType.GetMethod("Serialize").MakeGenericMethod(eventType).Invoke(null, new[] { replayEvent });
        object Frame(string json) => jObject.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { json });
        // A guest tags each action it sends (PendingActions.Sent), so it can tell it coming back or being refused.
        object Tagged(object replayEvent, string requestId)
        {
            eventType.GetField("requestId").SetValue(replayEvent, requestId);
            return replayEvent;
        }
        object Heartbeat(string requestId = null) => Tagged(Activator.CreateInstance(heartbeatType, true), requestId);
        object Automation(string requestId, params object[] arguments)
        {
            object e = Activator.CreateInstance(automationType);
            automationType.GetField("entityID").SetValue(e, Guid.Empty.ToString());
            automationType.GetField("methodKey").SetValue(e, "Timberborn.AutomationBuildings.Lever.SwitchState");
            automationType.GetField("arguments").SetValue(e, arguments);
            return Tagged(e, requestId);
        }
        // One tick's actions, as a player sends them.
        object GroupOf(int tick, params object[] events)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(eventType));
            foreach (object e in events) list.Add(e);
            object group = Activator.CreateInstance(groupedType, list);
            eventType.GetField("ticksSinceLoad").SetValue(group, tick);
            return group;
        }
        string Group(int tick, params object[] events) => Write(GroupOf(tick, events));

        string Renamed(string json, string from, string to) =>
            json.Contains($"\"{from}\"") ? json.Replace($"\"{from}\"", $"\"{to}\"") : throw new Exception($"{from} is not in {json}");

        const int Tick = 3;
        object Readable() => Frame(Group(Tick, Heartbeat("guest:read")));
        // Unreadable frames, with pieces of text the reason for dropping each must contain, and the tags of the guest
        // actions in it, which a host tells that guest were refused.
        var unreadable = new List<(string Kind, Func<object> Make, string[] Named, string[] Tags)>
        {
            ("a type the binder refuses", () => Frame(Group(Tick, Heartbeat("guest:1"), Automation("guest:2", new FrameSentinel()))),
                new[] { nameof(FrameSentinel), "RuntimeChecks" }, new[] { "guest:1", "guest:2" }),
            ("an action from a mod this game does not have", () => Frame(Renamed(Group(Tick, Heartbeat("guest:3")),
                    heartbeatType.FullName + ", " + heartbeatType.Assembly.GetName().Name, "MissingMod.Actions.MissingEvent, MissingMod.Actions")),
                new[] { "MissingMod.Actions.MissingEvent", "MissingMod.Actions" }, new[] { "guest:3" }),
            ("a frame with no type", () => Frame($"{{\"ticksSinceLoad\": {Tick}, \"requestId\": \"guest:4\"}}"),
                Array.Empty<string>(), new[] { "guest:4" }),
            // Both are read fine, but no action in them can be played: replaying them would fail and stop the session,
            // and on a host an empty entry would throw out of the tick before anything was played.
            ("a group holding an empty entry", () => Frame(Group(Tick, Heartbeat("guest:5"), null)),
                new[] { "group of actions" }, new[] { "guest:5" }),
            ("a group inside a group", () => Frame(Group(Tick, Heartbeat("guest:6"), GroupOf(Tick, Heartbeat("guest:inner")))),
                new[] { "group of actions" }, new[] { "guest:6" }),
        };

        // The real event IO around a real TimberClient or TimberServer that has received these frames. Nothing is
        // connected: the frames are put where the receive thread would have put them.
        (object IO, object Net, List<string> Faults) Received(bool asGuest, params object[] frames)
        {
            object netObject = asGuest
                ? Activator.CreateInstance(net.GetType("TimberNet.TimberClient", true),
                    DispatchProxy.Create(net.GetType("TimberNet.ISocketStream", true), typeof(InertProxy)))
                : Activator.CreateInstance(net.GetType("TimberNet.TimberServer", true),
                    DispatchProxy.Create(net.GetType("TimberNet.ISocketListener", true), typeof(InertProxy)), null, null);
            netBase.GetProperty("Started").SetValue(netObject, true);
            var received = (IList)netBase.GetField("receivedEvents", all).GetValue(netObject);
            foreach (object frame in frames) received.Add(frame);
            var faults = new FaultRecorder();
            var onFault = netBase.GetEvent("OnSessionFault");
            onFault.AddEventHandler(netObject, Delegate.CreateDelegate(onFault.EventHandlerType, faults, nameof(FaultRecorder.Record)));
            // A host's IO is made the way the game makes it (Start then creates the network); a guest's constructor
            // connects at once, so only its fields are made.
            object io = asGuest ? RuntimeHelpers.GetUninitializedObject(clientIOType) : Activator.CreateInstance(serverIOType, true);
            io.GetType().GetProperty("NetBase").SetValue(io, netObject);
            return (io, netObject, faults.Reasons);
        }
        List<object> Read(object io)
        {
            try { return ((IEnumerable)io.GetType().GetMethod("ReadEvents").Invoke(io, new object[] { Tick })).Cast<object>().ToList(); }
            catch (TargetInvocationException e) { throw new Exception("ReadEvents threw: " + e.InnerException?.Message, e.InnerException); }
        }
        List<string> RefusedTags(object serverIO)
        {
            MethodInfo take = serverIOType.GetMethod("TakeUnreadableRequestIds")
                ?? throw new Exception("A host does not keep the tags of guest actions it could not read (ServerEventIO.TakeUnreadableRequestIds)");
            return ((IEnumerable<string>)take.Invoke(serverIO, null)).ToList();
        }

        test("A guest stops the session when the host sends a frame it cannot read, and says which type", () =>
        {
            // Control: frames that can be read are read, and nothing stops.
            Logged(() =>
            {
                var (io, _, faults) = Received(true, Readable(), Readable());
                if (Read(io).Count != 2 || faults.Count != 0) throw new Exception("Readable frames were not read as before");
            });
            foreach (var bad in unreadable)
            {
                List<object> events = null;
                List<string> faults = null;
                List<string> log = Logged(() =>
                {
                    var (io, _, recorded) = Received(true, Readable(), bad.Make(), Readable());
                    faults = recorded;
                    events = Read(io);
                });
                // The host has played everything it sent, so this game can no longer keep up with it.
                if (faults.Count != 1) throw new Exception($"With {bad.Kind}, the session fault was raised {faults.Count} times");
                // Nothing of that tick is played: the session is over and part of the tick would be worse.
                if (events.Count != 0) throw new Exception($"With {bad.Kind}, {events.Count} actions of the tick were still played");
                if (!faults[0].StartsWith("An action from the host could not be read"))
                    throw new Exception($"With {bad.Kind}, the reason does not say what happened: {faults[0]}");
                foreach (string name in bad.Named)
                    if (!faults[0].Contains(name)) throw new Exception($"With {bad.Kind}, the reason does not name {name}: {faults[0]}");
                if (!log.Any(line => line.StartsWith("LogError"))) throw new Exception($"With {bad.Kind}, no error was logged");
            }
        });

        test("A host ignores a guest's frame it cannot read, keeps the rest and logs which type", () =>
        {
            object[] frames = new[] { Readable() }.Concat(unreadable.Select(bad => bad.Make())).Append(Readable()).ToArray();
            // As the host's receive thread numbers each frame by the connection it came from.
            foreach (object frame in frames) netBase.GetMethod("StampPlayer").Invoke(null, new[] { frame, (object)2 });
            List<object> events = null;
            List<string> faults = null;
            object netObject = null;
            List<string> log = Logged(() =>
            {
                var (io, received, recorded) = Received(false, frames);
                faults = recorded;
                netObject = received;
                events = Read(io);
            });
            // A guest's action is played only once the host has read it, so no player went out of step. Ending the
            // session here would let any guest end it.
            if (faults.Count != 0) throw new Exception("The host raised a session fault: " + faults[0]);
            if ((bool)netBase.GetProperty("IsStopped").GetValue(netObject)) throw new Exception("The host's session was stopped");
            if (events.Count != 2) throw new Exception($"{events.Count} actions were read; the 2 readable ones should be");
            string warnings = string.Join("\n", log.Where(line => line.StartsWith("LogWarning")));
            foreach (string name in unreadable.SelectMany(bad => bad.Named))
                if (!warnings.Contains(name)) throw new Exception($"The log does not name {name}:\n{warnings}");
            int fromGuest = log.Count(line => line.StartsWith("LogWarning") && line.Contains("Ignored an action from player 2 "));
            if (fromGuest != unreadable.Count) throw new Exception($"{fromGuest} of {unreadable.Count} warnings say which guest sent the frame:\n{warnings}");
        });

        test("A host keeps the tags of a guest's actions it could not read, to tell that guest they were refused", () =>
        {
            object[] frames = new[] { Readable() }.Concat(unreadable.Select(bad => bad.Make())).Append(Readable()).ToArray();
            object io = null;
            Logged(() =>
            {
                io = Received(false, frames).IO;
                Read(io);
            });
            // Every action in a frame that could not be read is lost, so each is refused (ReplayService sends an
            // ActionRefusedEvent for it). Only a group's own entries are looked at, as the host stamps them
            // (TimberNetBase.StampPlayer); the readable frames' actions are played, not refused.
            string[] expected = unreadable.SelectMany(bad => bad.Tags).ToArray();
            List<string> tags = RefusedTags(io);
            if (!tags.SequenceEqual(expected))
                throw new Exception($"Refused [{string.Join(", ", tags)}]; expected [{string.Join(", ", expected)}]");
            if (RefusedTags(io).Count != 0) throw new Exception("The same actions would be refused twice");
            // A guest never refuses anything: the host has played what it sent.
            if (clientIOType.GetMethod("TakeUnreadableRequestIds") != null) throw new Exception("A guest keeps tags to refuse");
        });
    }
}

public class RecordingLoggerProxy : DispatchProxy
{
    public readonly List<string> Lines = new();
    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        Lines.Add(targetMethod.Name + ": " + args?.FirstOrDefault());
        return null;
    }
}

// An interface whose members do nothing and return defaults.
public class InertProxy : DispatchProxy
{
    protected override object Invoke(MethodInfo targetMethod, object[] args) =>
        targetMethod.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void) ? Activator.CreateInstance(targetMethod.ReturnType) : null;
}

public class FaultRecorder
{
    public readonly List<string> Reasons = new();
    public void Record(string reason) => Reasons.Add(reason);
}
