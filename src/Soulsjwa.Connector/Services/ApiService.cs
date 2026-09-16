using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services;

public sealed class ApiService : IDisposable
{
    private HttpClient? _httpClient;

    public void Configure(string serverUrl, string apiKey)
    {
        _httpClient?.Dispose();

        var baseAddress = CreateValidatedBaseAddress(serverUrl);

        _httpClient = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
    }

    /// <summary>
    /// Anonymous endpoint. The connector refuses to proceed when its own
    /// <c>ConnectorConstants.Version</c> is older than the value returned here.
    /// </summary>
    public Task<ApiResult<ConnectorVersionResponse>> GetRequiredVersionAsync() =>
        GetAsync<ConnectorVersionResponse>("/api/v1/connector/version");

    /// <summary>
    /// Anonymous endpoint. Used to filter an event's games down to the ones
    /// this connector can monitor.
    /// </summary>
    public Task<ApiResult<List<ConnectorSupportedGameResponse>>> GetSupportedGamesAsync() =>
        GetAsync<List<ConnectorSupportedGameResponse>>("/api/v1/connector/supported-games");

    public Task<ApiResult<ConnectorGameDataResponse>> GetGameDataPointsAsync(int gameId) =>
        GetAsync<ConnectorGameDataResponse>($"/api/v1/connector/games/{gameId}/data");

    /// <summary>
    /// Authenticated. The events the API key's owner competes in, each with its
    /// games — the connector's own route, because the public events list is a
    /// card shape with no games on it. Also the first authenticated call in the
    /// connect sequence, so a bad key surfaces here as "Unauthorized".
    /// </summary>
    public Task<ApiResult<List<ConnectorEventResponse>>> GetEventsAsync() =>
        GetAsync<List<ConnectorEventResponse>>("/api/v1/connector/events");

    /// <summary>
    /// Submits the parsed game state for rule evaluation. The user is implied
    /// by the API key and never sent in the body, so a competitor cannot spoof
    /// someone else's submission.
    /// </summary>
    public async Task<ApiResult<ConnectorDataSubmissionResult>> SubmitGameDataAsync(
        Guid eventId, Guid eventGameId, string dataJson)
    {
        if (_httpClient is null)
            return ApiResult<ConnectorDataSubmissionResult>.Failure("Not configured. Please set server URL and API key first.");

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/connector/events/{eventId}/games/{eventGameId}/submit",
                new ConnectorSubmissionPayload(dataJson));

            var error = MapErrorStatus(response.StatusCode);
            if (error is not null) return ApiResult<ConnectorDataSubmissionResult>.Failure(error);

            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<ConnectorDataSubmissionResult>();
            return data is not null
                ? ApiResult<ConnectorDataSubmissionResult>.Success(data)
                : ApiResult<ConnectorDataSubmissionResult>.Failure("Received empty response from server.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<ConnectorDataSubmissionResult>.Failure($"Connection error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ApiResult<ConnectorDataSubmissionResult>.Failure("Request timed out. Please check the server URL.");
        }
    }

    private async Task<ApiResult<T>> GetAsync<T>(string path)
    {
        if (_httpClient is null)
            return ApiResult<T>.Failure("Not configured. Please set server URL and API key first.");

        try
        {
            var response = await _httpClient.GetAsync(path);

            var error = MapErrorStatus(response.StatusCode);
            if (error is not null) return ApiResult<T>.Failure(error);

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<T>();
            return data is not null
                ? ApiResult<T>.Success(data)
                : ApiResult<T>.Failure("Received empty response from server.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Failure($"Connection error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Failure("Request timed out. Please check the server URL.");
        }
        catch (UriFormatException)
        {
            return ApiResult<T>.Failure("Invalid server URL format.");
        }
    }

    private static Uri CreateValidatedBaseAddress(string serverUrl)
    {
        if (!Uri.TryCreate(serverUrl.TrimEnd('/'), UriKind.Absolute, out var uri))
            throw new UriFormatException("Invalid server URL format.");

        if (uri.Scheme == Uri.UriSchemeHttps)
            return uri;

        if (uri.Scheme == Uri.UriSchemeHttp && IsLoopbackHost(uri.Host))
            return uri;

        throw new UriFormatException("Server URL must use HTTPS unless it targets localhost or a loopback address.");
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

    private static string? MapErrorStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Unauthorized. Please check your API key.",
        HttpStatusCode.Forbidden => "Forbidden. Your API key does not have access to this resource.",
        _ => null,
    };

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

public sealed class ApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ApiResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static ApiResult<T> Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}
