using Booster.Auth.Database.Models.Enums;

namespace Booster.Auth.Idp.Models;

public sealed record IdentifierField
{
    public required string LabelKey { get; init; }

    public required string HintKey { get; init; }

    public required string PlaceholderKey { get; init; }

    public required string InputType { get; init; }

    public required string AutoComplete { get; init; }

    public string? InputMode { get; init; }

    public static IdentifierField From(IReadOnlyList<IdentifierType> allowed)
    {
        var email = allowed.Contains(IdentifierType.Email);
        var userName = allowed.Contains(IdentifierType.Username);
        var phone = allowed.Contains(IdentifierType.Phone);

        if (!email && !userName && !phone)
            email = true;

        return (email, userName, phone) switch
        {
            (true, false, false) => new IdentifierField
            {
                LabelKey = "Identifier.Label.Email",
                HintKey = "Identifier.Hint.Email",
                PlaceholderKey = "Identifier.Placeholder.Email",
                InputType = "email",
                AutoComplete = "email"
            },
            (false, true, false) => new IdentifierField
            {
                LabelKey = "Identifier.Label.Username",
                HintKey = "Identifier.Hint.Username",
                PlaceholderKey = "Identifier.Placeholder.Username",
                InputType = "text",
                AutoComplete = "username"
            },
            (false, false, true) => new IdentifierField
            {
                LabelKey = "Identifier.Label.Phone",
                HintKey = "Identifier.Hint.Phone",
                PlaceholderKey = "Identifier.Placeholder.Phone",
                InputType = "tel",
                AutoComplete = "tel",
                InputMode = "numeric"
            },
            (true, true, false) => Mixed("Identifier.Label.EmailOrUsername", "Identifier.Placeholder.Email"),
            (true, false, true) => Mixed("Identifier.Label.EmailOrPhone", "Identifier.Placeholder.Email"),
            (false, true, true) => Mixed("Identifier.Label.UsernameOrPhone", "Identifier.Placeholder.Username"),
            _ => Mixed("Identifier.Label.Any", "Identifier.Placeholder.Email")
        };
    }

    private static IdentifierField Mixed(string labelKey, string placeholderKey) => new()
    {
        LabelKey = labelKey,
        HintKey = "Identifier.Hint.Mixed",
        PlaceholderKey = placeholderKey,
        InputType = "text",
        AutoComplete = "username"
    };
}
