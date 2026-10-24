using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Npc;
using TuTien.Application.Handlers.Skill;
using TuTien.Application.MainTasks;
using TuTien.Model.Client;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Application.Main
{
    public partial class Controller
    {
        private async Task OnWorldMessage(TcpMessage message, CancellationToken cancellationToken)
        {
            var character = _clients!.GetCharacter(_client.Player!.Character?.Id ?? 0);
            if (character == null || character.PlayerId != _client.Player.Id || character.PlaySessionId != _client.Id)
            {
                Reply(message, false, error: "Nhân vật chưa sẵn sàng. Hãy vào lại game.");
                return;
            }
            string error = "Không thể xử lý thao tác lúc này.";
            object? result = null;
            switch (message.Type)
            {
                case "world.zones":
                    result = await _maps!.GetZones(character.PlayerId, character.Id, _client.Id, cancellationToken);
                    break;
                case "map.change":
                case "zone.change":
                case "character.revive":
                    var change = message.Type == "character.revive" ? null : ReadData<MapChange>(message);
                    var value = change ?? ReadData<WorldAction>(message);
                    var changed = await _tcpSession!.ChangeMap(value.PlayToken,
                        message.Type == "zone.change" ? character.MapId : change?.MapId ?? 1,
                        change?.ZoneId ?? 0, message.Type == "character.revive", reason => error = reason, cancellationToken);
                    result = Service.CharacterInfo(changed, _cache!);
                    break;
                case "npc.menu":
                    var npc = ReadData<NpcSelect>(message);
                    result = await ((CharacterHandler)character.CharacterHandler!).Handle<object>(async () =>
                    {
                        var target = ((GameZone)character.Zone!).Npcs.Find(item => item.Id == npc.Id);
                        if (target == null)
                        {
                            return null;
                        }
                        var menu = new NpcHandler(target).OpenMenu(character, npc.PlayToken, _config!.InteractionRange, out error);
                        if (menu == null)
                        {
                            return null;
                        }
                        var draft = character.Clone();
                        if (new TaskHandler(_cache!, _config).UpdateCount(draft, "talk", npc.Id))
                        {
                            if (!await _characters!.SaveGame(draft))
                            {
                                character.CharacterHandler!.Close();
                                return null;
                            }
                            ((CharacterHandler)character.CharacterHandler!).SetData(draft);
                        }
                        return menu;
                    }, reason => error = reason, cancellationToken);
                    break;
                case "monster.attack":
                    var attack = ReadData<MonsterAttack>(message);
                    result = await new SkillHandler(_cache!, _config!, _characters!, _clients).AttackMonster(character,
                        attack.TargetId, attack.LifeId, attack.PlayToken, reason => error = reason, cancellationToken);
                    break;
                case "item.pick":
                    var pick = ReadData<ItemMapPick>(message);
                    if (Guid.TryParseExact(pick.Id, "N", out _))
                    {
                        var picked = await ((CharacterHandler)character.CharacterHandler!).PickItemMap(pick.Id, pick.PlayToken,
                            _characters!, reason => error = reason, cancellationToken);
                        result = Service.CharacterInfo(picked, _cache!);
                    }
                    break;
            }
            Reply(message, result != null, result, result == null ? error : "");
        }
    }
}
