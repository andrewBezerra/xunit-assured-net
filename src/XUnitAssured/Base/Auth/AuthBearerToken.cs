namespace XUnitAssured.Base.Auth;

public class AuthBearerToken
{
	public AuthBearerToken(string access_token, int expires_in, int refresh_expires_in, string token_type, int not_before_policy, string scope)
	{
		Access_token = access_token;
		Expires_in = expires_in;
		Refresh_expires_in = refresh_expires_in;
		Token_type = token_type;
		Not_before_policy = not_before_policy;
		Scope = scope;
	}

	public string Access_token { get; set; }
	public int Expires_in { get; set; }
	public int Refresh_expires_in { get; set; }
	public string Token_type { get; set; }
	public int Not_before_policy { get; set; }
	public string Scope { get; set; }
}

