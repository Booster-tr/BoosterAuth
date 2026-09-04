using Booster.Database.DynamicFilter.Models;

namespace Booster.Auth.Database.Models;

public record BaseDynamicRequest
{
    public PageRequest PageRequest { get; set; } = new PageRequest()
    {
        PageSize = 10,
        PageIndex = 0,
    };

    public DynamicQuery? DynamicQuery { get; set; }
}

