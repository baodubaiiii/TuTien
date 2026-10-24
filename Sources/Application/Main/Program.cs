using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Net.WebSockets;
using Microsoft.Extensions.FileProviders;
using TuTien.Application.Interfaces.Client;
using TuTien.Application.Manager;
using TuTien.Application.Threading;
using TuTien.Database;
using TuTien.Database.Player;

namespace TuTien.Application.Main
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.Local.json", true).AddEnvironmentVariables().AddCommandLine(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });

            builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
            builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
            builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(".runtime/character-creation/keys")));
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.Cookie.Name = "TuTien.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = 403;
                    return Task.CompletedTask;
                };
            });
            builder.Services.AddAuthorization();
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-Token";
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    key => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                options.AddPolicy("character", context => RateLimitPartition.GetFixedWindowLimiter(
                    context.User.Identity?.Name ?? "unknown",
                    key => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });
            builder.Services.AddSingleton<ConfigManager>();
            builder.Services.AddSingleton<Cache>();
            builder.Services.AddHttpClient<InitData>(client => client.Timeout = TimeSpan.FromSeconds(15));
            builder.Services.AddSingleton<IClientManager, ClientManager>();
            builder.Services.AddSingleton<MapManager>();
            builder.Services.AddSingleton<ClanManager>();
            builder.Services.AddSingleton<LoginGame>();
            builder.Services.AddSingleton<DbContextAsync>();
            builder.Services.AddSingleton<UserDB>();
            builder.Services.AddSingleton<CharacterDB>();
            builder.Services.AddSingleton<ClanDB>();
            builder.Services.AddSingleton<Server>();
            builder.Services.AddHostedService(provider => provider.GetRequiredService<Server>());
            builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16384);

            await using var app = builder.Build();
            await app.Services.GetRequiredService<DbContextAsync>().Init();
            app.UseExceptionHandler(handler => handler.Run(async context =>
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { message = "Không thể hoàn tất thao tác. Vui lòng thử lại sau." });
            }));
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.Headers.CacheControl = "no-cache, no-store";
                    context.Response.Headers.Pragma = "no-cache";
                    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path != "/api/dev/shutdown")
                    {
                        try
                        {
                            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
                        }
                        catch (AntiforgeryValidationException)
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsJsonAsync(new { message = "Phiên xác nhận không hợp lệ. Hãy thử lại thao tác." });
                            return;
                        }
                    }
                }

                await next(context);
            });
            app.Lifetime.ApplicationStopping.Register(() =>
            {
                foreach (var session in app.Services.GetRequiredService<IClientManager>().GetAllSession())
                {
                    session.CloseMessage();
                }
            });
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
            string root = Path.GetFullPath(builder.Configuration["Game:Root"] ?? app.Environment.ContentRootPath);
            string content = Path.Combine(root, "content", "published", "base");
            Directory.CreateDirectory(content);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(content),
                RequestPath = "/cdn/base"
            });
            string unityContent = Path.Combine(root, "content", "published", "unity");
            Directory.CreateDirectory(unityContent);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(unityContent),
                RequestPath = "/cdn/unity",
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream"
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(root, "wireframe")),
                RequestPath = "/wireframe",
                OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
            });
            app.MapGet("/", () => Results.Redirect("/wireframe/index.html"));
            app.MapGet("/health", (Server server, IClientManager clients, Cache cache, ConfigManager config, MapManager maps) => Results.Ok(new
            {
                status = server.IsRunning ? "ready" : "stopped",
                mode = "gameplay",
                creationReady = cache.IsCreationReady,
                database = config.DatabaseProvider,
                worldReady = maps.IsWorldReady,
                templateCounts = new { items = cache.ItemTemplates.Count, maps = cache.MapTemplates.Count, npcs = cache.NpcTemplates.Count, monsters = cache.MonsterTemplates.Count, skills = cache.SkillTemplates.Count, tasks = cache.TaskTemplates.Count, realms = cache.RealmTemplates.Count },
                sessions = clients.GetAllSession().Length,
                protocolVersion = 1,
                tcpReady = server.IsTcpRunning,
                tcpPort = config.TcpPort
            }));
            app.MapGet("/api/bootstrap", (ConfigManager config) => Results.Ok(Service.Bootstrap(config)));
            Controller.MapHttp(app);
            app.Map("/ws", HandleAcceptClient);
            app.MapGet("/ws/game/{id:int}", ServerSession.HandleGameClient).RequireAuthorization();

            if (app.Environment.IsDevelopment())
            {
                app.MapPost("/api/dev/shutdown", (HttpContext context, IHostApplicationLifetime lifetime) =>
                {
                    if (context.Connection.RemoteIpAddress == null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress) ||
                        context.Request.Headers["X-TuTien-Local-Control"] != "shutdown")
                    {
                        return Results.NotFound();
                    }

                    context.Response.OnCompleted(() =>
                    {
                        lifetime.StopApplication();
                        return Task.CompletedTask;
                    });
                    return Results.Accepted();
                });
            }

            await app.StartAsync();

            var config = app.Services.GetRequiredService<ConfigManager>();
            var cache = app.Services.GetRequiredService<Cache>();
            if (!cache.IsCreationReady && string.IsNullOrWhiteSpace(config.CdnBaseUrl))
            {
                var address = new UriBuilder(app.Urls.First());
                if (address.Host == "0.0.0.0" || address.Host == "[::]")
                {
                    address.Host = "127.0.0.1";
                }

                await app.Services.GetRequiredService<InitData>().Load(address.Uri.ToString().TrimEnd('/') + "/cdn/base");
            }

            await app.Services.GetRequiredService<Server>().StartWorld();
            await app.WaitForShutdownAsync();
        }

        private static async Task HandleAcceptClient(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var config = context.RequestServices.GetRequiredService<ConfigManager>();
            var clients = context.RequestServices.GetRequiredService<IClientManager>();
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            string origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = 403;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var session = new ServerSession(socket, config, loggerFactory);
            if (!clients.Add(session))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "server_full", timeout.Token);
                }
                catch (Exception error) when (error is WebSocketException || error is OperationCanceledException)
                {
                    socket.Abort();
                }
                socket.Dispose();
                return;
            }

            try
            {
                await session.Start(context.RequestAborted);
            }
            finally
            {
                clients.RemoveSession(session);
            }
        }
    }
}
