#nullable enable
using BeaverBuddies.Connect;

/// <summary>
/// Reviewer A: checks of the proposed fixes that need their new code (BeaverBuddies/Connect/JoinFlowRules.cs, linked into
/// StabilityTests). ReviewAChecks runs against both trees; this file only against the fixed one.
/// </summary>
static class JoinFixChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("J1: the waiting-room check leaves a guest alone once its save has come; a guest in a game gets the room's window (rc7)", () =>
        {
            // Classic joins: never welcomed.
            Check(JoinFlowRules.CheckWaitingRoom(true, false, false, false) == WaitingRoomStep.Nothing);
            Check(JoinFlowRules.CheckWaitingRoom(true, false, true, true) == WaitingRoomStep.Nothing);
            // Main menu, in the room: the page shows it.
            Check(JoinFlowRules.CheckWaitingRoom(true, true, false, false) == WaitingRoomStep.ShowRoom);
            // rc7: a guest in a game that a room welcomes sees it as a window over the game (until rc6 it left, D20).
            Check(JoinFlowRules.CheckWaitingRoom(true, true, false, true) == WaitingRoomStep.ShowWindow);
            // J1: the frame the save is handed to LoadMap, which has emptied the registry: nothing, in either scene.
            Check(JoinFlowRules.CheckWaitingRoom(true, true, true, false) == WaitingRoomStep.Nothing
                && JoinFlowRules.CheckWaitingRoom(true, true, true, true) == WaitingRoomStep.Nothing, "a waiting-room guest is dropped as its save loads");
            // A connection that is gone does nothing here (the join's error reports it).
            Check(JoinFlowRules.CheckWaitingRoom(false, true, false, true) == WaitingRoomStep.Nothing);
        });

        yield return ("J8: a room that ended is reported once: by its page, or by the join when the page never opened", () =>
        {
            Check(JoinFlowRules.ReportJoinError(false, false), "an ordinary failed join said nothing");
            Check(!JoinFlowRules.ReportJoinError(true, true), "a room's end was reported twice");
            Check(JoinFlowRules.ReportJoinError(true, false), "a room that ended before its page opened said nothing");
        });

        yield return ("A-new-1: 'Connecting to …' shows only while a join is under way and not yet in a room", () =>
        {
            Check(JoinFlowRules.ShowConnectingBox(true, false));
            Check(!JoinFlowRules.ShowConnectingBox(true, true), "the box went over the waiting room's page");
            Check(!JoinFlowRules.ShowConnectingBox(false, false), "the box came back for a join that had failed");
        });
    }
}
