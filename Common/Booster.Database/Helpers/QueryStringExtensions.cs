using System.Collections.Specialized;
using System.Web;
using Microsoft.AspNetCore.Http;

namespace Booster.Database.Helpers;

public static class QueryStringExtensions
{
    public static NameValueCollection ToNvc(this HttpRequest request)
    {
        var parameters = HttpUtility.ParseQueryString(request.QueryString.Value ?? "");
        return parameters;
    }

}
