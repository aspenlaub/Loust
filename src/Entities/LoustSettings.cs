using System.Xml.Serialization;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Entities;

public class LoustSettings : ISecretResult<LoustSettings> {
    [XmlAttribute("lastchangedphpfilesurl")]
    public string LastChangedPhpFilesUrl { get; set; }

    [XmlAttribute("trivialtest")]
    public string TrivialTest { get; set; }

    [XmlAttribute("scriptwildcard")]
    public string ScriptWildcard { get; set; }

    [XmlAttribute("anotherscriptwildcard")]
    public string AnotherScriptWildcard { get; set; }

    [XmlAttribute("yetanotherscriptwildcard")]
    public string YetAnotherScriptWildcard { get; set; }

    public LoustSettings Clone() {
        return new LoustSettings {
            LastChangedPhpFilesUrl = LastChangedPhpFilesUrl,
            TrivialTest = TrivialTest,
            ScriptWildcard = ScriptWildcard,
            AnotherScriptWildcard = AnotherScriptWildcard,
            YetAnotherScriptWildcard = YetAnotherScriptWildcard
        };
    }
}