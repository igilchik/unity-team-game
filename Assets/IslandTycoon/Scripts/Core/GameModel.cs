namespace IslandTycoon
{
    /// <summary>
    /// Pure game logic. No rendering, no MonoBehaviour: the view only reads state
    /// and calls the public commands, so every rule is testable.
    /// </summary>
    public class GameModel
    {
        public readonly GameConfig Cfg;

        public GameModel(GameConfig cfg)
        {
            Cfg = cfg;
        }

        public IslandConfig IC => Cfg.island;
    }
}
