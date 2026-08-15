namespace SyntaxCircus.AspNetCore.Common;

public static class StandardExceptionHandlingExtensions
{
    /// <summary>
    /// Bundles the common non-development pipeline bootstrap: an exception handler at
    /// <paramref name="errorPath"/>, HSTS, and (optionally) a status-code re-execute page. Skips
    /// all of it in Development. Composable/opt-in by design — a pure API host behind a reverse
    /// proxy that terminates TLS and handles error pages itself should simply not call this.
    /// </summary>
    public static IApplicationBuilder UseStandardExceptionHandling(
        this IApplicationBuilder app,
        string errorPath = "/error",
        bool useStatusCodePages = false,
        string? statusCodePagesPath = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var environment = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            return app;
        }

        app.UseExceptionHandler(errorPath);
        app.UseHsts();

        if (useStatusCodePages)
        {
            app.UseStatusCodePagesWithReExecute(statusCodePagesPath ?? errorPath);
        }

        return app;
    }
}
