namespace Webhook.Types;

public class Response(int statusCode, string body)
{
    public int StatusCode { get; set; } = statusCode;

    public string Body { get; set; } = body;
}