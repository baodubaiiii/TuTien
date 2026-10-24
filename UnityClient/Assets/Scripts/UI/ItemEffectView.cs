using TMPro;
using UnityEngine;

namespace TuTien.Client
{
    public class ItemEffectView : MonoBehaviour
    {
        public TMP_Text Name;
        public TMP_Text Time;

        public void SetData(string name, long seconds)
        {
            Name.text = name;
            Time.text = seconds / 60 + ":" + (seconds % 60).ToString("00");
        }
    }
}
