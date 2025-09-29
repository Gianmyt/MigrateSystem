namespace Migration.API.Authentication
{
    public class AuthenticationModel
    {

    }

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
