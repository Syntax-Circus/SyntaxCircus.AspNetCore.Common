namespace SyntaxCircus.AspNetCore.Common.Tests;

public sealed class IpBanTrackerTests
{
    private static IpBanTracker Tracker(int threshold = 3, int windowMinutes = 5, IReadOnlyList<string>? exemptIps = null) =>
        new(
            Options.Create(new IpBanOptions { RejectionThreshold = threshold, WindowMinutes = windowMinutes }),
            new IpAllowList(Options.Create(new IpAllowListOptions { Ips = exemptIps ?? [] })));

    [Fact]
    public void IsBanned_UnknownIp_ReturnsFalse() =>
        Tracker().IsBanned(IPAddress.Parse("1.2.3.4"), DateTimeOffset.UtcNow).ShouldBeFalse();

    [Fact]
    public void IsBanned_BeforeExpiry_ReturnsTrue_AfterExpiry_ReturnsFalse()
    {
        var tracker = Tracker(); var ip = IPAddress.Parse("1.2.3.4"); var now = DateTimeOffset.UtcNow;
        tracker.Ban(ip, now.AddHours(24));
        tracker.IsBanned(ip, now.AddHours(23)).ShouldBeTrue();
        tracker.IsBanned(ip, now.AddHours(24).AddSeconds(1)).ShouldBeFalse();
    }

    [Fact]
    public void RecordRejection_ReturnsTrueExactlyOnceWhenThresholdIsFirstCrossed()
    {
        var tracker = Tracker(threshold: 3); var ip = IPAddress.Parse("5.6.7.8"); var now = DateTimeOffset.UtcNow;
        tracker.RecordRejection(ip, now, out var c1).ShouldBeFalse(); c1.ShouldBe(1);
        tracker.RecordRejection(ip, now, out var c2).ShouldBeFalse(); c2.ShouldBe(2);
        tracker.RecordRejection(ip, now, out var c3).ShouldBeTrue(); c3.ShouldBe(3);
        tracker.RecordRejection(ip, now, out var c4).ShouldBeFalse(); c4.ShouldBe(4);
    }

    [Fact]
    public void RecordRejection_ResetsCountAfterWindowExpires()
    {
        var tracker = Tracker(threshold: 3, windowMinutes: 5); var ip = IPAddress.Parse("9.9.9.9"); var now = DateTimeOffset.UtcNow;
        tracker.RecordRejection(ip, now, out _);
        tracker.RecordRejection(ip, now, out _);
        tracker.RecordRejection(ip, now.AddMinutes(6), out var afterWindow).ShouldBeFalse();
        afterWindow.ShouldBe(1);
    }

    [Fact]
    public void RecordRejection_NullIp_IgnoredAndReturnsFalse()
    {
        Tracker(threshold: 1).RecordRejection(null, DateTimeOffset.UtcNow, out var count).ShouldBeFalse();
        count.ShouldBe(0);
    }

    [Fact]
    public void RecordRejection_ExemptIp_NeverRecordedEvenPastThreshold()
    {
        var tracker = Tracker(threshold: 1, exemptIps: ["1.2.3.4"]);
        var ip = IPAddress.Parse("1.2.3.4");
        var now = DateTimeOffset.UtcNow;

        tracker.RecordRejection(ip, now, out var count).ShouldBeFalse();
        count.ShouldBe(0);
    }

    [Fact]
    public void IsBanned_ExemptIp_AlwaysReturnsFalseEvenIfExplicitlyBanned()
    {
        var tracker = Tracker(exemptIps: ["1.2.3.4"]);
        var ip = IPAddress.Parse("1.2.3.4");
        var now = DateTimeOffset.UtcNow;

        tracker.Ban(ip, now.AddHours(24));

        tracker.IsBanned(ip, now).ShouldBeFalse();
    }

    [Fact]
    public void IsBanned_NonExemptIp_UnaffectedByAllowList()
    {
        var tracker = Tracker(exemptIps: ["1.2.3.4"]);
        var ip = IPAddress.Parse("9.9.9.9");
        var now = DateTimeOffset.UtcNow;

        tracker.Ban(ip, now.AddHours(24));

        tracker.IsBanned(ip, now).ShouldBeTrue();
    }

    [Fact]
    public void GetActiveBans_ReturnsUnexpiredBansOnly_SoonestExpiryFirst()
    {
        var tracker = Tracker(); var now = DateTimeOffset.UtcNow;
        tracker.Ban(IPAddress.Parse("1.1.1.1"), now.AddHours(2));
        tracker.Ban(IPAddress.Parse("2001:db8::1"), now.AddHours(1));
        tracker.Ban(IPAddress.Parse("3.3.3.3"), now.AddMinutes(-1));

        tracker.GetActiveBans(now).ShouldBe(
        [
            new ActiveIpBan("2001:db8::1", now.AddHours(1)),
            new ActiveIpBan("1.1.1.1", now.AddHours(2)),
        ]);
    }

    [Fact]
    public void Unban_ActiveBan_RemovesItAndReturnsTrue()
    {
        var tracker = Tracker(); var ip = IPAddress.Parse("1.2.3.4"); var now = DateTimeOffset.UtcNow;
        tracker.Ban(ip, now.AddHours(1));

        tracker.Unban(ip, now).ShouldBeTrue();

        tracker.IsBanned(ip, now).ShouldBeFalse();
        tracker.GetActiveBans(now).ShouldBeEmpty();
    }

    [Fact]
    public void Unban_UnknownOrExpiredBan_ReturnsFalse()
    {
        var tracker = Tracker(); var now = DateTimeOffset.UtcNow;
        var expired = IPAddress.Parse("1.2.3.4");
        tracker.Ban(expired, now.AddMinutes(-1));

        tracker.Unban(expired, now).ShouldBeFalse();
        tracker.Unban(IPAddress.Parse("5.5.5.5"), now).ShouldBeFalse();
    }

    [Fact]
    public void Unban_ClearsRejectionCount_SoTheThresholdCanTriggerAgain()
    {
        var tracker = Tracker(threshold: 2); var ip = IPAddress.Parse("1.2.3.4"); var now = DateTimeOffset.UtcNow;
        tracker.RecordRejection(ip, now, out _);
        tracker.RecordRejection(ip, now, out _).ShouldBeTrue();
        tracker.Ban(ip, now.AddHours(1));

        tracker.Unban(ip, now);

        tracker.RecordRejection(ip, now, out var count).ShouldBeFalse();
        count.ShouldBe(1);
        tracker.RecordRejection(ip, now, out _).ShouldBeTrue();
    }
}
