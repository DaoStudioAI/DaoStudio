using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaoStudio.Utilities;

/// <summary>
/// A delegating handler that logs HTTP requests and responses
/// </summary>
public class LoggingHttpMessageHandler : DelegatingHandler
{
    private readonly ILogger _logger;
    private readonly bool _logHeaders;
    private readonly bool _logContent;
    private readonly bool _enableStreaming;

    /// <summary>
    /// Creates a new instance of the <see cref="LoggingHttpMessageHandler"/> class
    /// </summary>
    /// <param name="logger">The logger to use</param>
    /// <param name="logHeaders">Whether to log HTTP headers (may contain sensitive information)</param>
    /// <param name="logContent">Whether to log request and response content</param>
    /// <param name="enableStreaming">Whether to enable streaming responses (when true, response content won't be logged to avoid buffering)</param>
    public LoggingHttpMessageHandler(ILogger logger, bool logHeaders = false, bool logContent = true, bool enableStreaming = false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logHeaders = logHeaders;
        _logContent = logContent;
        _enableStreaming = enableStreaming;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="LoggingHttpMessageHandler"/> class with an inner handler
    /// </summary>
    /// <param name="innerHandler">The inner handler to delegate to</param>
    /// <param name="logger">The logger to use</param>
    /// <param name="logHeaders">Whether to log HTTP headers (may contain sensitive information)</param>
    /// <param name="logContent">Whether to log request and response content</param>
    /// <param name="enableStreaming">Whether to enable streaming responses (when true, response content won't be logged to avoid buffering)</param>
    public LoggingHttpMessageHandler(HttpMessageHandler innerHandler, ILogger logger, bool logHeaders = false, bool logContent = true, bool enableStreaming = false)
        : base(innerHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logHeaders = logHeaders;
        _logContent = logContent;
        _enableStreaming = enableStreaming;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var requestId = Guid.NewGuid().ToString();
        
        // Log the request
        await LogRequestAsync(request, requestId);

        // Start timing
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Send the request to the inner handler
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request failed: {RequestId}", requestId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }

        // Log the response (skip content logging if streaming is enabled to avoid buffering)
        await LogResponseAsync(response, requestId, stopwatch.ElapsedMilliseconds);

        return response;
    }

    private async Task LogRequestAsync(HttpRequestMessage request, string requestId)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"HTTP Request: {requestId}");
        builder.AppendLine($"Method: {request.Method}");
        builder.AppendLine($"Uri: {request.RequestUri}");
        
        if (_logHeaders && request.Headers != null)
        {
            builder.AppendLine("Headers:");
            foreach (var header in request.Headers)
            {
                builder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (request.Content?.Headers != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    builder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
            }
        }

        if (_logContent && request.Content != null)
        {
            string content = await request.Content.ReadAsStringAsync();
            builder.AppendLine("Content:");
            builder.AppendLine(content);
        }

        _logger.LogInformation(builder.ToString());
    }

    private async Task LogResponseAsync(HttpResponseMessage response, string requestId, long elapsedMs)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"HTTP Response: {requestId} - Took {elapsedMs}ms");
        builder.AppendLine($"StatusCode: {(int)response.StatusCode} {response.StatusCode}");
        
        if (_logHeaders && response.Headers != null)
        {
            builder.AppendLine("Headers:");
            foreach (var header in response.Headers)
            {
                builder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }

            if (response.Content?.Headers != null)
            {
                foreach (var header in response.Content.Headers)
                {
                    builder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
            }
        }

        // Skip content logging if streaming is enabled to avoid buffering the entire response
        if (_logContent && response.Content != null && !_enableStreaming)
        {
            string content = await response.Content.ReadAsStringAsync();
            builder.AppendLine("Content:");
            builder.AppendLine(content);
        }
        else if (_logContent && _enableStreaming)
        {
            builder.AppendLine("Content: [Streaming enabled - content not logged to avoid buffering]");
        }

        _logger.LogInformation(builder.ToString());
    }
}
