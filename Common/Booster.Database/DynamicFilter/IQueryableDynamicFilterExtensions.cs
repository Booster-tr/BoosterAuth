using Booster.Database.DynamicFilter.Models;
using Booster.Database.Helpers;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Text;

namespace Booster.Database.DynamicFilter;

public static class IQueryableDynamicFilterExtensions
{
    private static readonly IDictionary<FilterOperator, string> _operators = new Dictionary<FilterOperator, string>
    {
        { FilterOperator.Equals, "=" },
        { FilterOperator.DoesntEqual, "!=" },
        { FilterOperator.LessThan, "<" },
        { FilterOperator.LessThanOrEqual, "<=" },
        { FilterOperator.GreaterThan, ">" },
        { FilterOperator.GreaterThanOrEqual, ">=" },
        { FilterOperator.IsEmpty, "== null" },
        { FilterOperator.IsNotEmpty, "!= null" },
        { FilterOperator.StartsWith, "StartsWith" },
        { FilterOperator.EndsWith, "EndsWith" },
        { FilterOperator.Contains, "Contains" },
        { FilterOperator.ContainsIgnoreCase, "Contains" },
        { FilterOperator.NotContains, "Contains" }
    };

    public static Filter GetNewWay(Filter filter, Logic logic)
    {
        if (filter.Filters == null)
            filter.Filters = new List<Filter>();

        var newWay = new Filter { Field = string.Empty, Logic = logic, Operator = FilterOperator.Equals, Value = "1", Filters = new List<Filter>() };
        filter.Filters.Add(newWay);
        return newWay;
    }

    public static IQueryable<T> ToDynamic<T>(this IQueryable<T> query, DynamicQuery? dynamicQuery)
    {
        if (dynamicQuery is null)
            return query;
        if (dynamicQuery.Filter is not null)
            query = Filter(query, dynamicQuery.Filter);
        if (dynamicQuery.Sort is not null)
            query = Sort(query, dynamicQuery.Sort);
        return query;
    }

    private static IQueryable<T> Filter<T>(IQueryable<T> queryable, Filter filter)
    {
        IList<Filter> filters = GetAllFilters(filter);
        string?[] values = [.. filters.Select(f => f.Value)];
        string where = Transform(filter, filters, typeof(T));
        if (!string.IsNullOrEmpty(where) && values != null)
            queryable = queryable.Where(where, values);

        return queryable;
    }

    private static IQueryable<T> Sort<T>(IQueryable<T> queryable, Sort sort)
    {
        if (string.IsNullOrEmpty(sort.Field))
            throw new ArgumentException("Invalid Field");

        if (sort is not null)
        {
            return queryable.OrderBy($"{sort.Field} {sort.OrderOperator.GetDescription()}");
        }

        return queryable;
    }

    public static IList<Filter> GetAllFilters(Filter? filter)
    {
        if (filter == null)
        {
            return new List<Filter>();
        }
        List<Filter> filters = [];
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

    public static string Transform(Filter filter, IList<Filter> filters, Type? entityType = null)
    {
        int index = filters.IndexOf(filter);
        StringBuilder where = new();

        if (filter.Operator == FilterOperator.Raw && !string.IsNullOrEmpty(filter.Field))
        {
            return filter.Field.Replace("@value", $"@{index}");
        }

        // Try to build collection expression if entityType is provided and field contains a dot
        if (entityType != null && !string.IsNullOrEmpty(filter.Field) && filter.Field.Contains('.'))
        {
            var collectionExpr = TryBuildCollectionExpression(entityType, filter.Field, filter.Operator, index);
            if (collectionExpr != null)
            {
                if (filter.Logic is not null && filter.Filters is not null && filter.Filters.Any())
                {
                    return $"{collectionExpr} {filter.Logic} ({string.Join($" {filter.Logic.Value.GetDescription()} ", filter.Filters.Select(f => Transform(f, filters, entityType)))})";
                }
                return collectionExpr;
            }
        }

        string comparison = _operators[filter.Operator];

        if (!string.IsNullOrEmpty(filter.Value))
        {
            if (filter.Operator == FilterOperator.NotContains)
                where.Append($"(!np({filter.Field}).Contains(@{index}))");
            else if (filter.Operator == FilterOperator.ContainsIgnoreCase)
                where.Append($"(np({filter.Field}).ToLower().Contains(@{index}))");
            else if (comparison is "StartsWith" or "EndsWith" or "Contains")
                where.Append($"(np({filter.Field}).{comparison}(@{index}))");
            else
                where.Append($"np({filter.Field}) {comparison} @{index}");
        }
        else if (filter.Operator is FilterOperator.IsEmpty or FilterOperator.IsNotEmpty)
        {
            where.Append($"np({filter.Field}) {comparison}");
        }

        if (filter.Logic is not null && filter.Filters is not null && filter.Filters.Any())
        {
            if (string.IsNullOrEmpty(filter.Field))
            {
                return $"({string.Join($" {filter.Logic.Value.GetDescription()} ", filter.Filters.Select(f => Transform(f, filters, entityType)))})";
            }

            return $"{where} {filter.Logic} ({string.Join($" {filter.Logic.Value.GetDescription()} ", filter.Filters.Select(f => Transform(f, filters, entityType)))})";
        }

        return where.ToString();
    }

    private static string? TryBuildCollectionExpression(Type entityType, string field, FilterOperator op, int index)
    {
        var parts = field.Split('.');
        var currentType = entityType;
        int collectionIndex = -1;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var prop = currentType.GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return null;

            if (IsCollectionType(prop.PropertyType))
            {
                collectionIndex = i;
                var collectionPath = string.Join(".", parts.Take(collectionIndex + 1));
                var innerField = string.Join(".", parts.Skip(collectionIndex + 1));
                return BuildAnyExpression(collectionPath, innerField, op, index);
            }

            currentType = prop.PropertyType;
        }

        return null;
    }

    private static bool IsCollectionType(Type type)
    {
        if (type == typeof(string))
            return false;

        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private static string BuildAnyExpression(string collectionPath, string innerField, FilterOperator op, int index)
    {
        var innerExpr = op switch
        {
            FilterOperator.Equals => $"{innerField} == @{index}",
            FilterOperator.DoesntEqual => $"{innerField} != @{index}",
            FilterOperator.GreaterThan => $"{innerField} > @{index}",
            FilterOperator.GreaterThanOrEqual => $"{innerField} >= @{index}",
            FilterOperator.LessThan => $"{innerField} < @{index}",
            FilterOperator.LessThanOrEqual => $"{innerField} <= @{index}",
            FilterOperator.Contains => $"{innerField}.Contains(@{index})",
            FilterOperator.ContainsIgnoreCase => $"{innerField}.ToLower().Contains(@{index})",
            FilterOperator.StartsWith => $"{innerField}.StartsWith(@{index})",
            FilterOperator.EndsWith => $"{innerField}.EndsWith(@{index})",
            FilterOperator.IsEmpty => $"{innerField} == null",
            FilterOperator.IsNotEmpty => $"{innerField} != null",
            _ => $"{innerField}.Contains(@{index})"
        };

        if (op == FilterOperator.NotContains)
            return $"(!{collectionPath}.Any({innerField}.Contains(@{index})))";

        return $"{collectionPath}.Any({innerExpr})";
    }

}
