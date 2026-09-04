namespace Booster.Database.DynamicFilter.Models;

public class PageRequest : ICloneable
{
    public int PageIndex
    {
        get
        {
            return pageIndex;
        }
        set
        {
            if (value <= 0)
            {
                pageIndex = 0;
            }
            else
            {
                pageIndex = value - 1;
            }
        }
    }
    public int PageSize { get; set; }

    private int pageIndex;

    public object Clone()
    {
        return MemberwiseClone();
    }
}

