using Booster.Database.DynamicFilter.Models;
using System.Collections.Specialized;
using System.Reflection;

namespace Booster.Database.NameValueCollectionHelpers;

public static class CollectionToDynamicQueryExtension
{
    public const string SortFieldQueryStringName = "Sort.Field";

    public const string SortOrderOperatorQueryStringName = "Sort.OrderOperator";

    public const string LanguageCodeFilterName = "LanguageCode";

    private static string[] ExtractFilters = new[] { CollectionToPageRequestExtension.PageQueryStringName, CollectionToPageRequestExtension.PageSizeQueryStringName, SortOrderOperatorQueryStringName, SortFieldQueryStringName, LanguageCodeFilterName };

    private static readonly Dictionary<string, FilterOperator> OperatorMapping = new Dictionary<string, FilterOperator>(StringComparer.OrdinalIgnoreCase)
    {
        { "eq", FilterOperator.Equals },
        { "ne", FilterOperator.DoesntEqual },
        { "gt", FilterOperator.GreaterThan },
        { "gte", FilterOperator.GreaterThanOrEqual },
        { "lt", FilterOperator.LessThan },
        { "lte", FilterOperator.LessThanOrEqual },
        { "contains", FilterOperator.Contains },
        { "containsic", FilterOperator.ContainsIgnoreCase },
        { "notcontains", FilterOperator.NotContains },
        { "startswith", FilterOperator.StartsWith },
        { "endswith", FilterOperator.EndsWith },
        { "isempty", FilterOperator.IsEmpty },
        { "isnotempty", FilterOperator.IsNotEmpty },
        { "raw", FilterOperator.Raw }
    };

    public static DynamicQuery ToDynamicFilter<T>(this NameValueCollection nvc)
    {
        DynamicQuery dynamicQuery = new DynamicQuery
        {
            Sort = GetSortInfo(nvc),
            Filter = BuildDynamicFilters<T>(nvc),
            LanguageCode = nvc[LanguageCodeFilterName] ?? Thread.CurrentThread.CurrentCulture.Name
        };

        return dynamicQuery;
    }

    private static Sort? GetSortInfo(NameValueCollection nvc)
    {
        bool hasSort = false;
        var sort = new Sort();

        if (!string.IsNullOrEmpty(nvc[SortFieldQueryStringName]))
        {
            var rawField = nvc[SortFieldQueryStringName]!;
            hasSort = true;
            sort.Field = ProcessSortField(rawField);
        }

        if (!string.IsNullOrEmpty(nvc[SortOrderOperatorQueryStringName]))
        {
            sort.OrderOperator = nvc[SortOrderOperatorQueryStringName]!.ToLower() == "asc"
                ? OrderOperator.Asc
                : OrderOperator.Desc;
        }

        return hasSort ? sort : null;
    }

    private static (string propertyName, FilterOperator filterOperator) ParseKeyWithOperator(string key)
    {
        if (!key.Contains('-'))
            return (key, FilterOperator.Equals);

        var parts = key.Split('-', 2);
        if (parts.Length != 2)
            return (key, FilterOperator.Equals);

        var propertyName = parts[0];
        var operatorCode = parts[1];

        if (OperatorMapping.TryGetValue(operatorCode, out var filterOperator))
            return (propertyName, filterOperator);

        return (key, FilterOperator.Equals);
    }

    private static Filter? BuildDynamicFilters<T>(NameValueCollection nvc)
    {
        PropertyInfo[] properties = typeof(T).GetProperties();

        List<string> allKeys = nvc.AllKeys
            .Where(w => !ExtractFilters.Contains(w) && !string.IsNullOrWhiteSpace(w) && !string.IsNullOrWhiteSpace(nvc[w]))
            .Where(w => IsKnownProperty(properties, w!))
            .ToList()!;

        Filter? rootFilter = null;

        foreach (string key in allKeys)
        {
            string value = nvc[key]!;
            var (cleanKey, explicitOperator) = ParseKeyWithOperator(key);

            Filter newFilter;

            if (IsNestedProperty(cleanKey))
            {
                newFilter = CreateNestedPropertyFilter(cleanKey, value, explicitOperator);
            }
            else
            {
                var propertyName = cleanKey;
                newFilter = CreateSubFilters(Filter.Create(propertyName, explicitOperator, value));
            }

            rootFilter = AppendFilter(rootFilter, newFilter);
        }

        return rootFilter;
    }


    private static bool IsKnownProperty(PropertyInfo[] properties, string key)
    {
        var (cleanKey, _) = ParseKeyWithOperator(key);

        var root = cleanKey.Split('.')[0];

        return properties.Any(p => string.Equals(p.Name, root, StringComparison.OrdinalIgnoreCase));
    }

    private static Filter CreateSubFilters(Filter filter)
    {
        var values = filter.Value!.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length <= 1)
            return filter;

        var first = values.First();
        var root = new Filter
        {
            Field = filter.Field,
            Operator = filter.Operator,
            Logic = Logic.Or,
            Value = first,
            Filters = [.. values.Where(w => w != first).Select(v => new Filter
            {
                Field = filter.Field,
                Operator = filter.Operator,
                Value = v,
                Logic = Logic.Or
            })]
        };

        return root;
    }

    private static Filter AppendFilter(Filter? root, Filter newFilter)
    {
        if (root == null)
            return newFilter;

        // Eğer root'un kendi Or mantığı varsa (pipe ile ayrılmış değerler),
        // root'u bir wrapper filter içine almalıyız ki Or mantığı bozulmasın
        if (root.Logic == Logic.Or && root.Filters != null && root.Filters.Any())
        {
            var wrapper = new Filter
            {
                Operator = FilterOperator.Equals,
                Logic = Logic.And,
                Filters = new List<Filter> { root, newFilter }
            };
            return wrapper;
        }

        if (root.Filters == null)
            root.Filters = new List<Filter>();

        root.Logic = Logic.And;
        root.Filters.Add(newFilter);
        return root;
    }


    public static IList<Filter> GetAllFilters(Filter filter)
    {
        List<Filter> filters = new();
        GetFilters(filter, filters);
        return filters;
    }

    private static void GetFilters(Filter filter, IList<Filter> filters)
    {
        filters.Add(filter);
        if (filter.Filters is not null && filter.Filters.Any())
            foreach (Filter item in filter.Filters)
                GetFilters(item, filters);
    }


    public static bool IsGenericType(this Type type, Type genericType) => type.IsGenericType && type.GetGenericTypeDefinition() == genericType;

    public static bool IsNullableType(this Type type) => type.IsGenericType(typeof(Nullable<>));

    private static string ProcessSortField(string rawField)
    {
        if (!IsNestedProperty(rawField))
            return rawField;

        var parts = rawField.Split('.');
        if (parts.Length < 2)
            return rawField;

        if (IsCollectionProperty(parts[0]))
        {
            return $"{parts[0]}.First().{parts[1]}";
        }

        return rawField;
    }

    private static bool IsNestedProperty(string key)
    {
        return key.Contains('.') && !key.StartsWith("Sort.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCollectionProperty(string propertyName)
    {
        return propertyName.EndsWith("s", StringComparison.OrdinalIgnoreCase);
    }

    private static Filter CreateNestedPropertyFilter(string key, string value, FilterOperator explicitOperator)
    {
        var parts = key.Split('.');
        if (parts.Length < 2)
            return Filter.Create(key, explicitOperator, value);

        var firstPart = parts[0];

        if (IsCollectionProperty(firstPart))
        {
            var collectionName = firstPart;
            var fieldName = parts[1];
            var rawQuery = CreateCollectionQuery(collectionName, fieldName, explicitOperator);

            return CreateSubFilters(new Filter
            {
                Field = rawQuery,
                Value = value,
                Operator = FilterOperator.Raw
            });
        }
        else
        {
            return CreateSubFilters(Filter.Create(key, explicitOperator, value));
        }
    }

    private static string CreateCollectionQuery(string collectionName, string fieldName, FilterOperator filterOperator)
    {
        var operatorExpression = GetOperatorExpression(fieldName, filterOperator);
        return $"{collectionName}.Any({operatorExpression})";
    }

    private static string GetOperatorExpression(string fieldName, FilterOperator filterOperator)
    {
        return filterOperator switch
        {
            FilterOperator.Equals => $"{fieldName} == @value",
            FilterOperator.DoesntEqual => $"{fieldName} != @value",
            FilterOperator.GreaterThan => $"{fieldName} > @value",
            FilterOperator.GreaterThanOrEqual => $"{fieldName} >= @value",
            FilterOperator.LessThan => $"{fieldName} < @value",
            FilterOperator.LessThanOrEqual => $"{fieldName} <= @value",
            FilterOperator.Contains => $"{fieldName}.Contains(@value)",
            FilterOperator.ContainsIgnoreCase => $"{fieldName}.ToLower().Contains(@value.ToLower())",
            FilterOperator.NotContains => $"!{fieldName}.Contains(@value)",
            FilterOperator.StartsWith => $"{fieldName}.StartsWith(@value)",
            FilterOperator.EndsWith => $"{fieldName}.EndsWith(@value)",
            FilterOperator.IsEmpty => $"string.IsNullOrEmpty({fieldName})",
            FilterOperator.IsNotEmpty => $"!string.IsNullOrEmpty({fieldName})",
            _ => $"{fieldName}.Contains(@value)"
        };
    }

}
