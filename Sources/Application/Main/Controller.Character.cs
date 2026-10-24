using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TuTien.Application.Threading;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;
using TuTien.Application.Manager;
using TuTien.Application.Interfaces.Client;
using GamePlayer = TuTien.Model.Player;

namespace TuTien.Application.Main
{
    public partial class Controller
    {
        public static void MapHttp(WebApplication app)
        {
            app.MapGet("/api/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Ok(new { token = tokens.RequestToken });
            });

            app.MapPost("/api/register", CreateUser).RequireRateLimiting("auth");
            app.MapPost("/api/login", Login).RequireRateLimiting("auth");
            app.MapPost("/api/logout", (Delegate)Logout).RequireAuthorization();
            app.MapGet("/api/account", GetAccount).RequireAuthorization();
            app.MapGet("/api/characters", GetCharacters).RequireAuthorization();
            app.MapPost("/api/characters", CreateCharacter).RequireAuthorization().RequireRateLimiting("character");
            app.MapPost("/api/characters/{id:int}/enter", EnterGame).RequireAuthorization().RequireRateLimiting("character");
            app.MapPost("/api/characters/{id:int}/talent/complete", CompleteTalent).RequireAuthorization();
            app.MapPost("/api/characters/{id:int}/bag/use", UseItemBag).RequireAuthorization().RequireRateLimiting("character");
            app.MapPost("/api/characters/{id:int}/body/remove", RemoveItemBody).RequireAuthorization().RequireRateLimiting("character");
            app.MapPost("/api/characters/{id:int}/potential", ChangePotential).RequireAuthorization().RequireRateLimiting("character");
        }

        private static async Task<IResult> CreateUser(UserLogin value, LoginGame login, HttpContext context)
        {
            if (value.Username == null || value.Password == null || !LoginGame.CheckAccount(value.Username, value.Password))
            {
                return Results.BadRequest(new { message = "Tài khoản 3–24 chữ/số/gạch dưới; mật khẩu 8–128 ký tự." });
            }

            var player = await login.CreateUser(value.Username, value.Password, context.RequestAborted);
            if (player == null)
            {
                return Results.Conflict(new { message = "Không thể tạo tài khoản. Tên tài khoản có thể đã được sử dụng." });
            }

            await SignIn(context, player);
            return Results.Json(new { player.Id, player.Username }, statusCode: 201);
        }

        private static async Task<IResult> Login(UserLogin value, LoginGame login, HttpContext context)
        {
            if (value.Username == null || value.Password == null)
            {
                return Results.BadRequest(new { message = "Hãy nhập tài khoản và mật khẩu." });
            }

            var player = await login.Login(value.Username, value.Password, context.RequestAborted);
            if (player == null)
            {
                return Results.Json(new { message = "Tài khoản hoặc mật khẩu không đúng." }, statusCode: 401);
            }

            await SignIn(context, player);
            return Results.Ok(new { player.Id, player.Username });
        }

        private static Task SignIn(HttpContext context, GamePlayer player)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
                new Claim(ClaimTypes.Name, player.Username)
            }, CookieAuthenticationDefaults.AuthenticationScheme);

            return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false });
        }

        private static async Task<IResult> Logout(HttpContext context, IClientManager clients)
        {
            var sessions = clients.GetAllSession().Where(session => session.Player?.Id == GetPlayerId(context)).ToArray();
            foreach (var session in sessions)
            {
                session.CloseMessage();
            }
            await Task.WhenAll(sessions.Select(session => session.Completion));
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }

        private static async Task<IResult> GetAccount(HttpContext context, UserDB users)
        {
            var player = await users.Load(GetPlayerId(context), context.RequestAborted);
            if (player == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new { player.Id, player.Username });
        }

        private static int GetPlayerId(HttpContext context)
        {
            return int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        private static async Task<IResult> GetCharacters(HttpContext context, CharacterDB characters, Cache cache)
        {
            var values = await characters.LoadAll(GetPlayerId(context), context.RequestAborted);
            var slots = Enumerable.Range(1, 3).Select(slot => new
            {
                slot,
                character = Service.CharacterInfo(values.Find(character => character.Slot == slot), cache)
            });

            return Results.Ok(new { slots });
        }

        private static async Task<IResult> CreateCharacter(CharacterCreate value, HttpContext context, CharacterDB characters, Cache cache)
        {
            if (!cache.IsCreationReady)
            {
                return Results.Json(new { message = "Chưa tải được dữ liệu tạo nhân vật. Vui lòng thử lại sau." }, statusCode: 503);
            }

            string? error = characters.CheckCreate(value);
            if (error != null)
            {
                return Results.BadRequest(new { message = error });
            }

            var character = await characters.Create(GetPlayerId(context), value, context.RequestAborted);
            if (character == null)
            {
                return Results.Conflict(new { message = "Ô nhân vật đã có người hoặc đạo hiệu đã được sử dụng. Hãy kiểm tra lại." });
            }

            return Results.Json(Service.CharacterInfo(character, cache), statusCode: 201);
        }

        private static async Task<IResult> EnterGame(int id, HttpContext context, CharacterDB characters, Cache cache)
        {
            if (!cache.IsCreationReady)
            {
                return Results.Json(new { message = "Dữ liệu Thiên Phú chưa sẵn sàng." }, statusCode: 503);
            }

            var character = await characters.Enter(id, GetPlayerId(context), context.RequestAborted);
            if (character == null)
            {
                return Results.NotFound(new { message = "Không tìm thấy nhân vật trong tài khoản này." });
            }

            return Results.Ok(new
            {
                needsTalentReveal = !character.TalentSeen,
                character = Service.CharacterInfo(character, cache)
            });
        }

        private static async Task<IResult> CompleteTalent(int id, HttpContext context, CharacterDB characters, MapManager maps)
        {
            bool saved = await characters.CompleteTalent(id, GetPlayerId(context), context.RequestAborted);
            if (!saved)
            {
                return Results.NotFound(new { message = "Nhân vật chưa nhận Thiên Phú hoặc không thuộc tài khoản này." });
            }

            await maps.ConfirmTalent(GetPlayerId(context), id);
            return Results.NoContent();
        }
    }
}
