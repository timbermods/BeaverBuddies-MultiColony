using System;

namespace BeaverBuddies
{
    // Decides how fast a player's game runs while it is behind the newest tick it has received.
    //
    // This only changes how quickly a player works through ticks it already holds. Which events
    // run on which tick is decided by the host, so the speed chosen here cannot change what
    // anyone simulates.
    //
    // The original rule sped a guest up only once it was more ticks behind than the game speed:
    // more than 1 tick at speed 1, but more than 7 ticks at speed 7. Both players run at the same
    // nominal speed, so every hitch on the guest added lag that nothing recovered until it passed
    // that mark. At speed 7 a guest therefore sat 3 to 5 ticks behind, about 0.3 to 0.4 s before
    // it saw the result of its own actions, on top of the network delay.
    //
    // Now a guest also starts catching up once it is more than BufferTicksFor(speed) behind, and
    // keeps going until it is within ReleaseTicksFor(speed). The original rule still applies, so a
    // guest is never slower to catch up than it was.
    //
    // How far behind a guest may stay depends on the speed. At speed 3 or below a tick lasts 0.2 s
    // or more, and every tick a guest runs behind is that much longer before it sees its own
    // actions (0.6 s a tick at speed 1). There it catches up as soon as it is more than one tick
    // behind, and all the way. Faster, a tick is short, a tick of lag costs little, and the lag
    // flickers more with the network, so the wider buffer stays.
    public static class CatchUpSpeed
    {
        // The fastest speed that gets the short buffer.
        public const float ShortBufferMaxSpeed = 3;

        // Lag a guest settles at. At high speed not zero: a guest with nothing queued waits for the
        // host's next heartbeat before every tick, and at a tick every 0.1 s the network's jitter
        // would show as stutter. Above speed 7 (a speed boost, see SpeedBoost) the buffer grows with
        // the speed so it stays the same stretch of time, about a sixth of a second: two ticks at
        // speed 7, three at 10, nine at 30.
        public static int BufferTicksFor(float targetSpeed) =>
            targetSpeed <= ShortBufferMaxSpeed ? 1 : Math.Max(2, (int)Math.Round(targetSpeed * 2 / 7));

        // Once catching up, continue until this close. Each speed change notifies every animated
        // building, so the rule is built to change speed a few times per hitch, not every tick: the
        // release mark is below the buffer, so the lag flickering by one tick changes nothing.
        public static int ReleaseTicksFor(float targetSpeed) =>
            targetSpeed <= ShortBufferMaxSpeed ? 0 : BufferTicksFor(targetSpeed) - 1;

        // The original cap on catch-up speed, for the speeds the game's buttons give (1, 3 and 7).
        public const float MaxSpeed = 10;

        // A boosted speed can be above that cap. A guest then catches up this many steps above the
        // chosen speed, whatever it is, so it is never held below the speed it is meant to run at.
        public const float CatchUpMargin = 3;

        public static float CapFor(float targetSpeed) => Math.Max(MaxSpeed, targetSpeed + CatchUpMargin);

        // The speed a guest works through ticks at, before any catching up: the one the players chose or, while the host
        // eases off for a slow guest (HostPacing, FrameRatePacing), the host's own lower pace, which the host sends with
        // each tick (HeartbeatEvent.hostSpeed). Until 1.4.0-beta12 a guest ran at the chosen speed while the host ran
        // slower, so it reached the start of every tick before the host's word for it and stood waiting there: every
        // guest but the slow one went stop-go on every tick, more the more the host eased. A host holding still (0) is
        // not a pace: a guest behind it catches up.
        public static float PaceFor(float chosenSpeed, float? hostSpeed) =>
            hostSpeed is float host && host > 0 && host < chosenSpeed ? host : chosenSpeed;

        public static float For(float targetSpeed, int ticksBehind, float currentSpeed)
        {
            // The original rule, unchanged. It also covers a paused game (target 0), where a guest
            // that is behind still has to run to reach the tick the host paused on.
            float cap = CapFor(targetSpeed);
            float speed = targetSpeed;
            if (ticksBehind > targetSpeed)
            {
                speed = Math.Max(targetSpeed, Math.Min(ticksBehind, cap));
            }
            if (targetSpeed <= 0)
            {
                return speed;
            }

            int buffer = BufferTicksFor(targetSpeed);
            bool catchingUp = currentSpeed > targetSpeed;
            if (ticksBehind > (catchingUp ? ReleaseTicksFor(targetSpeed) : buffer))
            {
                // Whole steps above the chosen speed. The lag naturally flickers by one tick as the host's
                // tick arrives and ours finishes, so while catching up the speed only ever rises; it drops
                // back once, at the release mark. Otherwise it would flip on every tick.
                float boosted = targetSpeed + Math.Max(1, ticksBehind - buffer);
                if (catchingUp)
                {
                    boosted = Math.Max(boosted, currentSpeed);
                }
                speed = Math.Max(speed, Math.Min(boosted, cap));
            }
            return speed;
        }
    }
}
