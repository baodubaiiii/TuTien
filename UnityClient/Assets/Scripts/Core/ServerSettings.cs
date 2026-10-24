using UnityEngine;

namespace TuTien.Client
{
    [CreateAssetMenu(menuName = "Tu Tien/Server Settings")]
    public class ServerSettings : ScriptableObject
    {
        public string Host = "127.0.0.1";
        public int Port = 7777;
        public string HttpUrl = "http://127.0.0.1:5080";
    }
}
