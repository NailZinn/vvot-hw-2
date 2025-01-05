using System.Text.Json;
using Webhook.Types;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
            return await Helpers.GetErrorResponseAsync(update.Message.Chat.Id);
        }

        return update.Message.Text switch
        {
            "/getface" => await Helpers.HandleGetFaceAsync(update.Message.Chat.Id),
            var message when message!.StartsWith("/find") => await Helpers.HandleFindAsync(message, update.Message.Chat.Id),
            var message when update.Message.ReplyToMessage is not null &&
                update.Message.ReplyToMessage.Type == MessageType.Photo &&
                update.Message.ReplyToMessage.Caption is not null =>
                await Helpers.HandleReplyToPhotoAsync(
                    update.Message.ReplyToMessage.Caption,
                    message,
                    update.Message.Chat.Id
                ),
            _ => await Helpers.GetErrorResponseAsync(update.Message.Chat.Id)
        };
    }
}
