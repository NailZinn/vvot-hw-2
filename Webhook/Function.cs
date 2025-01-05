using System.Text.Json;
using Webhook.Types;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Amazon.S3;
using Amazon.S3.Model;

namespace Webhook;

public class Handler
{
    public async Task<Response> FunctionHandler(Request request)
    {
        var body = request.body.Replace("\n", "");

        var update = JsonSerializer.Deserialize<Update>(body, Utils.SnakeCaseOptions);

        if (update is null || update.Message is null)
        {
            return new Response(200, "Некорректный запрос.");
        }

        if (update.Message.Type is not MessageType.Text || update.Message.Text is null)
        {
            await Utils.SendMessageAsync(update.Message.Chat.Id, "Ошибка.");
            return new Response(200, "Некорректный запрос");
        }

        if (update.Message.Text == "/getface")
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = "https://s3.yandexcloud.net"
            };

            var s3Client = new AmazonS3Client(Utils.AwsAccessKeyId, Utils.AwsSecretAccessKey, s3Config);

            var listObjectsRequest = new ListObjectsV2Request
            {
                BucketName = Utils.FacesBucketName,
            };

            var listObjectsResponse = await s3Client.ListObjectsV2Async(listObjectsRequest);

            var unnamedFaceObject = listObjectsResponse.S3Objects.FirstOrDefault(x => x.Key.Split('/').Length == 1);

            if (unnamedFaceObject is null)
            {
                await Utils.SendMessageAsync(update.Message.Chat.Id, "Не удалось найти фотографию без имени");
                return new Response(200, "Ok");
            }

            await Utils.SendPhotoAsync(update.Message.Chat.Id, $"?face={unnamedFaceObject.Key}");
            return new Response(200, "Ok");
        }

        if (update.Message.Text.StartsWith("/find"))
        {
            var name = update.Message.Text.Split(' ')[^1];

            var s3Config = new AmazonS3Config
            {
                ServiceURL = "https://s3.yandexcloud.net"
            };

            var s3Client = new AmazonS3Client(Utils.AwsAccessKeyId, Utils.AwsSecretAccessKey, s3Config);

            var listObjectsRequest = new ListObjectsV2Request
            {
                BucketName = Utils.FacesBucketName,
            };

            var listObjectsResponse = await s3Client.ListObjectsV2Async(listObjectsRequest);

            var faceObjectData = listObjectsResponse.S3Objects.FirstOrDefault(x => x.Key.Split('/')[0] == name);

            if (faceObjectData is null)
            {
                await Utils.SendMessageAsync(update.Message.Chat.Id, $"Фотографии с {name} не найдены");
                return new Response(200, "Ok");
            }

            var getFaceObjectMetadataRequest = new GetObjectMetadataRequest
            {
                BucketName = Utils.FacesBucketName,
                Key = faceObjectData.Key
            };

            var getFaceObjectMetadataResponse = await s3Client.GetObjectMetadataAsync(getFaceObjectMetadataRequest);

            var originalKey = getFaceObjectMetadataResponse.Metadata["object_id"];

            await Utils.SendPhotoAsync(update.Message.Chat.Id, $"/photos/{originalKey}");
            return new Response(200, "Ok");
        }

        if (update.Message.ReplyToMessage is not null && update.Message.ReplyToMessage.Type == MessageType.Photo)
        {
            var filePath = await Utils.GetFilePathAsync(update.Message.ReplyToMessage.Photo![^1].FileId);

            if (filePath is null)
            {
                await Utils.SendMessageAsync(update.Message.Chat.Id, "Ошибка.");
                return new Response(200, "Некорректный запрос");
            }

            var s3Config = new AmazonS3Config
            {
                ServiceURL = "https://s3.yandexcloud.net"
            };

            var s3Client = new AmazonS3Client(Utils.AwsAccessKeyId, Utils.AwsSecretAccessKey, s3Config);

            var listObjectsRequest = new ListObjectsV2Request
            {
                BucketName = Utils.FacesBucketName,
            };
            
            var faceObjectOldKey = filePath.Replace("photos/", "");
            var faceObjectNewKey = $"{update.Message.Text}/{faceObjectOldKey.Split('/')[^1]}";

            var copyObjectRequest = new CopyObjectRequest
            {
                SourceBucket = Utils.FacesBucketName,
                SourceKey = filePath,
                DestinationBucket = Utils.FacesBucketName,
                DestinationKey = faceObjectNewKey
            };

            await s3Client.CopyObjectAsync(copyObjectRequest);

            var deleteObjectRequest = new DeleteObjectRequest
            {
                BucketName = Utils.FacesBucketName,
                Key = faceObjectOldKey
            };

            await s3Client.DeleteObjectAsync(deleteObjectRequest);

            return new Response(200, "Ok");
        }

        await Utils.SendMessageAsync(update.Message.Chat.Id, "Ошибка.");
        return new Response(200, "Некорректный запрос");
    }
}
