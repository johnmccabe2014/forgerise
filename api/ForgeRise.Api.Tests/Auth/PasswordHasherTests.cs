using ForgeRise.Api.Auth;
using Xunit;

namespace ForgeRise.Api.Tests.Auth;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Round_trips_a_password()
    {
        var encoded = _hasher.Hash("correct horse battery staple");
        Assert.StartsWith("argon2id$v=19$", encoded);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("correct horse battery staple", encoded));
    }

    [Fact]
    public void Rejects_wrong_password()
    {
        var encoded = _hasher.Hash("correct horse battery staple");
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("wrong password value", encoded));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-argon2-hash")]
    [InlineData("argon2id$v=19$bad")]
    [InlineData("argon2id$v=19$m=99,t=99,p=99$###$###")]
    public void Rejects_malformed_encoded_strings(string encoded)
    {
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.Verify("any password value", encoded));
    }

    [Fact]
    public void Different_invocations_produce_different_hashes_due_to_random_salt()
    {
        var a = _hasher.Hash("same-password-1234");
        var b = _hasher.Hash("same-password-1234");
        Assert.NotEqual(a, b);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("same-password-1234", a));
        Assert.Equal(PasswordVerificationResult.Success, _hasher.Verify("same-password-1234", b));
    }
}
