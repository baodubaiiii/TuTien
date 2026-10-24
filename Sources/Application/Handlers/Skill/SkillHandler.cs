using TuTien.Application.Interfaces.Character;
using TuTien.Application.Interfaces.Skill;
using System.Security.Cryptography;
using TuTien.Application.Handlers.Character;
using TuTien.Application.Interfaces.Client;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Info;
using GameCharacter = TuTien.Model.Character.Character;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Application.Handlers.Skill
{
    public partial class SkillHandler : ISkillHandler
    {
        private static long _nextAttackId;
        private readonly Cache? _cache;
        private readonly ConfigManager? _config;
        private readonly CharacterDB? _database;
        private readonly IClientManager? _clients;

        public SkillHandler()
        {
        }

        public SkillHandler(Cache cache, ConfigManager config, CharacterDB database, IClientManager clients)
        {
            _cache = cache;
            _config = config;
            _database = database;
            _clients = clients;
        }

        public static long GetDamage(InfoChar attacker, InfoChar target, out bool isCrit, out bool isDodge, Func<int, int>? random = null)
        {
            random ??= maximum => RandomNumberGenerator.GetInt32(maximum);
            isCrit = false;
            isDodge = random(10000) < Math.Clamp(target.DodgeFull, 0, 100) * 100;
            if (isDodge)
            {
                return 0;
            }
            decimal damage = Math.Max(0m, (decimal)attacker.DamageFull - Math.Max(0, target.DefenceFull));
            isCrit = random(10000) < Math.Clamp(attacker.CritFull, 0, 100) * 100;
            if (isCrit)
            {
                damage *= 2;
            }
            return (long)Math.Min(damage, long.MaxValue);
        }

        public virtual async Task<InfoAttack?> AttackPlayer(ICharacter character, int targetId, string playToken = "",
            Action<string>? onError = null, CancellationToken cancellationToken = default, int skillSlot = -1)
        {
            if (character is not GameCharacter attacker || _cache == null || _config == null || _database == null || _clients == null)
            {
                onError?.Invoke("Nhân vật chưa sẵn sàng chiến đấu.");
                return null;
            }
            var handler = (CharacterHandler)attacker.CharacterHandler!;
            return await handler.Handle<InfoAttack>(async () =>
            {
                var target = _clients.GetCharacter(targetId);
                if (target == null || target.IsChangingMap || target.InfoGame.Breakthrough.Active || target.Id == attacker.Id || !ReferenceEquals(target.Zone, attacker.Zone) ||
                    ((CharacterHandler)target.CharacterHandler!).IsClosed || !attacker.TalentSeen || !target.TalentSeen ||
                    attacker.InfoGame.Hp <= 0 || target.InfoGame.Hp <= 0 || string.IsNullOrEmpty(playToken) || playToken != attacker.PlayToken)
                {
                    onError?.Invoke("Mục tiêu hoặc phiên chơi không hợp lệ.");
                    return null;
                }
                float dx = target.Position.X - attacker.Position.X;
                float dy = target.Position.Y - attacker.Position.Y;
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - attacker.InfoGame.LastAttackTime < _config.AttackCooldown)
                {
                    onError?.Invoke("Đòn đánh chưa hồi.");
                    return null;
                }
                var source = attacker.Clone();
                var victim = target.Clone();
                source.CharacterHandler = new CharacterHandler(source, _cache, _config);
                victim.CharacterHandler = new CharacterHandler(victim, _cache, _config);
                source.CharacterHandler.SetUpInfo();
                victim.CharacterHandler.SetUpInfo();
                var power = PrepareAttack(source, skillSlot, now, out float range, out short skillId, out string error);
                if (power == null || dx * dx + dy * dy > range * range)
                {
                    onError?.Invoke(power == null ? error : "Mục tiêu ở quá xa.");
                    return null;
                }
                long damage = GetDamage(power, victim.InfoChar, out bool crit, out bool dodge);
                victim.InfoGame.Hp = Math.Max(0, victim.InfoGame.Hp - damage);
                source.InfoGame.LastAttackTime = now;
                if (victim.InfoGame.Hp == 0)
                {
                    victim.InfoGame.DeathTime = now;
                    victim.TargetX = victim.Position.X;
                    victim.TargetY = victim.Position.Y;
                }
                source.CharacterHandler.SetUpInfo();
                victim.CharacterHandler.SetUpInfo();
                if (!await _database.SaveCombat(source, victim))
                {
                    handler.Close();
                    target.CharacterHandler!.Close();
                    onError?.Invoke("Dữ liệu đã thay đổi. Hãy vào lại game.");
                    return null;
                }
                handler.SetData(source);
                ((CharacterHandler)target.CharacterHandler!).SetData(victim);
                target.TargetX = victim.TargetX;
                target.TargetY = victim.TargetY;
                var attack = new InfoAttack
                {
                    Id = Interlocked.Increment(ref _nextAttackId),
                    AttackerId = attacker.Id,
                    TargetId = target.Id,
                    SkillId = skillId,
                    Damage = damage,
                    IsCrit = crit,
                    IsDodge = dodge,
                    TargetHp = target.InfoGame.Hp,
                    TargetHpFull = target.InfoChar.HpFull,
                    Time = now
                };
                var zone = (GameZone)attacker.Zone!;
                while (zone.Attacks.Count >= 32 || (zone.Attacks.TryPeek(out var previous) && previous.Time < now - 3000))
                {
                    zone.Attacks.Dequeue();
                }
                zone.Attacks.Enqueue(attack);
                return attack;
            }, reason => onError?.Invoke(reason), cancellationToken);
        }

    }
}
