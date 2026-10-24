using System.Security.Cryptography;
using TuTien.Application.Handlers.Character;
using TuTien.Application.Interfaces.Character;
using TuTien.Model.Info;
using TuTien.Model.Item;
using TuTien.Application.MainTasks;
using GameCharacter = TuTien.Model.Character.Character;
using GameZone = TuTien.Model.Map.Zone;

namespace TuTien.Application.Handlers.Skill
{
    public partial class SkillHandler
    {
        public virtual async Task<InfoAttack?> AttackMonster(ICharacter character, int targetId, string lifeId, string playToken,
            Action<string>? onError = null, CancellationToken cancellationToken = default, int skillSlot = -1)
        {
            if (character is not GameCharacter attacker || _cache == null || _config == null || _database == null)
            {
                onError?.Invoke("Nhân vật chưa sẵn sàng chiến đấu.");
                return null;
            }
            var handler = (CharacterHandler)attacker.CharacterHandler!;
            return await handler.Handle<InfoAttack>(async () =>
            {
                var zone = (GameZone)attacker.Zone!;
                if (!zone.MonsterMaps.TryGetValue(targetId, out var monster) || monster.IsDie || monster.IsReturning || monster.LifeId != lifeId ||
                    !attacker.TalentSeen || attacker.InfoGame.Hp <= 0 || string.IsNullOrEmpty(playToken) || attacker.PlayToken != playToken)
                {
                    onError?.Invoke("Quái hoặc phiên chơi không hợp lệ.");
                    return null;
                }
                float dx = monster.Position.X - attacker.Position.X;
                float dy = monster.Position.Y - attacker.Position.Y;
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - attacker.InfoGame.LastAttackTime < _config.AttackCooldown)
                {
                    onError?.Invoke("Quái ở quá xa hoặc đòn đánh chưa hồi.");
                    return null;
                }
                var draft = attacker.Clone();
                draft.CharacterHandler = new CharacterHandler(draft, _cache, _config);
                draft.CharacterHandler.SetUpInfo();
                var power = PrepareAttack(draft, skillSlot, now, out float range, out short skillId, out string error);
                if (power == null || dx * dx + dy * dy > range * range)
                {
                    onError?.Invoke(power == null ? error : "Quái ở quá xa.");
                    return null;
                }
                long damage = GetDamage(power, monster.InfoChar, out bool crit, out bool dodge);
                long hp = Math.Max(0, monster.InfoChar.Hp - damage);
                draft.InfoGame.LastAttackTime = now;
                if (skillId != 0)
                {
                    new TaskHandler(_cache, _config).UpdateCount(draft, "cast", skillId);
                }
                ItemMap? item = null;
                bool saved;
                if (hp == 0)
                {
                    if (zone.ItemMaps.Count >= 128 || draft.InfoGame.TuVi > long.MaxValue - monster.Template.TuVi)
                    {
                        onError?.Invoke("Vật phẩm trong khu vực đã đầy. Hãy nhặt đồ hoặc chờ một lát.");
                        return null;
                    }
                    draft.InfoGame.TuVi = checked(draft.InfoGame.TuVi + monster.Template.TuVi);
                    new TaskHandler(_cache, _config).UpdateCount(draft, "kill", monster.Template.Id);
                    if (RandomNumberGenerator.GetInt32(100) < monster.Template.DropChance)
                    {
                        item = new ItemMap
                        {
                            PlayerId = attacker.Id,
                            MapId = attacker.MapId,
                            ZoneId = attacker.ZoneId,
                            ItemId = monster.Template.DropItemId,
                            Quantity = monster.Template.DropQuantity,
                            X = monster.Position.X,
                            Y = monster.Position.Y,
                            OwnerTime = now + _config.DropOwnerSeconds * 1000L,
                            LeftTime = now + _config.DropLifeSeconds * 1000L
                        };
                    }
                    saved = await _database.SaveMonsterKill(draft, monster.LifeId, monster.Template.TuVi, item, now);
                }
                else
                {
                    saved = await _database.SaveGame(draft);
                }
                if (!saved)
                {
                    if (hp == 0 && await _database.HasMonsterKill(monster.LifeId))
                    {
                        monster.InfoChar.Hp = 0;
                        monster.IsDie = true;
                        monster.RespawnTime = now + monster.Template.RespawnSeconds * 1000L;
                        foreach (var drop in await _database.LoadItemMaps(now))
                        {
                            if (drop.MapId == attacker.MapId && drop.ZoneId == attacker.ZoneId && zone.ItemMaps.Count < 128)
                            {
                                zone.ItemMaps.TryAdd(drop.Id, drop);
                            }
                        }
                    }
                    handler.Close();
                    onError?.Invoke("Dữ liệu đã thay đổi. Hãy vào lại game.");
                    return null;
                }
                draft.CharacterHandler.SetUpInfo();
                handler.SetData(draft);
                monster.InfoChar.Hp = hp;
                monster.TargetId = attacker.Id;
                if (hp == 0)
                {
                    monster.IsDie = true;
                    monster.TargetId = 0;
                    monster.RespawnTime = now + monster.Template.RespawnSeconds * 1000L;
                    if (item != null)
                    {
                        zone.ItemMaps.Add(item.Id, item);
                    }
                }
                var attack = new InfoAttack
                {
                    AttackerId = attacker.Id,
                    TargetId = monster.Id,
                    TargetType = "monster",
                    SkillId = skillId,
                    Damage = damage,
                    IsCrit = crit,
                    IsDodge = dodge,
                    TargetHp = hp,
                    TargetHpFull = monster.InfoChar.HpFull,
                    Time = now
                };
                AddAttack(zone, attack);
                return attack;
            }, reason => onError?.Invoke(reason), cancellationToken);
        }

        internal static void AddAttack(GameZone zone, InfoAttack attack)
        {
            attack.Id = Interlocked.Increment(ref _nextAttackId);
            while (zone.Attacks.Count >= 32 || (zone.Attacks.TryPeek(out var previous) && previous.Time < attack.Time - 3000))
            {
                zone.Attacks.Dequeue();
            }
            zone.Attacks.Enqueue(attack);
        }
    }
}
