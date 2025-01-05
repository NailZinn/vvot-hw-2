using System.Text.Json;

namespace Webhook;

public static class Utils
{
    public static readonly JsonSerializerOptions SnakeCaseOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static readonly string AwsAccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")!;

    public static readonly string AwsSecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")!;

    public static readonly string FacesBucketName = Environment.GetEnvironmentVariable("FACES_BUCKET_NAME")!;

    public static readonly string ApiGatewayUrl = Environment.GetEnvironmentVariable("API_GATEWAY_URL")!;

    public static readonly string TgBotToken = Environment.GetEnvironmentVariable("TG_BOT_TOKEN")!;

    private static readonly string TgBotUrlFormat = $"https://api.telegram.org/bot{TgBotToken}/{{0}}";

    public static async Task SendMessageAsync(long chatId, string text)
    {
        KeyValuePair<string, string>[] parameters = [new("text", text)];

        await SendAsync(chatId, parameters);
    }

    public static async Task SendPhotoAsync(long chatId, string photoPathSuffix)
    {
        KeyValuePair<string, string>[] parameters = [new("photo", $"{ApiGatewayUrl}{photoPathSuffix}")];

        await SendAsync(chatId, parameters);
    }

    private static async Task SendAsync(long chatId, IEnumerable<KeyValuePair<string, string>> parameters)
    {        
        using var httpClient = new HttpClient();
        using var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(string.Format(TgBotUrlFormat, "sendPhoto"))
        };
        httpRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>(parameters)
        {
            ["chat_id"] = chatId.ToString(),
        });

        await httpClient.SendAsync(httpRequest);
    }

    public static async Task<string?> GetFilePathAsync(string fileId)
    {
        using var httpClient = new HttpClient();
        using var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(string.Format(TgBotUrlFormat, "getFile"))
        };
        httpRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["file_id"] = fileId
        });

        var httpResponse = await httpClient.SendAsync(httpRequest);

        if (!httpResponse.IsSuccessStatusCode) return null;

        var dataAsString = await httpResponse.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<JsonElement>(dataAsString)
            .GetProperty("result")
            .GetProperty("file_path")
            .GetString();
    }
}