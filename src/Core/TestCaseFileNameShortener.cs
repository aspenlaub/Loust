using System.Globalization;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Core;

public class TestCaseFileNameShortener : ITestCaseFileNameShortener {
    public string CoverageFileForScriptFile(IFolder folder, string scriptFileName) {
        return folder.FullName + '\\' + CoverageFileForScriptFileShortName(scriptFileName);
    }

    public string CoverageFileForScriptFileShortName(string scriptFileName) {
        return "oust_" + scriptFileName.Substring(scriptFileName.LastIndexOf('\\') + 1)
                                       .Replace(".xml", ".txt")
                                       .ToLower(CultureInfo.InvariantCulture)
                                       .Replace("ä", "ae")
                                       .Replace("ö", "oe")
                                       .Replace("ü", "ue")
                                       .Replace("ß", "ss")
                                       .Replace(" ", "_");
    }
}