namespace SyntaxCircus.AspNetCore.Common;

public sealed class IpBanOptions
{
    public const string SectionName = "IpBan";

    /// <summary>Number of rate-limit rejections from one IP within <see cref="WindowMinutes"/> that triggers a ban.</summary>
    public int RejectionThreshold { get; init; } = 20;

    /// <summary>Rolling window, in minutes, over which rejections are counted toward <see cref="RejectionThreshold"/>.</summary>
    public int WindowMinutes { get; init; } = 5;

    /// <summary>How long, in hours, a ban lasts once triggered.</summary>
    public int BanDurationHours { get; init; } = 24;
}
