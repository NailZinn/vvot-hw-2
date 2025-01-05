using Amazon.S3.Model;
using Webhook.Types;

namespace Webhook;

public static class Helpers
{
    public static async Task<Response> HandleGetFaceAsync(long chatId)
    {
        var listObjectsResponse = await Utils.ListFaceObjectsAsync();

        var unnamedFaceObject = listObjectsResponse.S3Objects.FirstOrDefault(x => x.Key.Split('/').Length == 1);

        if (unnamedFaceObject is null)
        {
            await Utils.SendMessageAsync(chatId, "Не удалось найти фотографию без имени");
            return new Response(200, "Ok");
        }

        await Utils.SendPhotoAsync(chatId, $"?face={unnamedFaceObject.Key}", unnamedFaceObject.Key);
        return new Response(200, "Ok");
    }

    public static async Task<Response> HandleFindAsync(string message, long chatId)
    {
        var command = message.Split(' ');

        if (command.Length == 1) return await GetErrorResponseAsync(chatId);

        var name = command[^1];

        var s3Client = Utils.GetS3Client();

        var listObjectsResponse = await Utils.ListFaceObjectsAsync(s3Client);

        var faceObjectData = listObjectsResponse.S3Objects.FirstOrDefault(x => x.Key.Split('/')[0] == name);

        if (faceObjectData is null)
        {
            await Utils.SendMessageAsync(chatId, $"Фотографии с {name} не найдены");
            return new Response(200, "Ok");
        }

        var getFaceObjectMetadataRequest = new GetObjectMetadataRequest
        {
            BucketName = Utils.FacesBucketName,
            Key = faceObjectData.Key
        };

        var getFaceObjectMetadataResponse = await s3Client.GetObjectMetadataAsync(getFaceObjectMetadataRequest);

        var originalKey = getFaceObjectMetadataResponse.Metadata["object_id"];

        await Utils.SendPhotoAsync(chatId, $"/photos/{originalKey}");
        return new Response(200, "Ok");
    }

    public static async Task<Response> HandleReplyToPhotoAsync(string faceKey, string message, long chatId)
    {
        var s3Client = Utils.GetS3Client();

        var faceObjectNewKey = $"{message}/{faceKey}";

        var copyObjectRequest = new CopyObjectRequest
        {
            SourceBucket = Utils.FacesBucketName,
            SourceKey = faceKey,
            DestinationBucket = Utils.FacesBucketName,
            DestinationKey = faceObjectNewKey
        };

        try
        {
            await s3Client.CopyObjectAsync(copyObjectRequest);
        }
        catch
        {
            return await GetErrorResponseAsync(chatId);
        }

        var deleteObjectRequest = new DeleteObjectRequest
        {
            BucketName = Utils.FacesBucketName,
            Key = faceKey
        };

        await s3Client.DeleteObjectAsync(deleteObjectRequest);

        return new Response(200, "Ok");
    }

    public static async Task<Response> GetErrorResponseAsync(long chatId)
    {
        await Utils.SendMessageAsync(chatId, "Ошибка");
        return new Response(200, "Некорректный запрос");
    }
}