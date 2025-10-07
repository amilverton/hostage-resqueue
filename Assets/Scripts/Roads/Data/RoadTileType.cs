namespace Roads
{
    public enum RoadTileType
    {
        None,
        Straight,
        Corner,
        TIntersection,
        FourWay,
        Roundabout,
        DeadEnd,        // Single connection endpoint (branch terminus)
        Start,          // Entry point (special dead end)
        Exit            // Exit point (special dead end)
    }
}
