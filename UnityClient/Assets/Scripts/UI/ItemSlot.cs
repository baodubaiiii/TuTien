using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class ItemSlot : MonoBehaviour
    {
        public Button Button;
        public Image Background;
        public TMP_Text Label;
        public TMP_Text Quantity;
        public Image Selection;

        public void SetData(string name, int quantity, bool selected)
        {
            Label.text = name;
            Quantity.text = quantity > 1 ? quantity.ToString() : "";
            Background.color = string.IsNullOrEmpty(name) ? new Color(0.9f, 0.91f, 0.87f) : new Color(0.97f, 0.96f, 0.92f);
            Selection.enabled = selected;
        }
    }
}
