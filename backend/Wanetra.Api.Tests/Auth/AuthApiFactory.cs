namespace Wanetra.Api.Tests.Auth;

public sealed class AuthApiFactory(string authSetting = "") : WanetraApiFactory
{
    protected override string AuthSetting => authSetting;
}
