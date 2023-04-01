using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;

public interface ICoverageFinder {
    string SortValueForScriptFile(string scriptFileName, bool byLastWriteTime);
    Task<IList<string>> GetOrderedScriptFileNamesAsync(bool byLastWriteTime, bool ignoreUncovered, bool ignoreValidation, bool ignoreUnitTest);
    Task<IList<string>> GetLastModifiedPhpFilesWithoutCoverageAsync();
    int NumberOfResults(string wildcard);
}