using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Configures the operator-facing HTML page rendered by
/// <see cref="HealthDashboardExtensions.MapHealthCheckDashboard"/>.
/// </summary>
public sealed class HealthDashboardOptions
{
    /// <summary>Page title, shown in the header and browser tab.</summary>
    public string Title { get; set; } = "Service Status";

    /// <summary>Optional subtitle shown under the title.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Static informational notices (e.g. data-source attribution) shown near the top of the page.</summary>
    public IReadOnlyList<HealthDashboardNotice> Notices { get; set; } = [];

    /// <summary>Free-form key/value sections (e.g. "Configuration") rendered below the health-checks table.</summary>
    public IReadOnlyList<HealthDashboardSection> Sections { get; set; } = [];

    /// <summary>
    /// Groups of named status items that don't come from an <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/>
    /// (e.g. scheduled background job state), each rendered as header mini-badges plus a details table.
    /// </summary>
    public IReadOnlyList<HealthDashboardStatusGroup> StatusGroups { get; set; } = [];

    /// <summary>Links to related endpoints (e.g. the JSON readiness/liveness endpoints) shown in the footer.</summary>
    public IReadOnlyList<HealthDashboardLink> ApiLinks { get; set; } = [];
}

/// <summary>A static informational notice box, e.g. a data-source attribution requirement.</summary>
public sealed record HealthDashboardNotice(string Title, string Text);

/// <summary>A free-form key/value section, e.g. an app's active configuration.</summary>
public sealed record HealthDashboardSection(string Title, IReadOnlyList<(string Label, string Value)> Rows);

/// <summary>A named group of <see cref="HealthDashboardStatusItem"/>s, e.g. background job status.</summary>
public sealed record HealthDashboardStatusGroup(string Title, IReadOnlyList<HealthDashboardStatusItem> Items);

/// <summary>
/// A single status item outside the <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/>
/// model — e.g. a scheduled background updater's current state.
/// </summary>
/// <param name="Name">Item name, e.g. a background job's key.</param>
/// <param name="Status">Free-form display label, e.g. "in_progress" — not constrained to <see cref="HealthStatus"/>'s 3 values.</param>
/// <param name="BadgeVariant">Which of the 3 badge colors <paramref name="Status"/> should render as.</param>
/// <param name="Details">Optional extra label/value pairs shown alongside the item, e.g. last-run timestamps.</param>
public sealed record HealthDashboardStatusItem(
    string Name,
    string Status,
    HealthStatus BadgeVariant,
    IReadOnlyDictionary<string, string?>? Details = null);

/// <summary>A labeled link to a related endpoint, e.g. the machine-readable JSON report.</summary>
public sealed record HealthDashboardLink(string Label, string Href);
