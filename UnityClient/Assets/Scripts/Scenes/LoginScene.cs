using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TuTien.Client
{
    public class LoginScene : BaseScene
    {
        public TMP_InputField Username;
        public TMP_InputField Password;
        public TMP_InputField ConfirmPassword;
        public GameObject ConfirmRow;
        public TMP_Text Title;
        public TMP_Text SubmitLabel;
        public Button Submit;
        public Button Register;
        public Button ShowPassword;
        public TMP_Text RegisterLabel;
        public TMP_Text PasswordLabel;
        private bool registering;

        private void Start()
        {
            if (!EnsureClient())
            {
                return;
            }

            Username.text = UnityEngine.PlayerPrefs.GetString("TuTien.Username", "");
            Submit.onClick.AddListener(() => Run(Login));
            Register.onClick.AddListener(ChangeMode);
            ShowPassword.onClick.AddListener(TogglePassword);
            Password.onSubmit.AddListener(value => Run(Login));
            ConfirmPassword.onSubmit.AddListener(value => Run(Login));
            RefreshForm();
            Username.ActivateInputField();
        }

        private async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Username.text) || string.IsNullOrEmpty(Password.text))
            {
                ErrorText.text = "Nhập tài khoản và mật khẩu để tiếp tục.";
                return;
            }

            if (registering && Password.text != ConfirmPassword.text)
            {
                ErrorText.text = "Mật khẩu nhập lại chưa khớp.";
                ConfirmPassword.ActivateInputField();
                return;
            }

            UnityEngine.PlayerPrefs.SetString("TuTien.Username", Username.text.Trim());
            await Client.Login(Username.text.Trim(), Password.text, registering);
        }

        private void ChangeMode()
        {
            registering = !registering;
            RefreshForm();
        }

        private void RefreshForm()
        {
            Title.text = registering ? "Đăng ký" : "Đăng nhập";
            SubmitLabel.text = registering ? "Tạo tài khoản" : "Đăng nhập";
            RegisterLabel.text = registering ? "Đã có tài khoản? Đăng nhập" : "Chưa có tài khoản? Đăng ký";
            ConfirmRow.SetActive(registering);
            ((RectTransform)Submit.transform).anchoredPosition = new Vector2(0, registering ? -145 : -60);
            ((RectTransform)Register.transform).anchoredPosition = new Vector2(0, registering ? -220 : -135);
            ((RectTransform)ErrorText.transform).anchoredPosition = new Vector2(0, registering ? -283 : -210);
            ((RectTransform)BusyText.transform).anchoredPosition = new Vector2(0, registering ? -332 : -260);
            ErrorText.text = string.Empty;
        }

        private void TogglePassword()
        {
            bool show = Password.contentType == TMP_InputField.ContentType.Password;
            Password.contentType = show ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            Password.ForceLabelUpdate();
            PasswordLabel.text = show ? "Ẩn" : "Hiện";
        }
    }
}
