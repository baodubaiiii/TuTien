using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class CharacterPreview : MonoBehaviour
    {
        public GameObject Figure;
        public Image Robe;
        public Image Hair;
        public TMP_Text Name;
        public TMP_Text Description;

        public void SetData(string name, int gender, int appearance, string description)
        {
            Figure.SetActive(appearance > 0);
            Name.text = name;
            Description.text = description;
            float shade = (appearance % 3) * 0.06f;
            Robe.color = gender == 0 ? new Color(0.3f + shade, 0.5f, 0.45f) : new Color(0.5f, 0.38f + shade, 0.42f);
            ((RectTransform)Hair.transform).sizeDelta = new Vector2(gender == 0 ? 48 : 62, gender == 0 ? 24 : 52);
        }
    }
}
