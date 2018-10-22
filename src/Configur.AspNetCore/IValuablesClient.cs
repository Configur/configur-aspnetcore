using System.Collections.Generic;
using System.Threading.Tasks;

namespace Configur.AspNetCore
{
    public interface IValuablesClient
    {
        Task<IReadOnlyCollection<Valuable>> FindValuablesAsync
        (
            string appId,
            string appSecret,
            string appPassword
        );
    }
}
