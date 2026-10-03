namespace Booster.Auth.Common.Internal.Constants.Messages;

public class AuthZMessageKeys
{
    public const string PoolStoreNotFound = "authz.pool_store_not_found";
    public const string PoolStoreAlreadyExists = "authz.pool_store_already_exists";
    public const string SchemaNotFound = "authz.schema_not_found";
    public const string ResourceTypeNotFound = "authz.resource_type_not_found";
    public const string InvalidObjectFormat = "authz.invalid_object_format";
    public const string InvalidUserFormat = "authz.invalid_user_format";
    public const string InvalidCorrelationId = "authz.invalid_correlation_id";
    public const string DuplicateCorrelationId = "authz.duplicate_correlation_id";
    public const string SchemaConflict = "authz.schema_conflict";
    public const string SchemaHasIssues = "authz.schema_has_issues";
    public const string SchemaRejected = "authz.schema_rejected";
    public const string SchemaVersionNotFound = "authz.schema_version_not_found";
    public const string SchemaVersionActiveReadOnly = "authz.schema_version_active_read_only";
    public const string SchemaVersionActiveCannotDelete = "authz.schema_version_active_cannot_delete";
    public const string SchemaVersionNameTaken = "authz.schema_version_name_taken";
    public const string SchemaLimitExceeded = "authz.schema_limit_exceeded";
    public const string SchemaVersionLimitReached = "authz.schema_version_limit_reached";
    public const string PoolRelationNotAllowed = "authz.pool_relation_not_allowed";
    public const string PoolRelationRejected = "authz.pool_relation_rejected";
    public const string PoolRelationNotFound = "authz.pool_relation_not_found";
}
