using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class BaseScene : MonoBehaviour
    {
        public TMP_Text ErrorText;
        public TMP_Text BusyText;
        public Selectable[] Controls = Array.Empty<Selectable>();
        public bool Busy { get; private set; }

        protected GameClient Client
        {
            get
            {
                return GameClient.Instance;
            }
        }

        protected bool EnsureClient()
        {
            if (Client == null)
            {
                Instantiate(Resources.Load<GameObject>("Prefabs/GameRoot"));
            }
            if (SceneManager.GetActiveScene().name != "LoadingScene" && Client.Catalog == null)
            {
                SceneManager.LoadScene("LoadingScene");
                return false;
            }
            return true;
        }

        protected void Run(Func<Task> action)
        {
            if (!Busy)
            {
                _ = Execute(action);
            }
        }

        private async Task Execute(Func<Task> action)
        {
            Busy = true;
            OnBusyChanged();
            if (ErrorText)
            {
                ErrorText.text = string.Empty;
            }

            try
            {
                await action();
            }
            catch (Exception error)
            {
                if (this && ErrorText)
                {
                    ErrorText.text = error.Message;
                }
            }
            finally
            {
                Busy = false;
                if (this)
                {
                    OnBusyChanged();
                }
            }
        }

        protected virtual void OnBusyChanged()
        {
            foreach (var control in Controls)
            {
                if (control)
                {
                    control.interactable = !Busy;
                }
            }

            if (BusyText)
            {
                BusyText.text = Busy ? "Đang xử lý..." : string.Empty;
            }
        }
    }
}
