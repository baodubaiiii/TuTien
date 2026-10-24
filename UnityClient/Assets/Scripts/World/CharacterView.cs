using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace TuTien.Client
{
    public class CharacterView : MonoBehaviour
    {
        public SpriteRenderer Body;
        public SpriteRenderer Selection;
        public TMP_Text Name;
        public Transform Health;
        public int Id { get; private set; }
        public bool IsNpc { get; private set; }
        public bool IsMonster { get; private set; }
        private Vector3 destination;
        private bool initialized;
        private SpriteRenderer[] renderers;
        private MeshRenderer nameRenderer;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            nameRenderer = Name.GetComponent<MeshRenderer>();
        }

        public void SetData(JObject data, bool local, bool selected, bool npc = false, bool monster = false)
        {
            Id = data.Value<int>("id");
            IsNpc = npc;
            IsMonster = monster;
            destination = new Vector3(data.Value<float>("x") / 100f, -data.Value<float>("y") / 100f, 0);
            if (!initialized)
            {
                transform.position = destination;
                initialized = true;
            }
            Name.text = data.Value<string>("name") + (local ? " (Bạn)" : string.Empty);
            Name.transform.localPosition = new Vector3(0, npc || local ? 1.23f : 1.65f, 0);
            Name.color = local ? new Color(0.68f, 0.94f, 0.82f) : new Color(0.94f, 0.92f, 0.85f);
            Selection.enabled = selected;
            Body.color = npc ? new Color(0.8f, 0.66f, 0.35f) : local ? new Color(0.24f, 0.77f, 0.68f) : new Color(0.83f, 0.43f, 0.29f);
            if (monster)
            {
                Body.color = new Color(0.64f, 0.32f, 0.24f);
                Name.color = new Color(0.98f, 0.77f, 0.6f);
                GetComponent<CircleCollider2D>().enabled = data.Value<long>("hp") > 0;
            }
            Health.gameObject.SetActive(!npc);
            if (!npc)
            {
                long hp = data.Value<long>("hp");
                float fraction = hp / (float)System.Math.Max(1, data.Value<long>("hpMax"));
                Health.localScale = new Vector3(Mathf.Clamp01(fraction) * 0.6f, 0.045f, 1);
                if (hp == 0)
                {
                    Body.color = Color.gray;
                }
            }
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, destination, Mathf.Min(1, Time.deltaTime * 15));
            int order = 1000 - Mathf.RoundToInt(transform.position.y * 100);
            int index = 0;
            foreach (var sprite in renderers)
            {
                sprite.sortingOrder = order + index++;
            }
            Selection.sortingOrder = order - 10;
            nameRenderer.sortingOrder = 3000;
        }
    }
}
