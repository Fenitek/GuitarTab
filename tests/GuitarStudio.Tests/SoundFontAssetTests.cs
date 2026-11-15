using System.Security.Cryptography;

namespace GuitarStudio.Tests;

public sealed class SoundFontAssetTests
{
    [Fact]
    public void BundledGeneralUserHasTheVerifiedIdentityAndFitsTheSizeLimit()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "GeneralUser-GS.sf2"));
        Assert.Equal(31281186, bytes.Length);
        Assert.True(bytes.Length < 50_000_000);
        Assert.Equal("f45b6b4a68b6bf3d792fcbb6d7de24dc701a0f89c5900a21ef3aaece993b839a",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Fact]
    public void TheUnmodifiedLicenseTravelsAlongsideTheSoundFont()
    {
        var license = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "GeneralUser-GS-LICENSE.txt"));
        var prefix = System.Text.Encoding.ASCII.GetBytes($"blob {license.Length}\0");
        Assert.Equal("f8c4cc009ea1fd4f0a797e540b737dfe3a56843c",
            Convert.ToHexString(SHA1.HashData([.. prefix, .. license])).ToLowerInvariant());
    }
}
