using System.Text.Json;
using System.Text.Json.Serialization;
using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Item;
using TuTien.Application.Handlers.Skill;
using TuTien.Application.Interfaces.Client;
using TuTien.Application.Manager;
using TuTien.Application.Threading;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Client;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Main
{
    public partial class Controller
    {
        private readonly ServerSession? _tcpSession;
        private readonly MapManager? _maps;
        private readonly LoginGame? _login;
        private readonly CharacterDB? _characters;
        private readonly Cache? _cache;
        private readonly IClientManager? _clients;
        private readonly ConfigManager? _config;
        private long _authWindow = Environment.TickCount64;
        private int _authRequests;
        private long _actionWindow = Environment.TickCount64;
        private int _actionRequests;
        private static readonly JsonSerializerOptions TcpJson = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 32
        };

        public Controller(ServerSession session, ILogger<Controller> logger, MapManager maps, LoginGame login,
            CharacterDB characters, Cache cache, IClientManager clients, ConfigManager config)
            : this(session, logger)
        {
            _tcpSession = session;
            _maps = maps;
            _login = login;
            _characters = characters;
            _cache = cache;
            _clients = clients;
            _config = config;
        }

        private static T ReadData<T>(TcpMessage message) where T : class
        {
            if (message.Data.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Missing command data");
            }
            return message.Data.Deserialize<T>(TcpJson) ?? throw new JsonException("Missing command data");
        }

        private void Reply(TcpMessage message, bool success, object? data = null, string error = "")
        {
            _tcpSession!.SendMessage(message, success, data, error);
        }

        public async Task OnMessage(TcpMessage message, CancellationToken cancellationToken)
        {
            switch (message.Type)
            {
                case "bootstrap":
                    Reply(message, true, Service.Bootstrap(_config!));
                    return;
                case "ping":
                    Reply(message, true, new { serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                    return;
                case "login":
                case "register":
                    await OnLogin(message, cancellationToken);
                    return;
            }

            if (!_client.IsLogin || _client.Player == null)
            {
                Reply(message, false, error: "Hãy đăng nhập trước.");
                return;
            }

            switch (message.Type)
            {
                case "characters":
                    var values = await _characters!.LoadAll(_client.Player.Id, cancellationToken);
                    Reply(message, true, new
                    {
                        slots = Enumerable.Range(1, 3).Select(slot => new
                        {
                            slot,
                            character = Service.CharacterInfo(values.Find(value => value.Slot == slot), _cache!)
                        })
                    });
                    break;
                case "move":
                    var move = ReadData<CharacterMove>(message);
                    if (!float.IsFinite(move.X) || !float.IsFinite(move.Y))
                    {
                        throw new JsonException("Invalid move position");
                    }
                    int characterId = _client.Player.Character?.Id ?? 0;
                    if ((!string.IsNullOrEmpty(move.PlayToken) && move.PlayToken != (_client.Player.Character as GameCharacter)?.PlayToken) ||
                        !await _maps!.SetPosition(_client.Player.Id, characterId, _client.Id, move.X, move.Y, cancellationToken))
                    {
                        Reply(message, false, error: "Nhân vật chưa sẵn sàng di chuyển.");
                    }
                    break;
                case "attack":
                    await OnCharacterMessage(message, cancellationToken);
                    break;
                case "monster.attack":
                    await OnWorldMessage(message, cancellationToken);
                    break;
                case "skill.use":
                    await OnProgressionMessage(message, cancellationToken);
                    break;
                case "logout":
                    await _tcpSession!.OutMap();
                    _client.Player = null;
                    _client.IsLogin = false;
                    Reply(message, true);
                    break;
                case "character.leave":
                    await _tcpSession!.OutMap();
                    Reply(message, true);
                    break;
                default:
                    if (!AllowRequest(ref _actionWindow, ref _actionRequests, 60))
                    {
                        Reply(message, false, error: "Thao tác quá nhanh. Hãy thử lại sau.");
                        return;
                    }
                    if (message.Type is "progression" or "task.accept" or "task.claim" or "skill.learn" or "skill.assign" or "realm.advance" or "realm.cancel")
                    {
                        await OnProgressionMessage(message, cancellationToken);
                    }
                    else if (message.Type is "world.zones" or "map.change" or "zone.change" or "npc.menu" or "item.pick" or "character.revive")
                    {
                        await OnWorldMessage(message, cancellationToken);
                    }
                    else
                    {
                        await OnCharacterMessage(message, cancellationToken);
                    }
                    break;
            }
        }

        private async Task OnLogin(TcpMessage message, CancellationToken cancellationToken)
        {
            if (_client.IsLogin || !AllowRequest(ref _authWindow, ref _authRequests, 20))
            {
                Reply(message, false, error: "Hãy đăng xuất hoặc thử lại sau.");
                return;
            }

            var value = ReadData<UserLogin>(message);
            if (value.Username == null || value.Password == null || !LoginGame.CheckAccount(value.Username, value.Password))
            {
                Reply(message, false, error: "Tài khoản hoặc mật khẩu không hợp lệ.");
                return;
            }

            var player = message.Type == "register"
                ? await _login!.CreateUser(value.Username, value.Password, cancellationToken)
                : await _login!.Login(value.Username, value.Password, cancellationToken);
            if (player == null)
            {
                Reply(message, false, error: "Không thể đăng nhập hoặc tạo tài khoản.");
                return;
            }

            player.Session = _client;
            _client.Player = player;
            _client.IsLogin = true;
            Reply(message, true, new { player.Id, player.Username });
        }

        private async Task OnCharacterMessage(TcpMessage message, CancellationToken cancellationToken)
        {
            switch (message.Type)
            {
                case "character.create":
                    var create = ReadData<CharacterCreate>(message);
                    string? error = _characters!.CheckCreate(create);
                    if (error != null)
                    {
                        Reply(message, false, error: error);
                        return;
                    }
                    var created = await _characters.Create(_client.Player!.Id, create, cancellationToken);
                    Reply(message, created != null, Service.CharacterInfo(created, _cache!), created == null ? "Ô nhân vật hoặc đạo hiệu đã được sử dụng." : "");
                    break;
                case "character.enter":
                    var select = ReadData<CharacterSelect>(message);
                    if (!_maps!.IsWorldReady)
                    {
                        Reply(message, false, error: "Máy chủ chưa sẵn sàng.");
                        return;
                    }
                    var entered = await _characters!.Enter(select.Id, _client.Player!.Id, cancellationToken);
                    if (entered == null || !await _tcpSession!.JoinMap(select.Id, cancellationToken))
                    {
                        Reply(message, false, error: "Không thể vào nhân vật đã chọn.");
                        return;
                    }
                    Reply(message, true, new
                    {
                        characterId = select.Id,
                        needsTalentReveal = !entered.TalentSeen,
                        playToken = ((GameCharacter)_client.Player.Character!).PlayToken
                    });
                    break;
                case "talent.complete":
                    int id = _client.Player!.Character?.Id ?? 0;
                    bool saved = id > 0 && await _characters!.CompleteTalent(id, _client.Player.Id, cancellationToken);
                    if (saved)
                    {
                        await _maps!.ConfirmTalent(_client.Player.Id, id);
                    }
                    Reply(message, saved, error: saved ? "" : "Nhân vật chưa nhận Thiên Phú.");
                    break;
                case "bag.use":
                    await OnUseItemBag(message, cancellationToken);
                    break;
                case "body.remove":
                    await OnRemoveItemBody(message, cancellationToken);
                    break;
                case "potential":
                    await OnChangePotential(message, cancellationToken);
                    break;
                case "attack":
                    var attack = ReadData<CharacterAttack>(message);
                    var character = _clients!.GetCharacter(_client.Player!.Character?.Id ?? 0);
                    if (character == null || character.PlaySessionId != _client.Id)
                    {
                        Reply(message, false, error: "Nhân vật chưa sẵn sàng.");
                        return;
                    }
                    string reason = "Không thể tấn công lúc này.";
                    var hit = await new SkillHandler(_cache!, _config!, _characters!, _clients).AttackPlayer(
                        character, attack.TargetId, attack.PlayToken, failure => reason = failure, cancellationToken);
                    Reply(message, hit != null, hit, hit == null ? reason : "");
                    break;
                default:
                    Reply(message, false, error: "Chức năng chưa được triển khai.");
                    break;
            }
        }

        private Task OnUseItemBag(TcpMessage message, CancellationToken cancellationToken)
        {
            var value = ReadData<ItemUse>(message);
            if (string.IsNullOrWhiteSpace(value.Uid) || value.Uid.Length > 64 || value.Revision < 0)
            {
                Reply(message, false, error: "Vật phẩm không hợp lệ.");
                return Task.CompletedTask;
            }
            return HandleCharacter(message, value.PlayToken, value.Revision, character =>
            {
                var item = character.ItemBag.Find(entry => entry.Uid == value.Uid);
                if (character.InfoGame.Breakthrough.Active && (item == null || _cache!.ItemTemplates[item.Id].Type != 6))
                {
                    return "Chỉ có thể dùng đan dược trong lúc độ kiếp.";
                }
                return new ItemHandler(_cache!).UseItemBag(character, value.Uid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out string error) ? null : error;
            }, cancellationToken, true);
        }

        private Task OnRemoveItemBody(TcpMessage message, CancellationToken cancellationToken)
        {
            var value = ReadData<ItemBodyUse>(message);
            if (value.Slot < 0 || value.Slot > 5 || value.Revision < 0)
            {
                Reply(message, false, error: "Ô trang bị không hợp lệ.");
                return Task.CompletedTask;
            }
            return HandleCharacter(message, value.PlayToken, value.Revision, character =>
            {
                return new ItemHandler(_cache!).RemoveItemBody(character, value.Slot, character.BagCapacity, out string error) ? null : error;
            }, cancellationToken);
        }

        private Task OnChangePotential(TcpMessage message, CancellationToken cancellationToken)
        {
            var value = ReadData<PotentialChange>(message);
            if (value.Points == null || value.Points.Length != 5 || value.Revision < 0)
            {
                Reply(message, false, error: "Điểm tiềm năng không hợp lệ.");
                return Task.CompletedTask;
            }
            return HandleCharacter(message, value.PlayToken, value.Revision, character =>
            {
                return ((CharacterHandler)character.CharacterHandler!).ChangePotential(value.Points, out string error) ? null : error;
            }, cancellationToken);
        }

        private async Task HandleCharacter(TcpMessage message, string playToken, long revision,
            Func<GameCharacter, string?> action, CancellationToken cancellationToken, bool allowTrial = false)
        {
            int id = _client.Player!.Character?.Id ?? 0;
            var character = _clients!.GetCharacter(id);
            if (character == null || character.PlayerId != _client.Player.Id || character.PlaySessionId != _client.Id)
            {
                Reply(message, false, error: "Nhân vật chưa sẵn sàng. Hãy vào lại game.");
                return;
            }
            string error = "Không thể xử lý thao tác lúc này.";
            var updated = await ((CharacterHandler)character.CharacterHandler!).Handle(playToken, revision, _characters!, action, reason => error = reason, cancellationToken, allowTrial);
            Reply(message, updated != null, Service.CharacterInfo(updated, _cache!), updated == null ? error : "");
        }

        private static bool AllowRequest(ref long window, ref int requests, int limit)
        {
            if (Environment.TickCount64 - window >= 60000)
            {
                window = Environment.TickCount64;
                requests = 0;
            }
            return ++requests <= limit;
        }
    }
}
