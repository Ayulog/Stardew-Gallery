namespace StardewGallery;

internal static class ReplayEffectPolicy
{
    // This is a list of effects to suppress, never an eligibility list. All other commands
    // keep their native/registered dispatch, including third-party commands and forks.
    internal static IReadOnlyList<string> SuppressedNativeHandlers { get; } = new[]
    {
        "AddQuest", "RemoveQuest", "AddSpecialOrder", "RemoveSpecialOrder",
        "AddItem", "RemoveItem", "AwardFestivalPrize", "AddWorldState",
        "Friendship", "GainSkill", "RustyKey", "BroadcastEvent",
        "MineDeath", "HospitalDeath", "Cave", "AnimalNaming", "CatQuestion",
        "GrandpaEvaluation", "GrandpaEvaluation2", "Action", "DoAction"
    };
}
