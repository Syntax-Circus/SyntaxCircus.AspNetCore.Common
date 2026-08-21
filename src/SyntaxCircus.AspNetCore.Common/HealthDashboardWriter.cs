using System.Net;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Renders a self-contained HTML status dashboard from a <see cref="HealthReport"/> plus
/// <see cref="HealthDashboardOptions"/>. All styles are inline; no external assets are referenced so the
/// page works in airgapped / Docker environments. Always returns HTTP 200 with <c>Cache-Control: no-store</c>
/// regardless of the report's status — a dashboard should render even when everything's unhealthy.
/// </summary>
public static class HealthDashboardWriter
{
    public static Task WriteHtmlAsync(HttpContext context, HealthReport report, HealthDashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);

        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.ContentType = "text/html; charset=utf-8";

        var now = DateTimeOffset.UtcNow;
        var sb = new StringBuilder(8192);

        AppendHead(sb, options.Title);
        AppendHeader(sb, options.Title, options.Subtitle, report.Status, options.StatusGroups);
        sb.AppendLine("<div class=\"container\">");
        AppendNotices(sb, options.Notices);
        AppendChecksTable(sb, report);
        AppendSections(sb, options.Sections);
        AppendStatusGroups(sb, options.StatusGroups);
        AppendApiLinks(sb, options.ApiLinks);
        AppendFooter(sb, now);
        sb.Append("</div></body></html>");

        return context.Response.WriteAsync(sb.ToString(), context.RequestAborted);
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private static void AppendHead(StringBuilder sb, string title)
    {
        // Non-interpolated raw string literal — CSS braces are purely literal here; the title is
        // substituted separately to avoid mixing interpolation escaping with literal CSS braces.
        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>
            """);
        sb.Append(Encode(title));
        sb.Append("""
            </title>
            <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #212529; background: #f0f2f5; }
            .container { max-width: 900px; margin: 0 auto; padding: 2rem 1rem; }
            header { background: #1e2a38; color: #fff; padding: 1.25rem 0; margin-bottom: 2rem; }
            .header-inner { max-width: 900px; margin: 0 auto; padding: 0 1rem; display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.5rem; }
            .header-inner h1 { font-size: 1.25rem; font-weight: 600; letter-spacing: -0.01em; }
            .header-inner .subtitle { font-size: 0.8125rem; color: #8fa3ba; margin-top: 0.125rem; }
            .badge { display: inline-block; padding: 0.3rem 0.75rem; border-radius: 4px; font-size: 0.8125rem; font-weight: 600; color: #fff; white-space: nowrap; }
            .badge-sm { padding: 0.15rem 0.5rem; font-size: 0.6875rem; border-radius: 3px; }
            .badge-healthy  { background: #28a745; }
            .badge-degraded { background: #fd7e14; }
            .badge-unhealthy { background: #dc3545; }
            .header-badges { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
            .header-group { display: flex; align-items: center; gap: 0.35rem; background: rgba(255,255,255,0.08); border-radius: 5px; padding: 0.25rem 0.6rem; }
            .header-group-label { color: #8fa3ba; font-size: 0.75rem; white-space: nowrap; }
            section { background: #fff; border: 1px solid #dee2e6; border-radius: 6px; margin-bottom: 1.5rem; overflow: hidden; }
            section h2 { font-size: 0.875rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: #495057; padding: 0.75rem 1rem; border-bottom: 1px solid #dee2e6; background: #f8f9fa; margin: 0; }
            table { width: 100%; border-collapse: collapse; }
            th { text-align: left; padding: 0.5rem 1rem; font-size: 0.75rem; font-weight: 600; color: #6c757d; text-transform: uppercase; letter-spacing: 0.05em; border-bottom: 1px solid #dee2e6; white-space: nowrap; }
            td { padding: 0.75rem 1rem; border-bottom: 1px solid #f1f3f5; vertical-align: middle; }
            tr:last-child td { border-bottom: none; }
            .check-name { font-weight: 500; }
            .check-meta { color: #6c757d; font-size: 0.8125rem; margin-top: 0.2rem; }
            .check-exc { color: #c0392b; font-size: 0.8125rem; margin-top: 0.2rem; font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace; }
            .duration { text-align: right; white-space: nowrap; color: #adb5bd; font-size: 0.8125rem; }
            dl { display: grid; grid-template-columns: max-content 1fr; gap: 0.5rem 1.5rem; padding: 1rem; align-items: baseline; }
            dt { font-weight: 500; color: #495057; white-space: nowrap; }
            dd { color: #212529; }
            .api-link-box { background: #fff; border: 1px solid #dee2e6; border-radius: 6px; padding: 0.875rem 1rem; margin-bottom: 1.5rem; font-size: 0.875rem; color: #495057; }
            .api-link-box a { color: #0d6efd; text-decoration: none; font-weight: 500; }
            .api-link-box a:hover { text-decoration: underline; }
            .notice-box { background: #fff8e1; border: 1px solid #f1d188; border-radius: 6px; padding: 0.875rem 1rem; margin-bottom: 1.5rem; }
            .notice-title { font-size: 0.8125rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; color: #8a6d1d; margin-bottom: 0.35rem; }
            .notice-text { color: #6f5a1a; font-size: 0.875rem; }
            footer { text-align: center; color: #adb5bd; font-size: 0.8125rem; padding: 0.5rem 0 1rem; }
            </style>
            </head>
            <body>
            """);
    }

    private static void AppendHeader(
        StringBuilder sb,
        string title,
        string? subtitle,
        HealthStatus status,
        IReadOnlyList<HealthDashboardStatusGroup> statusGroups)
    {
        sb.Append("<header><div class=\"header-inner\">");
        sb.Append("<div><h1>").Append(Encode(title)).Append("</h1>");
        if (!string.IsNullOrEmpty(subtitle))
        {
            sb.Append("<div class=\"subtitle\">").Append(Encode(subtitle)).Append("</div>");
        }
        sb.Append("</div>");
        sb.Append("<div class=\"header-badges\">");

        foreach (var group in statusGroups)
        {
            if (group.Items.Count == 0)
            {
                continue;
            }

            sb.Append("<div class=\"header-group\">");
            sb.Append("<span class=\"header-group-label\">").Append(Encode(group.Title)).Append(":</span>");
            foreach (var item in group.Items)
            {
                sb.Append($"<span class=\"badge badge-sm {BadgeClass(item.BadgeVariant)}\">{Encode(item.Name)}: {Encode(item.Status)}</span>");
            }
            sb.Append("</div>");
        }

        sb.Append($"<span class=\"badge {BadgeClass(status)}\">{Encode(status.ToString())}</span>");
        sb.AppendLine("</div></div></header>");
    }

    private static void AppendNotices(StringBuilder sb, IReadOnlyList<HealthDashboardNotice> notices)
    {
        foreach (var notice in notices)
        {
            sb.Append("<section class=\"notice-box\">");
            sb.Append("<div class=\"notice-title\">").Append(Encode(notice.Title)).Append("</div>");
            sb.Append("<div class=\"notice-text\">").Append(Encode(notice.Text)).Append("</div>");
            sb.AppendLine("</section>");
        }
    }

    private static void AppendChecksTable(StringBuilder sb, HealthReport report)
    {
        sb.AppendLine("<section><h2>Health Checks</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>Check</th><th>Status</th><th>Details</th><th class=\"duration\">Duration</th></tr></thead>");
        sb.AppendLine("<tbody>");

        foreach (var (name, entry) in report.Entries)
        {
            sb.Append("<tr>");
            sb.Append($"<td><span class=\"check-name\">{Encode(name)}</span></td>");
            sb.Append($"<td><span class=\"badge {BadgeClass(entry.Status)}\">{Encode(entry.Status.ToString())}</span></td>");
            sb.Append("<td>");
            if (!string.IsNullOrEmpty(entry.Description))
                sb.Append($"<div class=\"check-meta\">{Encode(entry.Description)}</div>");
            if (entry.Exception is not null)
                sb.Append($"<div class=\"check-exc\">{Encode(entry.Exception.Message)}</div>");
            sb.Append("</td>");
            sb.Append($"<td class=\"duration\">{entry.Duration.TotalMilliseconds:F0}&nbsp;ms</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></section>");
    }

    private static void AppendSections(StringBuilder sb, IReadOnlyList<HealthDashboardSection> sections)
    {
        foreach (var section in sections)
        {
            sb.Append("<section><h2>").Append(Encode(section.Title)).Append("</h2><dl>");
            foreach (var (label, value) in section.Rows)
            {
                sb.Append($"<dt>{Encode(label)}</dt><dd>{Encode(value)}</dd>");
            }
            sb.AppendLine("</dl></section>");
        }
    }

    private static void AppendStatusGroups(StringBuilder sb, IReadOnlyList<HealthDashboardStatusGroup> groups)
    {
        foreach (var group in groups)
        {
            sb.Append("<section><h2>").Append(Encode(group.Title)).Append("</h2>");

            if (group.Items.Count == 0)
            {
                sb.AppendLine("<div style=\"padding: 1rem; color: #6c757d;\">No status has been recorded yet.</div>");
                sb.AppendLine("</section>");
                continue;
            }

            var detailKeys = group.Items
                .SelectMany(i => i.Details?.Keys ?? [])
                .Distinct()
                .ToList();

            sb.AppendLine("<table>");
            sb.Append("<thead><tr><th>Name</th><th>Status</th>");
            foreach (var key in detailKeys)
            {
                sb.Append("<th>").Append(Encode(key)).Append("</th>");
            }
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var item in group.Items)
            {
                sb.Append("<tr>");
                sb.Append($"<td><span class=\"check-name\">{Encode(item.Name)}</span></td>");
                sb.Append($"<td><span class=\"badge {BadgeClass(item.BadgeVariant)}\">{Encode(item.Status)}</span></td>");
                foreach (var key in detailKeys)
                {
                    var value = item.Details is not null && item.Details.TryGetValue(key, out var v) ? v : null;
                    sb.Append("<td>").Append(Encode(value ?? "-")).Append("</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></section>");
        }
    }

    private static void AppendApiLinks(StringBuilder sb, IReadOnlyList<HealthDashboardLink> links)
    {
        if (links.Count == 0)
        {
            return;
        }

        sb.Append("<div class=\"api-link-box\">");
        for (var i = 0; i < links.Count; i++)
        {
            if (i > 0)
            {
                sb.Append("&nbsp;&middot;&nbsp;");
            }
            sb.Append(Encode(links[i].Label)).Append(": <a href=\"").Append(Encode(links[i].Href)).Append("\">")
              .Append(Encode(links[i].Href)).Append("</a>");
        }
        sb.AppendLine("</div>");
    }

    private static void AppendFooter(StringBuilder sb, DateTimeOffset now)
    {
        sb.AppendLine($"<footer><p>Generated {Encode(now.ToString("yyyy-MM-dd HH:mm:ss"))} UTC</p></footer>");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BadgeClass(HealthStatus status) => status switch
    {
        HealthStatus.Healthy  => "badge-healthy",
        HealthStatus.Degraded => "badge-degraded",
        _                     => "badge-unhealthy"
    };

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
