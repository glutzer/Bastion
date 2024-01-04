public class BConfig
{
    public static BConfig Loaded { get; set; } = new BConfig();
    public int initialShackleSeconds = 60 * 10;
    public int temporalGearSeconds = 60 * 10;
    public int maxSeconds = 60 * 60;
}