using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;

public interface ITestCaseFileNameShortener {
    string CoverageFileForScriptFile(IFolder folder, string scriptFileName);
    string CoverageFileForScriptFileShortName(string scriptFileName);
}