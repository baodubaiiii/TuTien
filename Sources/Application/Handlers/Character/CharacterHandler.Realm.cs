using System.Security.Cryptography;
using TuTien.Application.Handlers.Npc;
using TuTien.Database.Player;
using TuTien.Model.Info;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Handlers.Character
{
    public partial class CharacterHandler
    {
        public string? AdvanceRealm()
        {
            if (Character is not GameCharacter character || _cache == null || _config == null)
            {
                return "Nhân vật chưa sẵn sàng.";
            }
            if (!NpcHandler.CanInteract(character, 101, _config.InteractionRange))
            {
                return "Hãy đến gần Lâm Bá để chuẩn bị tiến cấp.";
            }
            var current = _cache.RealmTemplates[character.RealmId];
            if (!current.IsImplemented || !_cache.RealmTemplates.TryGetValue(current.NextRealmId, out var next) || !next.IsImplemented)
            {
                return "Cảnh giới tiếp theo chưa được mở trong bản demo.";
            }
            if (character.InfoGame.TuVi < next.TuViRequired ||
                (next.RequiredTaskId != 0 && !character.InfoTask.Completed.Contains(next.RequiredTaskId)))
            {
                return "Chưa đủ tu vi hoặc chưa hoàn thành nhiệm vụ tân thủ.";
            }
            if (character.InfoGame.PointBudget > 1000000 - next.PointReward)
            {
                return "Không thể nhận thêm điểm tiềm năng.";
            }
            character.InfoGame.TuVi -= next.TuViRequired;
            if (next.RequiresTrial)
            {
                character.InfoGame.Breakthrough = new InfoBreakthrough
                {
                    Active = true,
                    TargetRealmId = next.Id,
                    ReservedTuVi = next.TuViRequired,
                    NextStrikeTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _config.TrialStrikeInterval,
                    DamagePercent = (int[])_config.TrialDamagePercent.Clone(),
                    StrikeInterval = _config.TrialStrikeInterval,
                    LossMin = _config.TrialLossMin,
                    LossMax = _config.TrialLossMax,
                    Result = "active"
                };
            }
            else
            {
                character.RealmId = next.Id;
                character.InfoGame.PointBudget += next.PointReward;
            }
            SetUpInfo();
            return null;
        }

        public string? CancelBreakthrough()
        {
            if (Character is not GameCharacter character || !character.InfoGame.Breakthrough.Active)
            {
                return "Không có lần độ kiếp đang diễn ra.";
            }
            FailBreakthrough(character);
            return null;
        }

        private static void FailBreakthrough(GameCharacter character)
        {
            var trial = character.InfoGame.Breakthrough;
            int percent = RandomNumberGenerator.GetInt32(trial.LossMin, trial.LossMax + 1);
            long loss = (long)decimal.Ceiling((decimal)trial.ReservedTuVi * percent / 100);
            character.InfoGame.TuVi = checked(character.InfoGame.TuVi + trial.ReservedTuVi - loss);
            trial.ReservedTuVi = 0;
            trial.Active = false;
            trial.Result = "failed";
            trial.LostTuVi = loss;
        }

        public async Task<bool> UpdateRealm(long now, CharacterDB database)
        {
            if (Character is not GameCharacter character || IsClosed || character.IsChangingMap ||
                !character.InfoGame.Breakthrough.Active || now < character.InfoGame.Breakthrough.NextStrikeTime)
            {
                return false;
            }
            var draft = character.Clone();
            var handler = new CharacterHandler(draft, _cache!, _config!);
            draft.CharacterHandler = handler;
            handler.SetUpInfo();
            var trial = draft.InfoGame.Breakthrough;
            while (trial.Active && now >= trial.NextStrikeTime)
            {
                if (draft.InfoGame.Hp <= 0)
                {
                    FailBreakthrough(draft);
                    break;
                }
                int percent = trial.DamagePercent[trial.StrikeCount];
                long damage = Math.Max(1, (long)decimal.Ceiling((decimal)draft.InfoChar.HpFull * percent / 100));
                draft.InfoGame.Hp = Math.Max(0, draft.InfoGame.Hp - damage);
                trial.LastDamage = damage;
                trial.StrikeCount++;
                trial.NextStrikeTime += trial.StrikeInterval;
                if (draft.InfoGame.Hp == 0)
                {
                    draft.InfoGame.DeathTime = now;
                    FailBreakthrough(draft);
                }
                else if (trial.StrikeCount == trial.DamagePercent.Length)
                {
                    var realm = _cache!.RealmTemplates[trial.TargetRealmId];
                    draft.RealmId = realm.Id;
                    draft.InfoGame.PointBudget += realm.PointReward;
                    trial.ReservedTuVi = 0;
                    trial.Active = false;
                    trial.Result = "success";
                }
            }
            handler.SetUpInfo();
            if (!await database.SaveGame(draft))
            {
                Close();
                return false;
            }
            SetData(draft);
            return true;
        }
    }
}
