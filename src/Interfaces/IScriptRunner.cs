using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;

public interface IScriptRunner {
    Task<IFindIdleProcessResult> RunScriptAsync(string fileName, IErrorsAndInfos errorsAndInfos);
    Task<bool> RecoverScriptAsync(string fileName);
}