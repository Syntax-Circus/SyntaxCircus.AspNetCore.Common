namespace SyntaxCircus.AspNetCore.Common;

/// <summary>A ban <see cref="IpBanTracker"/> is currently enforcing.</summary>
public sealed record ActiveIpBan(string IpAddress, DateTimeOffset BannedUntil);
