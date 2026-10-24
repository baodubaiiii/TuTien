using System.Threading.Tasks;
using TMPro;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class LoadingScene : BaseScene
    {
        public TMP_Text Status;
        public TMP_Text Size;
        public Button DownloadButton;
        public Slider Progress;
        private bool prepared;

        private void Start()
        {
            EnsureClient();
            DownloadButton.onClick.AddListener(() => Run(Download));
            Run(Prepare);
        }

        private async Task Prepare()
        {
            DownloadButton.interactable = false;
            Status.text = "Đang kiểm tra dữ liệu...";
            try
            {
                await Client.Prepare();
                prepared = true;
                Status.text = "Chuẩn bị nhập đạo";
                Size.text = "Dữ liệu cần tải: " + (Client.DownloadSize / 1048576f).ToString("0.00") + " MB";
            }
            finally
            {
                if (this)
                {
                    DownloadButton.interactable = true;
                }
            }
        }

        private async Task Download()
        {
            if (!prepared)
            {
                await Prepare();
                return;
            }
            DownloadButton.interactable = false;
            Status.text = "Đang tải dữ liệu...";
            try
            {
                await Client.Download(UpdateProgress);
                await GameClient.LoadScene("LoginScene");
            }
            finally
            {
                if (this)
                {
                    DownloadButton.interactable = true;
                }
            }
        }

        private void UpdateProgress(float value)
        {
            if (this)
            {
                Progress.value = value;
            }
        }
    }
}
