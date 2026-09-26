using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

// What a player does with a received frame it cannot read: one whose "$type" the binder refuses (see
// FrameTypeChecks), one from a mod this game does not have, one with no type at all, a group of actions that holds
// an empty entry or another group, or an action with a value of the wrong kind. The frames are read by the real ClientEventIO and ServerEventIO over a real
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
            Replaced(json, $"\"{from}\"", $"\"{to}\"");
        string Replaced(string json, string from, string to) =>
            json.Contains(from) ? json.Replace(from, to) : throw new Exception($"{from} is not in {json}");

        const int Tick = 3;
        object Readable() => Frame(Group(Tick, Heartbeat("guest:read")));
        // Unreadable frames, with pieces of text the reason for dropping each must contain, the tags of the guest actions
        // in it that a host loses and tells that guest were refused, and how many of its actions a host still keeps: a
        // host reads a group it cannot read as a whole an action at a time, and loses only the actions it cannot read.
        var unreadable = new List<(string Kind, Func<object> Make, string[] Named, string[] Tags, int Kept)>
        {
            ("a type the binder refuses", () => Frame(Group(Tick, Heartbeat("guest:1"), Automation("guest:2", new FrameSentinel()))),
                new[] { nameof(FrameSentinel), "RuntimeChecks" }, new[] { "guest:2" }, 1),
            ("an action from a mod this game does not have", () => Frame(Renamed(Group(Tick, Heartbeat("guest:3")),
                    heartbeatType.FullName + ", " + heartbeatType.Assembly.GetName().Name, "MissingMod.Actions.MissingEvent, MissingMod.Actions")),
                new[] { "MissingMod.Actions.MissingEvent", "MissingMod.Actions" }, new[] { "guest:3" }, 0),
            ("a frame with no type", () => Frame($"{{\"ticksSinceLoad\": {Tick}, \"requestId\": \"guest:4\"}}"),
                Array.Empty<string>(), new[] { "guest:4" }, 0),
            // Both are read fine, but not every action in them can be played: replaying them would fail and stop the
            // session, and on a host an empty entry would throw out of the tick before anything was played.
            ("a group holding an empty entry", () => Frame(Group(Tick, Heartbeat("guest:5"), null)),
                new[] { "group of actions" }, Array.Empty<string>(), 1),
            ("a group inside a group", () => Frame(Group(Tick, Heartbeat("guest:6"), GroupOf(Tick, Heartbeat("guest:inner")))),
                new[] { "group of actions" }, new[] { "guest:inner" }, 1),
            ("an action with a value of the wrong kind", () => Frame(Replaced(Group(Tick, Heartbeat("guest:7")), "\"digest\":null", "\"digest\":\"x\"")),
                new[] { "digest" }, new[] { "guest:7" }, 0),
            // A group whose own list cannot be read, holding an unreadable action too: once that action is left out the
            // rest still cannot be read, so the host keeps nothing of it and refuses every action in it.
            ("a group of a refused list, holding an unreadable action", () => Frame(Replaced(Group(Tick, Heartbeat("guest:11"), Automation("guest:12", new FrameSentinel())),
                    eventType.FullName + ", " + eventType.Assembly.GetName().Name + "]]", typeof(FrameSentinel).FullName + ", " + typeof(FrameSentinel).Assembly.GetName().Name + "]]")),
                new[] { nameof(FrameSentinel) }, new[] { "guest:11", "guest:12" }, 0),
            // One action a host cannot read loses only itself: the tick's other actions, before and after it, are played.
            ("one unreadable action among readable ones", () => Frame(Group(Tick, Heartbeat("guest:8"), Automation("guest:9", new FrameSentinel()), Heartbeat("guest:10"))),
                new[] { nameof(FrameSentinel) }, new[] { "guest:9" }, 2),
        };
        int keptByHost = unreadable.Sum(bad => bad.Kept);

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
        // The methods a method calls (call, callvirt), from its IL.
        List<MethodBase> Calls(MethodBase method)
        {
            var found = new List<MethodBase>();
            byte[] body = method.GetMethodBody()?.GetILAsByteArray();
            if (body == null) return found;
            var opcodes = typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)!).ToDictionary(o => (ushort)o.Value);
            int position = 0;
            while (position < body.Length)
            {
                ushort code = body[position++];
                if (code == 0xfe) code = (ushort)(0xfe00 | body[position++]);
                var op = opcodes[code];
                switch (op.OperandType)
                {
                    case System.Reflection.Emit.OperandType.InlineNone: break;
                    case System.Reflection.Emit.OperandType.ShortInlineBrTarget: case System.Reflection.Emit.OperandType.ShortInlineI:
                    case System.Reflection.Emit.OperandType.ShortInlineVar: position += 1; break;
                    case System.Reflection.Emit.OperandType.InlineVar: position += 2; break;
                    case System.Reflection.Emit.OperandType.InlineI8: case System.Reflection.Emit.OperandType.InlineR: position += 8; break;
                    case System.Reflection.Emit.OperandType.InlineSwitch: position += 4 + 4 * BitConverter.ToInt32(body, position); break;
                    case System.Reflection.Emit.OperandType.InlineMethod:
                        if (op == System.Reflection.Emit.OpCodes.Call || op == System.Reflection.Emit.OpCodes.Callvirt)
                            found.Add(method.Module.ResolveMethod(BitConverter.ToInt32(body, position))!);
                        position += 4;
                        break;
                    default: position += 4; break;
                }
            }
            return found;
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
                if (!faults[0].StartsWith("An action from the host couldn't be read"))
                    throw new Exception($"With {bad.Kind}, the reason does not say what happened: {faults[0]}");
                foreach (string name in bad.Named)
                    if (!faults[0].Contains(name)) throw new Exception($"With {bad.Kind}, the reason does not name {name}: {faults[0]}");
                if (!log.Any(line => line.StartsWith("LogError"))) throw new Exception($"With {bad.Kind}, no error was logged");
            }
            // It leaves as if it had quit (ReplayService.AbortReplay, leaveQuietly): the host and the others play on, since
            // nothing went wrong for them. A guest that read everything has not left.
            PropertyInfo left = clientIOType.GetProperty("LeftOverUnreadableAction")
                ?? throw new Exception("A guest does not say it left over an action it could not read");
            Logged(() =>
            {
                var (readIO, _, _) = Received(true, Readable());
                Read(readIO);
                if ((bool)left.GetValue(readIO)!) throw new Exception("A guest that read every frame says it left over one");
                var (badIO, _, _) = Received(true, unreadable[0].Make());
                Read(badIO);
                if (!(bool)left.GetValue(badIO)!) throw new Exception("A guest that could not read a frame does not say it left over it");
            });
            // The guest's fault handler passes that on, and a quiet leave closes the connection: only a real failure sends
            // the host the fault that stops everyone (AbortSession).
            const BindingFlags declared = all | BindingFlags.DeclaredOnly;
            var handlers = new[] { clientIOType }.Concat(clientIOType.GetNestedTypes(all))
                .SelectMany(t => t.GetMethods(declared))
                .Where(m => Calls(m).Any(c => c.Name == "AbortReplay"))
                .ToList();
            if (handlers.Count != 1 || !Calls(handlers[0]).Any(c => c.Name == "get_LeftOverUnreadableAction")
                || Calls(handlers[0]).Single(c => c.Name == "AbortReplay").GetParameters().Length != 2)
                throw new Exception("The guest's fault handler no longer says whether it left over an unreadable action");
            var abort = mod.GetType("BeaverBuddies.ReplayService", true).GetMethod("AbortReplay", all, new[] { typeof(string), typeof(bool) })
                ?? throw new Exception("ReplayService.AbortReplay(string, bool) is gone");
            var abortCalls = Calls(abort).Select(c => c.Name).ToList();
            if (!abortCalls.Contains("Close") || abortCalls.Count(n => n == "AbortSession") != 2)
                throw new Exception("AbortReplay no longer closes quietly for a guest that left: " + string.Join(", ", abortCalls));
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
            // The two readable frames, and each unreadable group with what could be kept of it.
            int groups = 2 + unreadable.Count(bad => bad.Kept > 0);
            if (events.Count != groups) throw new Exception($"{events.Count} frames were read; {groups} should be");
            int actions = events.Sum(e => e.GetType() == groupedType ? ((IList)groupedType.GetField("events").GetValue(e)!).Count : 1);
            if (actions != 2 + keptByHost) throw new Exception($"{actions} actions were kept; {2 + keptByHost} should be");
            string warnings = string.Join("\n", log.Where(line => line.StartsWith("LogWarning")));
            foreach (string name in unreadable.SelectMany(bad => bad.Named))
                if (!warnings.Contains(name)) throw new Exception($"The log does not name {name}:\n{warnings}");
            // One warning for each unreadable frame, or for each action lost from a group that was kept.
            int fromGuest = log.Count(line => line.StartsWith("LogWarning") && line.Contains("Ignored an action from player 2 "));
            if (fromGuest != unreadable.Count) throw new Exception($"{fromGuest} of {unreadable.Count} warnings say which guest sent the frame:\n{warnings}");
        });

        test("A host keeps a guest's readable actions of a tick, in their order, when one of them cannot be read", () =>
        {
            var bad = unreadable.Single(b => b.Kind == "one unreadable action among readable ones");
            object frame = bad.Make();
            netBase.GetMethod("StampPlayer").Invoke(null, new[] { frame, (object)2 });
            List<object> events = null;
            Logged(() => events = Read(Received(false, frame).IO));
            if (events.Count != 1 || events[0].GetType() != groupedType) throw new Exception("The kept actions are not one group");
            var kept = ((IList)groupedType.GetField("events").GetValue(events[0])!).Cast<object>().ToList();
            string[] tags = kept.Select(e => (string)eventType.GetField("requestId").GetValue(e)!).ToArray();
            if (!tags.SequenceEqual(new[] { "guest:8", "guest:10" })) throw new Exception($"Kept [{string.Join(", ", tags)}]");
            // Each is still the sender's, and the group is still that tick's.
            if ((int)eventType.GetField("ticksSinceLoad").GetValue(events[0])! != Tick) throw new Exception("The kept group lost its tick");
            foreach (object e in kept)
            {
                if ((int)eventType.GetField("player").GetValue(e)! != 2) throw new Exception("A kept action lost its sender");
            }
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

        test("A host sends each guest action it could not read back to that guest as refused", () =>
        {
            // The host's ReplayService as a tick reads the guests' frames (ReadEventsFromIO), with the ServerEventIO
            // above installed. Only what that step touches is set up: the IO and the queue of actions to send.
            var replayServiceType = mod.GetType("BeaverBuddies.ReplayService", true);
            var installed = mod.GetType("BeaverBuddies.IO.EventIO", true).GetField("instance", all)
                ?? throw new Exception("EventIO keeps no installed IO");
            var refusedType = mod.GetType("BeaverBuddies.Events.ActionRefusedEvent", true);
            object[] frames = new[] { Readable() }.Concat(unreadable.Select(bad => bad.Make())).Append(Readable()).ToArray();
            object previous = installed.GetValue(null);
            List<object> played = null, sent = null;
            List<string> log = Logged(() =>
            {
                object io = Received(false, frames).IO;
                installed.SetValue(null, io);
                try
                {
                    object service = RuntimeHelpers.GetUninitializedObject(replayServiceType);
                    object queue = Activator.CreateInstance(typeof(System.Collections.Concurrent.ConcurrentQueue<>).MakeGenericType(eventType));
                    replayServiceType.GetField("eventsToSend", all).SetValue(service, queue);
                    try { played = ((IEnumerable)replayServiceType.GetMethod("ReadEventsFromIO", all).Invoke(service, new object[] { Tick })).Cast<object>().ToList(); }
                    catch (TargetInvocationException e) { throw new Exception("ReadEventsFromIO threw: " + e.InnerException?.Message, e.InnerException); }
                    sent = ((IEnumerable)queue).Cast<object>().ToList();
                }
                finally { installed.SetValue(null, previous); }
            });
            if (played.Count != 2 + keptByHost) throw new Exception($"{played.Count} actions are to be played; {2 + keptByHost} should be");
            string[] expected = unreadable.SelectMany(bad => bad.Tags).ToArray();
            var refused = sent.Where(e => e.GetType() == refusedType).ToList();
            string[] tags = refused.Select(e => (string)refusedType.GetField("refusedRequestId").GetValue(e)).ToArray();
            if (refused.Count != sent.Count || !tags.SequenceEqual(expected))
                throw new Exception($"Sent {sent.Count} actions refusing [{string.Join(", ", tags)}]; expected refusals of [{string.Join(", ", expected)}]:\n{string.Join("\n", log)}");
            foreach (object e in refused)
            {
                string reason = refusedType.GetField("refusal").GetValue(e).ToString();
                if (reason != "HostRefused") throw new Exception($"An action was refused as {reason}, not HostRefused");
            }
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
