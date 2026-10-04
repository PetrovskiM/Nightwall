namespace Nightwall
{
    /// <summary>How the player wins a level.</summary>
    public enum WinConditionType
    {
        /// <summary>Survive every authored wave; the run ends when the last wave clears.</summary>
        SurviveAllWaves,
        /// <summary>No end condition — the run continues until the HQ is destroyed.</summary>
        Infinite,
    }
}
