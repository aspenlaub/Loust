using System.Collections.Generic;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;

public interface IScriptFinder {
    Task<IEnumerable<string>> FindScriptFileNamesAsync(IErrorsAndInfos errorsAndInfos);
    Task<string> ScriptFolderAsync(IErrorsAndInfos errorsAndInfos);
}