using System.Collections.Generic;
using System.Threading.Tasks;

namespace Configur.AspNetCore
{
    public interface IAppSettingsClient
    {
        Task<IReadOnlyCollection<AppSetting>> FindAsync
        (
            string appId,
            string appSecret,
            string appPassword
        );
    }
}
