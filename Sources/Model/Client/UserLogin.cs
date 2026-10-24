namespace TuTien.Model.Client
{
    public class UserLogin
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public UserLogin()
        {
            Username = string.Empty;
            Password = string.Empty;
        }
    }
}
