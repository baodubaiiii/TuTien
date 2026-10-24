using TuTien.Application.Interfaces.Character;
using TuTien.Application.Handlers.Item;
using TuTien.Database;
using GameCharacter = TuTien.Model.Character.Character;

namespace TuTien.Application.Handlers.Character
{
    public partial class CharacterHandler : ICharacterHandler
    {
        protected ICharacter Character { get; }
        private readonly Cache? _cache;
        private readonly ConfigManager? _config;
        public bool IsClosed { get; private set; }

        public CharacterHandler(ICharacter character)
        {
            Character = character;
        }

        public CharacterHandler(GameCharacter character, Cache cache, ConfigManager config)
        {
            Character = character;
            _cache = cache;
            _config = config;
        }

        public bool InitGame()
        {
            if (Character is not GameCharacter character || _cache == null || _config == null)
            {
                return false;
            }

            if (character.InfoGame.Version > 0)
            {
                CheckProgression(character);
                var all = character.ItemBag.Concat(character.ItemBody).Concat(character.ItemBox).ToArray();
                if (character.InfoGame.Version != 1 || character.ItemBag.Count > _config.BagCapacity || character.ItemBody.Count > 6 ||
                    all.Any(item => !Guid.TryParseExact(item.Uid, "N", out _) || !_cache.ItemTemplates.ContainsKey(item.Id) || item.Quantity < 1 || item.Quantity > _cache.ItemTemplates[item.Id].MaxStack) ||
                    all.Select(item => item.Uid).Distinct().Count() != all.Length ||
                    character.ItemBag.Any(item => item.IndexUI < 0 || item.IndexUI >= _config.BagCapacity) ||
                    character.ItemBag.Select(item => item.IndexUI).Distinct().Count() != character.ItemBag.Count ||
                    character.ItemBox.Any(item => item.IndexUI < 0) ||
                    character.ItemBox.Select(item => item.IndexUI).Distinct().Count() != character.ItemBox.Count ||
                    character.ItemBody.Any(item => !_cache.ItemTemplates[item.Id].IsTypeBody() || item.IndexUI != _cache.ItemTemplates[item.Id].Type || item.Quantity != 1) ||
                    character.ItemBody.Select(item => item.IndexUI).Distinct().Count() != character.ItemBody.Count)
                {
                    throw new InvalidDataException("Invalid stored inventory");
                }
                SetUpInfo();
                return false;
            }

            character.InfoGame.Version = 1;
            character.InfoGame.PointBudget = _config.StartingPoints;
            character.InfoGame.Points = new int[5];
            var handler = new ItemHandler(_cache);
            var templates = _cache.ItemTemplates.Values.Where(item => item.RealmId == 1 &&
                ((item.Type == 0 && item.ClassId == character.ClassId) || (item.Type >= 1 && item.Type <= 6))).OrderBy(item => item.Id);
            foreach (var template in templates)
            {
                var item = handler.GetItemDefault(template.Id)!;
                item.IndexUI = character.ItemBag.Count;
                item.Quantity = template.Type == 6 ? 5 : 1;
                character.ItemBag.Add(item);
            }

            SetUpInfo();
            character.InfoGame.Hp = character.InfoChar.HpFull;
            character.InfoGame.Mp = character.InfoChar.MpFull;
            SetUpInfo();
            return true;
        }

        public virtual void Update()
        {
        }

        public bool Update(long timeServer)
        {
            if (Character is not GameCharacter character)
            {
                return false;
            }

            if (character.InfoGame.Effects.RemoveAll(effect => effect.ExpireTime <= timeServer) == 0)
            {
                return false;
            }

            SetUpInfo();
            return true;
        }

        public virtual void SetUpInfo()
        {
            if (Character is not GameCharacter character || _cache == null || _config == null)
            {
                return;
            }

            if (character.InfoGame.Points.Length != 5 || character.InfoGame.Points.Any(point => point < 0) ||
                character.InfoGame.Points.Sum(point => (long)point) > character.InfoGame.PointBudget)
            {
                throw new InvalidDataException("Invalid stored potential points");
            }

            var items = new decimal[5];
            foreach (var item in character.ItemBody)
            {
                foreach (var option in item.Options)
                {
                    if (option.Id >= 1 && option.Id <= 5)
                    {
                        items[option.Id - 1] += option.Param;
                    }
                }
            }

            var effects = new decimal[5];
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var effect in character.InfoGame.Effects.Where(value => value.ExpireTime > now))
            {
                if (effect.StatId >= 1 && effect.StatId <= 5)
                {
                    effects[effect.StatId - 1] += effect.Param;
                }
            }

            var info = character.InfoChar;
            character.AttackRange = _config.AttackRange;
            character.AttackCooldown = _config.AttackCooldown;
            info.KhiHuyetFromPoint = character.InfoGame.Points[0];
            info.ChanLucFromPoint = character.InfoGame.Points[1];
            info.PhapLucFromPoint = character.InfoGame.Points[2];
            info.LinhGiacFromPoint = character.InfoGame.Points[3];
            info.SatCoFromPoint = character.InfoGame.Points[4];
            info.KhiHuyetFromItem = items[0];
            info.ChanLucFromItem = items[1];
            info.PhapLucFromItem = items[2];
            info.LinhGiacFromItem = items[3];
            info.SatCoFromItem = items[4];
            info.KhiHuyetFromEffect = effects[0];
            info.ChanLucFromEffect = effects[1];
            info.PhapLucFromEffect = effects[2];
            info.LinhGiacFromEffect = effects[3];
            info.SatCoFromEffect = effects[4];
            info.SetUpInfo(_cache.RaceTemplates.GetValueOrDefault(character.RaceId) ?? new TuTien.Model.Template.RaceTemplate());
            info.HpFull = Math.Max(1, (long)decimal.Floor(info.KhiHuyetFull * _config.HpPerKhiHuyet));
            info.MpFull = Math.Max(1, (long)decimal.Floor(info.PhapLucFull * _config.MpPerPhapLuc));
            info.DamageFull = (long)Math.Clamp(decimal.Floor(character.ClassId == 3 || character.ClassId == 4 ? info.PhapLucFull : info.ChanLucFull), 0, long.MaxValue);
            info.DefenceFull = (long)Math.Clamp(decimal.Floor(info.LinhGiacFull), 0, long.MaxValue);
            info.CritFull = Math.Clamp(info.SatCoFull * _config.CritPerSatCo, 0, 100);
            info.DodgeFull = Math.Clamp(info.LinhGiacFull * _config.DodgePerLinhGiac, 0, 100);
            character.InfoGame.Hp = Math.Clamp(character.InfoGame.Hp, 0, info.HpFull);
            character.InfoGame.Mp = Math.Clamp(character.InfoGame.Mp, 0, info.MpFull);
            info.Hp = character.InfoGame.Hp;
            info.Mp = character.InfoGame.Mp;
        }

        public bool ChangePotential(int[] points, out string error)
        {
            error = string.Empty;
            if (Character is not GameCharacter character || points.Length != 5 || points.Any(point => point < 0) ||
                points.Sum(point => (long)point) > character.InfoGame.PointBudget)
            {
                error = "Số điểm phân bổ không hợp lệ hoặc vượt điểm còn lại.";
                return false;
            }

            character.InfoGame.Points = (int[])points.Clone();
            SetUpInfo();
            return true;
        }

        public virtual void SendInfo()
        {
        }

        public virtual void Close()
        {
            IsClosed = true;
        }
    }
}
