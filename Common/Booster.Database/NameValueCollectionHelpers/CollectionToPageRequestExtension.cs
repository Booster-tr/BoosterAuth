using Booster.ExceptionManager.Abstraction.Constants;
using FluentValidation;
using FluentValidation.Results;
﻿using Booster.Database.DynamicFilter.Models;
using System.Collections.Specialized;

namespace Booster.Database.NameValueCollectionHelpers;

public static class CollectionToPageRequestExtension
{
    public const string PageQueryStringName = "Page";

    public const string PageSizeQueryStringName = "PageSize";

    public const int MaxPageSize = 500;

    public static PageRequest ToPageRequest(this NameValueCollection nvc)
    {
        PageRequest pageRequest = new PageRequest
        {
            PageIndex = 1,
            PageSize = 10,
        };

        if (int.TryParse(nvc[PageQueryStringName], out int page))
        {
            if (page <= 0)
            {
                page = 1;
            }
            pageRequest.PageIndex = page;
        }

        // Sayfa boyutu 1..MaxPageSize: negatif deger veritabaninda LIMIT hatasi (500), 0 sonsuz sayfa sayisi, cok buyuk
        // deger tek istekte tum tabloyu dokuyordu.
        if (nvc[PageSizeQueryStringName] is { Length: > 0 } rawSize)
        {
            if (!int.TryParse(rawSize, out int pageSize) || pageSize is < 1 or > MaxPageSize)
                throw new ValidationException([new ValidationFailure(PageSizeQueryStringName, ValidationErrors.PageSizeInvalid)]);
            pageRequest.PageSize = pageSize;
        }

        return pageRequest;

    }
}
