using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace TuTien.Client
{
    public class MapView : MonoBehaviour
    {
        public Transform Floor;
        public Transform Obstacles;
        public Sprite Sprite;
        public TMP_FontAsset Font;
        public float Width { get; private set; }
        public float Height { get; private set; }

        public void Load(JObject map)
        {
            Width = map.Value<float>("width") / 100f;
            Height = map.Value<float>("height") / 100f;
            Floor.localPosition = new Vector3(Width / 2, -Height / 2, 0);
            Floor.localScale = new Vector3(Width, Height, 1);
            foreach (Transform child in Obstacles)
            {
                Destroy(child.gameObject);
            }

            for (int x = 0; x < Mathf.CeilToInt(Width); x++)
            {
                for (int y = 0; y < Mathf.CeilToInt(Height); y++)
                {
                    var tile = new GameObject("Ground", typeof(SpriteRenderer));
                    tile.transform.SetParent(Obstacles, false);
                    tile.transform.position = new Vector3(x + 0.5f, -y - 0.5f, 0);
                    tile.transform.localScale = new Vector3(0.975f, 0.975f, 1);
                    var renderer = tile.GetComponent<SpriteRenderer>();
                    renderer.sprite = Sprite;
                    renderer.color = (x + y) % 2 == 0 ? new Color(0.18f, 0.29f, 0.25f) : new Color(0.19f, 0.31f, 0.26f);
                    renderer.sortingOrder = -50;
                }
            }
            foreach (JObject rectangle in map["mapCollisions"])
            {
                var obstacle = new GameObject("Obstacle", typeof(SpriteRenderer));
                obstacle.transform.SetParent(Obstacles, false);
                float width = rectangle.Value<float>("width") / 100f;
                float height = rectangle.Value<float>("height") / 100f;
                obstacle.transform.localPosition = new Vector3(rectangle.Value<float>("x") / 100f + width / 2, -rectangle.Value<float>("y") / 100f - height / 2, 0);
                obstacle.transform.localScale = new Vector3(width, height, 1);
                var renderer = obstacle.GetComponent<SpriteRenderer>();
                renderer.sprite = Sprite;
                renderer.color = new Color(0.36f, 0.4f, 0.31f);
                renderer.sortingOrder = 0;
            }
            foreach (JObject point in map["waypoint"])
            {
                var portal = new GameObject("Waypoint", typeof(WayPointView), typeof(SpriteRenderer), typeof(CircleCollider2D));
                portal.transform.SetParent(Obstacles, false);
                portal.transform.localPosition = new Vector3(point.Value<float>("x") / 100f, -point.Value<float>("y") / 100f, 0);
                portal.transform.localScale = new Vector3(0.7f, 0.7f, 1);
                portal.GetComponent<WayPointView>().MapId = point.Value<int>("mapId");
                portal.GetComponent<CircleCollider2D>().radius = 0.6f;
                var renderer = portal.GetComponent<SpriteRenderer>();
                renderer.sprite = Sprite;
                renderer.color = new Color(0.3f, 0.7f, 0.62f);
                renderer.sortingOrder = 100;
                var labelObject = new GameObject("Name", typeof(TextMeshPro));
                labelObject.transform.SetParent(portal.transform, false);
                labelObject.transform.localPosition = new Vector3(0, 1, 0);
                var label = labelObject.GetComponent<TextMeshPro>();
                label.font = Font;
                label.fontSize = 2.4f;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(5, 1.2f);
                var destination = GameClient.Instance.Catalog["maps"].OfType<JObject>().First(value => value.Value<int>("id") == point.Value<int>("mapId"));
                label.text = destination.Value<string>("name") + "\n[E]";
                label.GetComponent<MeshRenderer>().sortingOrder = 3000;
            }
        }
    }
}
