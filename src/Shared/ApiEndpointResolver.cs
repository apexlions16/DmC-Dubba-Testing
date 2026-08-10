namespace DmC.Qa.Shared;

public static class ApiEndpointResolver
{
    private const string EnvironmentVariable = "GAME_QA_API";
    private const string EndpointFileName = "qa-api-endpoint.txt";
    private const string LocalFallback = "http://localhost:7860/";

    public static Uri Resolve()
    {
        var environmentValue = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (TryCreateEndpoint(environmentValue, out var endpoint))
        {
            return endpoint;
        }

        var packagedConfig = Path.Combine(AppContext.BaseDirectory, EndpointFileName);
        if (File.Exists(packagedConfig)
            && TryCreateEndpoint(File.ReadAllText(packagedConfig).Trim(), out endpoint))
        {
            return endpoint;
        }

        var localConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameQaPlatform",
            EndpointFileName);
        if (File.Exists(localConfig)
            && TryCreateEndpoint(File.ReadAllText(localConfig).Trim(), out endpoint))
        {
            return endpoint;
        }

        return new Uri(LocalFallback, UriKind.Absolute);
    }

    private static bool TryCreateEndpoint(string? value, out Uri endpoint)
    {
        endpoint = null!;
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var builder = new UriBuilder(parsed);
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }
        endpoint = builder.Uri;
        return true;
    }
}
