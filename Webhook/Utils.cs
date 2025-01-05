using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;

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
        KeyValuePair<string, string?>[] parameters = [new("text", text)];

        await SendAsync(chatId, parameters, "sendMessage");
    }

    public static async Task SendPhotoAsync(long chatId, string photoPathSuffix, string? caption = null)
    {
        KeyValuePair<string, string?>[] parameters = [
            new("photo", $"{ApiGatewayUrl}{photoPathSuffix}"),
            new("caption", caption)
        ];

        await SendAsync(chatId, parameters, "sendPhoto");
    }

    private static async Task SendAsync(long chatId, IEnumerable<KeyValuePair<string, string?>> parameters, string method)
    {
        using var httpClient = new HttpClient();
        using var httpRequest = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(string.Format(TgBotUrlFormat, method))
        };
        httpRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string?>(parameters)
        {
            ["chat_id"] = chatId.ToString(),
        });

        await httpClient.SendAsync(httpRequest);
    }

    public static AmazonS3Client GetS3Client()
    {
        var s3Config = new AmazonS3Config
        {
            ServiceURL = "https://s3.yandexcloud.net"
        };

        return new AmazonS3Client(AwsAccessKeyId, AwsSecretAccessKey, s3Config);
    }

    public static async Task<ListObjectsV2Response> ListFaceObjectsAsync(AmazonS3Client? s3Client = null)
    {
        s3Client ??= GetS3Client();

        var listObjectsRequest = new ListObjectsV2Request
        {
            BucketName = FacesBucketName,
        };

        return await s3Client.ListObjectsV2Async(listObjectsRequest);
    }
}