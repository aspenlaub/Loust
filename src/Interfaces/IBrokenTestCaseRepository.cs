using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;

public interface IBrokenTestCaseRepository {
    Task RegisterAsync(string scriptFileName, IList<string> errors);
    Task RemoveAsync(string scriptFileName);
    Task<bool> ContainsAsync(string scriptFileName);
    Task<int> NumberOfBrokenTestsAsync();
}