using TuTien.Application.Handlers.Character;
using TuTien.Application.Handlers.Skill;
using TuTien.Application.Interfaces.Monster;
using TuTien.Application.Manager;
using TuTien.Database;
using TuTien.Database.Player;
using TuTien.Model.Info;
using TuTien.Model.Monster;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Handlers.Monster
{
    public class MonsterMapHandler : IMonsterHandler
    {
        protected MonsterMap Monster { get; }
        private readonly MapManager _maps;
        private readonly CharacterDB _database;
        private readonly Cache _cache;
        private readonly ConfigManager _config;

        public MonsterMapHandler(MonsterMap monster, MapManager maps, CharacterDB database, Cache cache, ConfigManager config)
        {
            Monster = monster;
            _maps = maps;
            _database = database;
            _cache = cache;
            _config = config;
            Reset();
        }

        public void Reset()
        {
            Monster.LifeId = Guid.NewGuid().ToString("N");
            Monster.IsDie = false;
            Monster.IsReturning = false;
            Monster.TargetId = 0;
            Monster.RespawnTime = 0;
            Monster.LastAttackTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Monster.Position.X = Monster.HomeX;
            Monster.Position.Y = Monster.HomeY;
            Monster.InfoChar = new InfoChar
            {
                Hp = Monster.Template.Hp,
                HpFull = Monster.Template.Hp,
                DamageFull = Monster.Template.Damage,
                DefenceFull = Monster.Template.Defence
            };
        }

        public async Task Update(long timeServer, float delta)
        {
            if (Monster.IsDie)
            {
                if (timeServer >= Monster.RespawnTime)
                {
                    Reset();
                }
                return;
            }
            var target = Monster.Zone.Characters.GetValueOrDefault(Monster.TargetId) as GameCharacter;
            if (target == null && Monster.TargetId != 0)
            {
                Monster.TargetId = 0;
                Monster.IsReturning = true;
            }
            if (target != null && (!CanAttack(target) || Distance(target.Position.X, target.Position.Y, Monster.HomeX, Monster.HomeY) > Monster.Template.LeashRange))
            {
                Monster.TargetId = 0;
                Monster.IsReturning = true;
                target = null;
            }
            if (Monster.IsReturning)
            {
                Move(Monster.HomeX, Monster.HomeY, delta);
                if (Distance(Monster.Position.X, Monster.Position.Y, Monster.HomeX, Monster.HomeY) < 3)
                {
                    Monster.IsReturning = false;
                    Monster.InfoChar.Hp = Monster.InfoChar.HpFull;
                }
                return;
            }
            if (target == null)
            {
                float nearest = Monster.Template.AggroRange;
                foreach (var character in Monster.Zone.Characters.Values)
                {
                    if (character is GameCharacter candidate && CanAttack(candidate))
                    {
                        float distance = Distance(candidate.Position.X, candidate.Position.Y, Monster.Position.X, Monster.Position.Y);
                        if (distance < nearest && Distance(candidate.Position.X, candidate.Position.Y, Monster.HomeX, Monster.HomeY) <= Monster.Template.LeashRange)
                        {
                            nearest = distance;
                            target = candidate;
                        }
                    }
                }
                Monster.TargetId = target?.Id ?? 0;
            }
            if (target == null)
            {
                float direction = ((timeServer / 3000 + Monster.Id) % 2) == 0 ? 35 : -35;
                Move(Monster.HomeX + direction, Monster.HomeY, delta * 0.4f);
                return;
            }
            if (Distance(target.Position.X, target.Position.Y, Monster.Position.X, Monster.Position.Y) > Monster.Template.AttackRange)
            {
                Move(target.Position.X, target.Position.Y, delta);
                return;
            }
            if (timeServer - Monster.LastAttackTime < Monster.Template.AttackCooldown)
            {
                return;
            }
            var draft = target.Clone();
            draft.CharacterHandler = new CharacterHandler(draft, _cache, _config);
            draft.CharacterHandler.SetUpInfo();
            long damage = SkillHandler.GetDamage(Monster.InfoChar, draft.InfoChar, out bool crit, out bool dodge);
            draft.InfoGame.Hp = Math.Max(0, draft.InfoGame.Hp - damage);
            if (draft.InfoGame.Hp == 0)
            {
                draft.InfoGame.DeathTime = timeServer;
            }
            draft.CharacterHandler.SetUpInfo();
            if (damage > 0)
            {
                if (!await _database.SaveGame(draft))
                {
                    target.CharacterHandler!.Close();
                    return;
                }
                ((CharacterHandler)target.CharacterHandler!).SetData(draft);
            }
            Monster.LastAttackTime = timeServer;
            SkillHandler.AddAttack(Monster.Zone, new InfoAttack
            {
                AttackerId = Monster.Id,
                AttackerType = "monster",
                TargetId = target.Id,
                Damage = damage,
                IsCrit = crit,
                IsDodge = dodge,
                TargetHp = target.InfoGame.Hp,
                TargetHpFull = target.InfoChar.HpFull,
                Time = timeServer
            });
        }

        private bool CanAttack(GameCharacter character)
        {
            return character.TalentSeen && !character.IsChangingMap && !character.InfoGame.Breakthrough.Active && character.InfoGame.Hp > 0 &&
                character.CharacterHandler is CharacterHandler handler && !handler.IsClosed;
        }

        private void Move(float x, float y, float delta)
        {
            float distance = Distance(x, y, Monster.Position.X, Monster.Position.Y);
            if (distance < 0.1f)
            {
                return;
            }
            float step = Math.Min(distance, Monster.Template.Speed * delta);
            float nextX = Monster.Position.X + (x - Monster.Position.X) / distance * step;
            float nextY = Monster.Position.Y + (y - Monster.Position.Y) / distance * step;
            int mapId = Monster.Zone.Map!.Id;
            if (_maps.CanStand(mapId, nextX, Monster.Position.Y))
            {
                Monster.Position.X = nextX;
            }
            if (_maps.CanStand(mapId, Monster.Position.X, nextY))
            {
                Monster.Position.Y = nextY;
            }
        }

        private static float Distance(float x, float y, float otherX, float otherY)
        {
            return MathF.Sqrt((x - otherX) * (x - otherX) + (y - otherY) * (y - otherY));
        }
    }
}
