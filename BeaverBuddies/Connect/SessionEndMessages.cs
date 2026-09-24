namespace BeaverBuddies.Connect
{
    /// <summary>
    /// What a player is told when the multiplayer session ends under a game that is still running. Plain text and no
    /// game types, so it can be checked outside the game.
    /// </summary>
    internal static class SessionEndMessages
    {
        /// <summary>
        /// The connection to the host dropped. <paramref name="reason"/> is the transport's own explanation, if it
        /// gave one.
        /// </summary>
        public static string ConnectionLost(string reason)
        {
            string text = "The multiplayer connection was lost.";
            if (!string.IsNullOrWhiteSpace(reason)) text += "\n\"" + reason.Trim() + "\"";
            return text + "\n\nMultiplayer has ended for this game and it is paused. " +
                "Rejoin waits here and joins the host's Co-op Game room as soon as they host this game again. " +
                "Or open the menu to save this game alone.";
        }
    }
}
