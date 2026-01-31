namespace ClosedCaptions.Config;

public class UserConfig
{
	public static readonly string Filename = "closedcaptions.json";

    public bool FilterSelf { get; set; } = false;
    public bool FilterWalk { get; set; } = false;
}