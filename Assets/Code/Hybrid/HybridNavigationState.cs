namespace ARNav.Hybrid
{
    /// <summary>
    /// State của hybrid indoor/outdoor handover.
    ///
    /// Quy ước transition (Vòng 2):
    ///   Outdoor → ApproachingEntrance  : user nằm trong vùng "approach" của 1 EntranceAnchor.
    ///   ApproachingEntrance → Outdoor  : user rời khỏi vùng approach.
    ///   ApproachingEntrance → TransitionScanning : user vào trong triggerRadius của anchor (call EnterIndoor).
    ///   TransitionScanning → Indoor    : VPS pose ổn định (gate pass + hysteresis).
    ///   TransitionScanning → Relocalization : timeout không localize được.
    ///   Indoor → Relocalization        : VPS pose stale > ngưỡng.
    ///   Relocalization → Indoor        : VPS gate pass lại.
    ///   Indoor → ExitingIndoor         : user vào trong triggerRadius của exit anchor.
    ///   ExitingIndoor → Outdoor        : GPS fresh + stable (call ExitToOutdoor).
    ///   * → Uncertain                  : mất cả GPS lẫn VPS quá lâu.
    /// </summary>
    public enum HybridNavigationState
    {
        Outdoor,
        ApproachingEntrance,
        TransitionScanning,
        Indoor,
        Relocalization,
        ExitingIndoor,
        Uncertain,
    }
}
