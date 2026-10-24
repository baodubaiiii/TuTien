using TMPro;
using UnityEngine;

namespace TuTien.Client
{
    public class FloatingText : MonoBehaviour
    {
        public TMP_Text Text;
        private float age;

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 0.55f;
            Text.alpha = Mathf.Clamp01((1.3f - age) * 2);
            if (age > 1.3f)
            {
                Destroy(gameObject);
            }
        }
    }
}
