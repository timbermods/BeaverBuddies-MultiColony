using BeaverBuddies;

/// <summary>
/// Reviewer D, S5 (1.4.0-rc1): host and guest frame by frame when the simulation, not the network, is the limit. Each
/// computer runs Unity's loop as the game does: a frame's time, capped at a third of a second (Unity's default
/// maximumDeltaTime, which the game leaves), times the time scale (SpeedManager.ScaleSpeed, with GameSpeedThrottler's
/// scale: 0.4 from 200 characters, 1 with the large colony speed limit removed), goes into the ticker's accumulator and
/// comes out as buckets (129 a tick of 0.6 s). The mod's TickingService is modelled as it is: the tick start (DoTick) is a
/// refunded bucket; a guest may not start a tick whose heartbeat has not arrived, and the frame's other buckets are lost
/// (not given back); a guest held longer than 0.1 s stands still (speed 0); a creation or deletion ends the frame's
/// ticking and gives back what is left, up to one tick. A frame lasts its render time plus the buckets it ticked. The
/// guest's speed is CatchUpSpeed.For at the host's pace (CatchUpSpeed.PaceFor); the host eases by HostPacing, sampled
/// once a second. Costs are estimates for a late game (tens of ms a tick); Kyler's Script P recordings replace them.
/// </summary>
sealed class LockstepModel
{
    const int Buckets = 129;
    const double TickSeconds = .6, PerBucket = TickSeconds / Buckets, MaxDelta = 1 / 3.0;

    sealed class Computer
    {
        public double TickCost, Render, Acc, FrameTime = 1 / 60.0, NextFrameAt, WaitingSince = -1;
        public int NextBucket, Tick;
        public float Speed;
        public long Frames, CutShort, LostToCap, LostWaiting;
        public readonly List<double> FrameTimes = new();
    }

    public sealed record Result(double HostTicksPerSecond, double GuestTicksPerSecond, double TargetTicksPerSecond,
        int LagP50, int LagP95, int LagMax, int LagMaxLastQuarter, double GuestFps, double GuestWorstFrameMs, double HostFps,
        int HostPercent, double CutShortPerTick, double LostWaitingPerSecond)
    {
        public override string ToString() =>
            $"host {HostTicksPerSecond:0.0} ticks/s ({HostPercent}% pacing), guest {GuestTicksPerSecond:0.0} ticks/s of {TargetTicksPerSecond:0.0} wanted; " +
            $"guest behind p50 {LagP50} p95 {LagP95} max {LagMax} (last quarter {LagMaxLastQuarter}) ticks; guest {GuestFps:0} fps (worst frame " +
            $"{GuestWorstFrameMs:0} ms), host {HostFps:0} fps; frames cut short {CutShortPerTick:0.00} a tick; guest buckets lost waiting {LostWaitingPerSecond:0}/s";
    }

    /// <param name="speed">The chosen speed with its boost (7 is the game's fastest button).</param>
    /// <param name="scale">GameSpeedThrottler's scale: 0.4 for 200+ characters (the default), 1 with the limit removed.</param>
    /// <param name="interruptsPerTick">Buckets a tick that create or delete (ending the frame's ticking).</param>
    public static Result Run(float speed, float scale, double hostTickMs, double guestTickMs, double renderMs = 8,
        double latencyMs = 50, double interruptsPerTick = 2, double seconds = 180, int seed = 7)
    {
        var random = new Random(seed);
        var host = new Computer { TickCost = hostTickMs / 1000, Render = renderMs / 1000, Speed = speed };
        var guest = new Computer { TickCost = guestTickMs / 1000, Render = renderMs / 1000, Speed = speed, NextFrameAt = .004 };
        var pacing = new HostPacing();
        // Heartbeats in flight: the tick and when it reaches the guest; and the newest the guest holds.
        var inFlight = new Queue<(double At, int Tick, float? Pace)>();
        int heartbeat = 0;
        float? hostPace = null;
        double nextSample = 1, warm = seconds / 3;
        var lag = new List<(double At, int Behind)>();
        int hostTicksAtWarm = 0, guestTicksAtWarm = 0;
        long guestFramesAtWarm = 0, hostFramesAtWarm = 0, cutAtWarm = 0, lostWaitingAtWarm = 0;
        float TimeScale(float s) => s <= 1 ? s : 1 + (s - 1) * scale;
        double interruptChance = interruptsPerTick / Buckets;

        while (true)
        {
            bool hostFirst = host.NextFrameAt <= guest.NextFrameAt;
            Computer c = hostFirst ? host : guest;
            double now = c.NextFrameAt;
            if (now > seconds) break;
            if (now >= warm && hostTicksAtWarm == 0)
            {
                hostTicksAtWarm = host.Tick; guestTicksAtWarm = guest.Tick;
                guestFramesAtWarm = guest.Frames; hostFramesAtWarm = host.Frames; cutAtWarm = host.CutShort + guest.CutShort;
                lostWaitingAtWarm = guest.LostWaiting;
            }
            while (inFlight.Count > 0 && inFlight.Peek().At <= now) { var h = inFlight.Dequeue(); heartbeat = h.Tick; hostPace = h.Pace; }

            // Speed, as ReplayService.UpdateSpeed sets it for the frame.
            if (hostFirst)
            {
                if (now >= nextSample)
                {
                    pacing.Sample(host.Tick - guest.Tick, true, speed);
                    nextSample += 1;
                }
                host.Speed = pacing.Apply(speed);
            }
            else
            {
                // ReplayService.UpdateSpeed: out of events (the next tick's heartbeat is not here) for longer than 0.1 s.
                bool outOfEvents = heartbeat < guest.Tick + 1;
                bool heldLong = outOfEvents && guest.WaitingSince >= 0 && now - guest.WaitingSince > .1;
                guest.Speed = heldLong ? 0 : CatchUpSpeed.For(CatchUpSpeed.PaceFor(speed, hostPace), heartbeat - guest.Tick, guest.Speed);
            }

            double dt = Math.Min(c.FrameTime, MaxDelta);
            c.Acc += dt * TimeScale(c.Speed);
            int buckets = (int)Math.Floor(c.Acc / PerBucket);
            c.Acc -= buckets * PerBucket;
            double cpu = 0;
            bool interrupted = false;
            while (buckets > 0)
            {
                if (c.NextBucket == 0)
                {
                    if (!hostFirst && heartbeat < c.Tick + 1)
                    {
                        // The tick gate: the frame's other buckets are lost.
                        if (c.WaitingSince < 0) c.WaitingSince = now;
                        c.LostWaiting += buckets;
                        buckets = 0;
                        break;
                    }
                    c.WaitingSince = -1;
                    c.Tick++;
                    if (hostFirst)
                    {
                        float eased = pacing.Apply(speed);
                        inFlight.Enqueue((now + cpu + latencyMs / 1000, c.Tick, eased > 0 && eased < speed ? eased : null));
                    }
                }
                cpu += c.TickCost / Buckets * (0.9 + 0.2 * random.NextDouble());
                c.NextBucket = (c.NextBucket + 1) % Buckets;
                buckets--;
                if (random.NextDouble() < interruptChance) { interrupted = true; break; }
            }
            if (interrupted && buckets > 0)
            {
                c.CutShort++;
                double wanted = c.Acc + buckets * PerBucket;
                c.Acc = Math.Min(TickSeconds, wanted);
                if (wanted > TickSeconds) c.LostToCap += (long)Math.Round((wanted - TickSeconds) / PerBucket);
            }
            c.FrameTime = c.Render + cpu;
            c.Frames++;
            if (now >= warm) c.FrameTimes.Add(c.FrameTime);
            c.NextFrameAt = now + c.FrameTime;
            if (!hostFirst && now >= warm) lag.Add((now, host.Tick - guest.Tick));
        }

        double span = seconds - warm;
        var sorted = lag.Select(l => l.Behind).OrderBy(b => b).ToList();
        int P(double q) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];
        double lastQuarter = seconds - span / 4;
        return new Result(
            (host.Tick - hostTicksAtWarm) / span, (guest.Tick - guestTicksAtWarm) / span, TimeScale(speed) / TickSeconds,
            P(.5), P(.95), sorted.Count == 0 ? 0 : sorted[^1], lag.Where(l => l.At >= lastQuarter).Select(l => l.Behind).DefaultIfEmpty(0).Max(),
            (guest.Frames - guestFramesAtWarm) / span, guest.FrameTimes.DefaultIfEmpty(0).Max() * 1000, (host.Frames - hostFramesAtWarm) / span,
            pacing.Percent, (host.CutShort + guest.CutShort - cutAtWarm) / Math.Max(1.0, (host.Tick - hostTicksAtWarm) + (guest.Tick - guestTicksAtWarm)),
            (guest.LostWaiting - lostWaitingAtWarm) / span);
    }
}
