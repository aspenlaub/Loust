using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Entities;

public class SecretLoustSettings : ISecret<LoustSettings> {
    private static LoustSettings DefaultLoustSettings;
    public LoustSettings DefaultValue => DefaultLoustSettings ??= new LoustSettings { LastChangedPhpFilesUrl = @"http://localhost"};

    public string Guid => "B2188891-AEE7-018D-AF58-013A97FED5F3";
}