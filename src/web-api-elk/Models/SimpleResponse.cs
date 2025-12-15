namespace web_api_elk.Models;

public class SimpleResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

