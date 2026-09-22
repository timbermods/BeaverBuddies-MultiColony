using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using BeaverBuddies.Events;
using Newtonsoft.Json;
using TimberNet;
using BeaverBuddies.Steam;

namespace BeaverBuddies.IO
{
    public abstract class NetIOBase<T> : EventIO where T : TimberNetBase
    {

        public T NetBase { get; protected set; }
        public abstract bool RecordReplayedEvents { get; }
        public abstract bool ShouldSendHeartbeat { get; }
        public abstract UserEventBehavior UserEventBehavior { get; }
        public bool IsOutOfEvents => NetBase == null ? true : !NetBase.ShouldTick;
        public int TicksBehind => NetBase == null ? 0 : NetBase.TicksBehind;
        public bool IsSessionOver => NetBase == null || NetBase.IsStopped;

        public void Close()
        {
            if (NetBase == null) return;
            NetBase.Close();
        }

        public void Update()
        {
            if (NetBase == null) return;
            NetBase.Update();
        }

        private static readonly JsonSerializer serializer = JsonSerializer.Create(JsonSettings.Default);

        private static ReplayEvent ToEvent(JObject obj)
        {
            //Plugin.Log($"Recieving {obj}");
            try
            {
                // Straight from the parsed message: writing it out as text and parsing it again was a third of the
                // work of receiving a large action (a dragged area) for nothing.
                return obj.ToObject<ReplayEvent>(serializer);
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
            return null;
        }

        public List<ReplayEvent> ReadEvents(int ticksSinceLoad)
        {
            if (NetBase == null) return new List<ReplayEvent>();
            return NetBase.ReadEvents(ticksSinceLoad)
                .Select(ToEvent)
                .Where(e => e != null)
                .ToList();
        }

        public virtual void WriteEvents(params ReplayEvent[] events)
        {
            if (NetBase == null) return;
            foreach (ReplayEvent e in events)
            {
                // Written out once, as text: the network hashes and compresses that text and never parses it. It used
                // to be parsed into a JObject and written out again (twice more on a host, for the hash and the wire).
                NetBase.DoUserInitiatedEvent(JsonSettings.Serialize(e), e.type, e.ticksSinceLoad);
            }
        }

        public bool HasEventsForTick(int tick)
        {
            if (NetBase == null) return false;
            return NetBase.HasEventsForTick(tick);
        }
    }
}
