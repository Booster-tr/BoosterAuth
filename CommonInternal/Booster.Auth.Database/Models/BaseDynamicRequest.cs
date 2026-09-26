using Booster.Database.DynamicFilter.Models;

namespace Booster.Auth.Database.Models;

public record BaseDynamicRequest
{
    public PageRequest PageRequest { get; set; } = new PageRequest()
    {
        PageSize = 10,
        // 1 tabanli: PageRequest.PageIndex artik oldugu gibi saklaniyor ve
        // ToPaginateAsync ilk sayfa icin 1 bekliyor.
        PageIndex = 1,
    };

    public DynamicQuery? DynamicQuery { get; set; }
}

