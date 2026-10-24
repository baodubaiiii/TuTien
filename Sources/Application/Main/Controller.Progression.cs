using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Skill;
using TuTien.Application.MainTasks;
using TuTien.Model.Client;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Main
{
    public partial class Controller
    {
        private async Task OnProgressionMessage(TcpMessage message, CancellationToken cancellationToken)
        {
            var character = _clients!.GetCharacter(_client.Player!.Character?.Id ?? 0);
            if (character == null || character.PlayerId != _client.Player.Id || character.PlaySessionId != _client.Id)
            {
                Reply(message, false, error: "Nhân vật chưa sẵn sàng.");
                return;
            }
            string error = "Không thể thực hiện thao tác lúc này.";
            var handler = (CharacterHandler)character.CharacterHandler!;
            if (message.Type == "progression")
            {
                var data = await handler.Handle<GameCharacter>(() => Task.FromResult<GameCharacter?>(character.Clone()), reason => error = reason, cancellationToken);
                Reply(message, data != null, Service.CharacterInfo(data, _cache!), data == null ? error : "");
                return;
            }
            if (message.Type == "skill.use")
            {
                var cast = ReadData<SkillUse>(message);
                if (cast.Slot < 0 || cast.Slot > 2 || (cast.TargetType != "player" && cast.TargetType != "monster"))
                {
                    Reply(message, false, error: "Ô kỹ năng hoặc mục tiêu không hợp lệ.");
                    return;
                }
                var skills = new SkillHandler(_cache!, _config!, _characters!, _clients);
                var result = cast.TargetType == "monster"
                    ? await skills.AttackMonster(character, cast.TargetId, cast.LifeId, cast.PlayToken, reason => error = reason, cancellationToken, cast.Slot)
                    : await skills.AttackPlayer(character, cast.TargetId, cast.PlayToken, reason => error = reason, cancellationToken, cast.Slot);
                Reply(message, result != null, result, result == null ? error : "");
                return;
            }
            string token;
            long revision;
            Func<GameCharacter, string?> action;
            if (message.Type is "task.accept" or "task.claim")
            {
                var value = ReadData<TaskSelect>(message);
                token = value.PlayToken;
                revision = value.Revision;
                var tasks = new TaskHandler(_cache!, _config!);
                action = draft => message.Type == "task.accept" ? tasks.AcceptTask(draft, value.Id) : tasks.RewardDoneTask(draft, value.Id);
            }
            else if (message.Type is "skill.learn" or "skill.assign")
            {
                var value = ReadData<SkillSelect>(message);
                token = value.PlayToken;
                revision = value.Revision;
                var skills = new SkillHandler(_cache!, _config!, _characters!, _clients);
                action = draft => message.Type == "skill.learn" ? skills.LearnSkill(draft, value.Id) : skills.AssignSkill(draft, value.Id, value.Slot);
            }
            else
            {
                var value = ReadData<RealmAdvance>(message);
                token = value.PlayToken;
                revision = value.Revision;
                action = draft => message.Type == "realm.advance"
                    ? ((CharacterHandler)draft.CharacterHandler!).AdvanceRealm()
                    : ((CharacterHandler)draft.CharacterHandler!).CancelBreakthrough();
            }
            var updated = await handler.Handle(token, revision, _characters!, action, reason => error = reason, cancellationToken, message.Type == "realm.cancel");
            Reply(message, updated != null, Service.CharacterInfo(updated, _cache!), updated == null ? error : "");
        }
    }
}
