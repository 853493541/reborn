namespace MapUiApp.Engine
{
    /// <summary>
    /// Minimal stand-in for map-ui-app's GameData so the shared UiLayout renderer
    /// can resolve STR_* ids without pulling in the map-specific engine.
    /// </summary>
    public static class GameData
    {
        public static string ResolveString(string value) => UiProcessApp.Engine.Strings.Resolve(value);

        public static bool TryResolveString(string value, out string resolved)
            => UiProcessApp.Engine.Strings.TryResolve(value, out resolved);
    }
}
