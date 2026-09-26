namespace Booster.Database.DynamicFilter.Models;

public class PageRequest : ICloneable
{
    /// <summary>
    /// Kaçıncı sayfa. 1 TABANLIDIR: ilk sayfa 1'dir.
    /// <para>
    /// Burada eskiden <c>value - 1</c> ile 0 tabanlıya çevriliyordu; oysa bu değer
    /// doğrudan <c>ToPaginateAsync</c>'e veriliyor ve o 1 tabanlı çalışıyor
    /// (<c>Skip((index - 1) * size)</c>). İki tarafın tabanı uyuşmadığı için her
    /// sayfa bir öncekinin içeriğini gösteriyor, son sayfaya ise hiç
    /// ulaşılamıyordu. <c>Paginate.HasPrevious =&gt; Index &gt; 1</c> de 1 tabanı
    /// varsayıyor — dönüştürme kaldırıldı.
    /// </para>
    /// <para>
    /// Sıfır ve altı 1'e çekilir: değer istekten geliyor ve negatif bir sayı
    /// <c>Skip</c>'e ulaştığında sorgu hata fırlatırdı.
    /// </para>
    /// </summary>
    public int PageIndex
    {
        get => pageIndex;
        set => pageIndex = value <= 0 ? 1 : value;
    }

    public int PageSize { get; set; }

    private int pageIndex = 1;

    public object Clone()
    {
        return MemberwiseClone();
    }
}

