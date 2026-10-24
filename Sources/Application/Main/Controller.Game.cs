using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Item;
using TuTien.Application.Interfaces.Client;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Main
{
    public partial class Controller
    {
        private static async Task<IResult> UseItemBag(int id, ItemUse value, HttpContext context,
            IClientManager clients, CharacterDB database, Cache cache)
        {
            if (string.IsNullOrWhiteSpace(value.Uid) || value.Uid.Length > 64 || value.Revision < 0)
            {
                return Results.BadRequest(new { message = "Vật phẩm không hợp lệ." });
            }

            return await HandleCharacter(id, value.PlayToken, value.Revision, context, clients, database, cache, character =>
            {
                var handler = new ItemHandler(cache);
                return handler.UseItemBag(character, value.Uid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out string error) ? null : error;
            });
        }

        private static async Task<IResult> RemoveItemBody(int id, ItemBodyUse value, HttpContext context,
            IClientManager clients, CharacterDB database, Cache cache)
        {
            if (value.Slot < 0 || value.Slot > 5 || value.Revision < 0)
            {
                return Results.BadRequest(new { message = "Ô trang bị không hợp lệ." });
            }

            return await HandleCharacter(id, value.PlayToken, value.Revision, context, clients, database, cache, character =>
            {
                var handler = new ItemHandler(cache);
                return handler.RemoveItemBody(character, value.Slot, character.BagCapacity, out string error) ? null : error;
            });
        }

        private static async Task<IResult> ChangePotential(int id, PotentialChange value, HttpContext context,
            IClientManager clients, CharacterDB database, Cache cache)
        {
            if (value.Points == null || value.Points.Length != 5 || value.Revision < 0)
            {
                return Results.BadRequest(new { message = "Danh sách điểm tiềm năng không hợp lệ." });
            }

            return await HandleCharacter(id, value.PlayToken, value.Revision, context, clients, database, cache, character =>
            {
                var handler = (CharacterHandler)character.CharacterHandler!;
                return handler.ChangePotential(value.Points, out string error) ? null : error;
            });
        }

        private static async Task<IResult> HandleCharacter(int id, string playToken, long revision, HttpContext context,
            IClientManager clients, CharacterDB database, Cache cache, Func<GameCharacter, string?> action)
        {
            var character = clients.GetCharacter(id);
            if (character == null || character.PlayerId != GetPlayerId(context))
            {
                return Results.Conflict(new { message = "Nhân vật chưa sẵn sàng. Hãy kết nối lại game." });
            }

            string error = "Không thể xử lý thao tác lúc này.";
            var handler = (CharacterHandler)character.CharacterHandler!;
            var updated = await handler.Handle(playToken, revision, database, action, reason => error = reason, context.RequestAborted);
            return updated == null ? Results.Conflict(new { message = error }) : Results.Ok(Service.CharacterInfo(updated, cache));
        }
    }
}
