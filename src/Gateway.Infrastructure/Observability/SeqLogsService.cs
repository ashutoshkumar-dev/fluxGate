using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gateway.Core.DTOs;
using Gateway.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gateway.Infrastructure.Observability;

/// <summary>
/// Queries the Seq HTTP API to return paginated, filterable log entries.
///
/// Seq API docs: https://docs.datalust.co/docs/posting-raw-events
/// Events endpoint: GET /api/events?filter=...&count=N&fromDateUtc=...&toDateUtc=...
/// Signal syntax for level: @Level = 'Error'
/// </summary>
public sealed class SeqLogsService : ISeqLogsService
{
    private readonly HttpClient              _http;
    private readonly ILogger<SeqLogsService> _log;
    private readonly string                  _seqBaseUrl;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public SeqLogsService(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<SeqLogsService> log)
    {
        _http       = httpFactory.CreateClient("seq");
        _log        = log;
        _seqBaseUrl = (configuration["Seq:ServerUrl"] ?? "http://localhost:5341").TrimEnd('/');
    }

    public async Task<LogPageDto> QueryLogsAsync(LogQueryDto query, CancellationToken ct = default)
    {
        // Build Seq signal / filter expression
        var filterParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // Map status param to Serilog level name
            var level = MapLevel(query.Status);
            filterParts.Add($"@Level = '{level}'");
        }

        if (!string.IsNullOrWhiteSpace(query.Route))
        {
            // Seq properties are case-sensitive; Serilog request logging uses "RequestPath"
            var escaped = query.Route.Replace("'", "\\'");
            filterParts.Add($"RequestPath like '%{escaped}%'");
        }

        var sb = new StringBuilder($"{_seqBaseUrl}/api/events?");
        sb.Append($"count={query.PageSize}");

        if (filterParts.Count > 0)
        {
            var filter = Uri.EscapeDataString(string.Join(" and ", filterParts));
            sb.Append($"&filter={filter}");
        }

        if (!string.IsNullOrWhiteSpace(query.From))
            sb.Append($"&fromDateUtc={Uri.EscapeDataString(query.From)}");

        if (!string.IsNullOrWhiteSpace(query.To))
            sb.Append($"&toDateUtc={Uri.EscapeDataString(query.To)}");

        // Seq doesn't have native pagination by page number; we use afterId / count
        // For simplicity: always pull the latest `pageSize` items (spec AC3-AC5 covers filter, not deep pagination)

        try
        {
            var resp = await _http.GetAsync(sb.ToString(), ct);
            resp.EnsureSuccessStatusCode();

            // Seq /api/events returns a raw JSON array (not wrapped in an object)
            var items_raw = await resp.Content.ReadFromJsonAsync<List<SeqEvent>>(_json, ct);

            var items = (items_raw ?? [])
                .Select(MapEvent)
                .ToList();

            return new LogPageDto
            {
                Page       = query.Page,
                PageSize   = query.PageSize,
                TotalCount = items.Count,
                Items      = items,
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to query Seq at {Url}", _seqBaseUrl);
            // Return empty rather than propagating a dependency failure
            return new LogPageDto { Page = query.Page, PageSize = query.PageSize, TotalCount = 0 };
        }
    }

    // ── Seq response model ────────────────────────────────────────────────
    // Seq /api/events returns a raw JSON array (not wrapped in an object).

    private sealed class SeqEvent
    {
        [JsonPropertyName("Timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonPropertyName("Level")]
        public string Level { get; set; } = "Information";

        [JsonPropertyName("MessageTemplateTokens")]
        public List<SeqToken>? MessageTemplateTokens { get; set; }

        // Seq does NOT include a RenderedMessage field in its API responses.
        // We build it from MessageTemplateTokens.

        [JsonPropertyName("Exception")]
        public string? Exception { get; set; }

        [JsonPropertyName("TraceId")]
        public string? TraceId { get; set; }

        [JsonPropertyName("Properties")]
        public List<SeqProperty>? Properties { get; set; }
    }

    private sealed class SeqToken
    {
        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        [JsonPropertyName("PropertyName")]
        public string? PropertyName { get; set; }

        /// <summary>Pre-formatted value rendered by Seq (e.g. "137.92" for Elapsed).</summary>
        [JsonPropertyName("FormattedValue")]
        public string? FormattedValue { get; set; }
    }

    private sealed class SeqProperty
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("Value")]
        public JsonElement Value { get; set; }
    }

    private static LogEntryDto MapEvent(SeqEvent e)
    {
        var props = (e.Properties ?? []).ToDictionary(
            p => p.Name,
            p => p.Value,
            StringComparer.OrdinalIgnoreCase);

        return new LogEntryDto
        {
            Timestamp  = e.Timestamp,
            Level      = e.Level,
            Message    = RenderMessage(e, props),
            Route      = GetString(props, "RequestPath"),
            Method     = GetString(props, "RequestMethod"),
            StatusCode = GetInt(props, "StatusCode"),
            LatencyMs  = GetDouble(props, "Elapsed"),
            TraceId    = e.TraceId ?? GetString(props, "TraceId"),
            UserId     = GetString(props, "UserId"),
            Exception  = e.Exception,
        };
    }

    /// <summary>
    /// Build a human-readable message from MessageTemplateTokens.
    /// Uses FormattedValue when available, otherwise looks up the Property value.
    /// </summary>
    private static string RenderMessage(SeqEvent e, Dictionary<string, JsonElement> props)
    {
        if (e.MessageTemplateTokens is null or { Count: 0 })
            return "";

        var sb = new StringBuilder();
        foreach (var token in e.MessageTemplateTokens)
        {
            if (token.Text is not null)
            {
                sb.Append(token.Text);
            }
            else if (token.PropertyName is not null)
            {
                if (token.FormattedValue is not null)
                    sb.Append(token.FormattedValue);
                else if (props.TryGetValue(token.PropertyName, out var val))
                    sb.Append(val.ValueKind == JsonValueKind.String ? val.GetString() : val.ToString());
                else
                    sb.Append('{').Append(token.PropertyName).Append('}');
            }
        }
        return sb.ToString();
    }

    private static string? GetString(Dictionary<string, JsonElement> props, string key)
        => props.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static int? GetInt(Dictionary<string, JsonElement> props, string key)
        => props.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : null;

    private static double? GetDouble(Dictionary<string, JsonElement> props, string key)
        => props.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble() : null;

    private static string MapLevel(string status) => status.ToLowerInvariant() switch
    {
        "error"   or "500" => "Error",
        "warning" or "warn" => "Warning",
        "debug"             => "Debug",
        _                   => "Information",
    };
}
