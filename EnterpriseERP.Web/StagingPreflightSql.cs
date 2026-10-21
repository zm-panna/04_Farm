namespace EnterpriseERP.Web;

/// <summary>Auditable fixed SELECT-only queries. No DDL, DML, DBCC, transaction, stored procedure or dynamic SQL.</summary>
public static class StagingPreflightSql
{
    public const string Identity = """
        SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName, DB_NAME() AS DatabaseName,
            ORIGINAL_LOGIN() AS LoginName, USER_NAME() AS UserName,
            TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) AS MajorVersion,
            HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION') AS ViewDefinition,
            CONVERT(nvarchar(60), DATABASEPROPERTYEX(DB_NAME(), 'Updateability')) AS Updateability
        """;
    public const string Tables = """
        SELECT TOP (@limit) s.name AS SchemaName, t.name AS TableName
        FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0
        ORDER BY s.name,t.name OPTION (MAXDOP 1)
        """;
    public const string Columns = """
        SELECT TOP (@limit) s.name AS SchemaName,t.name AS TableName,c.name AS ColumnName,c.is_nullable AS IsNullable,c.is_identity AS IsIdentity,c.is_computed AS IsComputed,
            ty.name + CASE WHEN ty.name IN ('varchar','char','varbinary','binary') THEN '(' + CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length) END + ')'
                WHEN ty.name IN ('nvarchar','nchar') THEN '(' + CASE WHEN c.max_length=-1 THEN 'max' ELSE CONVERT(varchar(10),c.max_length/2) END + ')'
                WHEN ty.name IN ('decimal','numeric') THEN '(' + CONVERT(varchar(10),c.precision) + ',' + CONVERT(varchar(10),c.scale) + ')'
                WHEN ty.name IN ('datetime2','datetimeoffset','time') AND c.scale<>7 THEN '(' + CONVERT(varchar(10),c.scale) + ')' ELSE '' END AS StoreType
        FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id
        JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.is_ms_shipped=0
        ORDER BY s.name,t.name,c.column_id OPTION (MAXDOP 1)
        """;
    public const string Indexes = """
        SELECT TOP (@limit) s.name AS SchemaName,t.name AS TableName,i.name AS IndexName,i.is_primary_key AS IsPrimary,
            i.is_unique_constraint AS IsConstraint,i.is_disabled AS IsDisabled,i.filter_definition AS Filter,
            ic.key_ordinal AS Ordinal,c.name AS ColumnName
        FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.indexes i ON i.object_id=t.object_id
        JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE t.is_ms_shipped=0 AND i.is_unique=1 AND ic.key_ordinal>0
        ORDER BY s.name,t.name,i.name,ic.key_ordinal OPTION (MAXDOP 1)
        """;
    public const string ForeignKeys = """
        SELECT TOP (@limit) s.name AS SchemaName,t.name AS TableName,f.name AS ConstraintName,
            f.is_disabled AS IsDisabled,f.is_not_trusted AS IsUntrusted,f.is_not_for_replication AS NotForReplication,
            fc.constraint_column_id AS Ordinal,c.name AS ColumnName,rs.name AS TargetSchema,rt.name AS TargetTable,rc.name AS TargetColumn,
            f.delete_referential_action_desc AS DeleteAction,f.update_referential_action_desc AS UpdateAction
        FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
        JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=fc.parent_column_id
        JOIN sys.tables rt ON rt.object_id=f.referenced_object_id JOIN sys.schemas rs ON rs.schema_id=rt.schema_id
        JOIN sys.columns rc ON rc.object_id=rt.object_id AND rc.column_id=fc.referenced_column_id WHERE t.is_ms_shipped=0
        ORDER BY s.name,t.name,f.name,fc.constraint_column_id OPTION (MAXDOP 1)
        """;
    public const string Checks = """
        SELECT TOP (@limit) s.name AS SchemaName,t.name AS TableName,c.name AS ConstraintName,c.definition AS Definition,
            c.is_disabled AS IsDisabled,c.is_not_trusted AS IsUntrusted,c.is_not_for_replication AS NotForReplication
        FROM sys.check_constraints c JOIN sys.tables t ON t.object_id=c.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
        WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name,c.name OPTION (MAXDOP 1)
        """;
    public const string RowSecurity = """
        SELECT TOP (@limit) s.name AS SchemaName,t.name AS TableName
        FROM sys.security_predicates p JOIN sys.security_policies sp ON sp.object_id=p.object_id
        JOIN sys.tables t ON t.object_id=p.target_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
        WHERE sp.is_enabled=1 ORDER BY s.name,t.name OPTION (MAXDOP 1)
        """;
    public const string Migrations = """
        SELECT TOP (@limit) MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId OPTION (MAXDOP 1)
        """;
    public const string Privileges = """
        SELECT TOP (@limit) p.HasPrivilege FROM (
            SELECT IS_SRVROLEMEMBER(r.RoleName) AS HasPrivilege FROM (VALUES ('sysadmin'),('securityadmin'),('serveradmin'),('setupadmin'),('processadmin'),('diskadmin'),('dbcreator'),('bulkadmin')) r(RoleName)
            UNION ALL SELECT IS_MEMBER(r.RoleName) FROM (VALUES ('db_owner'),('db_ddladmin'),('db_securityadmin'),('db_accessadmin'),('db_backupoperator')) r(RoleName)
            UNION ALL SELECT CASE WHEN USER_NAME()='dbo' THEN 1 ELSE 0 END
            UNION ALL SELECT 1 FROM sys.fn_my_permissions(NULL,'SERVER') WHERE permission_name LIKE 'ALTER%' OR permission_name LIKE 'CONTROL%' OR permission_name LIKE 'IMPERSONATE%' OR permission_name LIKE 'CREATE%' OR permission_name IN ('TAKE OWNERSHIP','UNSAFE ASSEMBLY','EXTERNAL ACCESS ASSEMBLY')
            UNION ALL SELECT 1 FROM sys.fn_my_permissions(NULL,'DATABASE') WHERE permission_name LIKE 'ALTER%' OR permission_name LIKE 'CONTROL%' OR permission_name LIKE 'IMPERSONATE%' OR permission_name LIKE 'CREATE%' OR permission_name IN ('TAKE OWNERSHIP','BACKUP DATABASE','BACKUP LOG','CONNECT REPLICATION')
            UNION ALL SELECT HAS_PERMS_BY_NAME(QUOTENAME(s.name),'SCHEMA','ALTER') FROM sys.schemas s WHERE s.name NOT IN ('sys','INFORMATION_SCHEMA')
            UNION ALL SELECT HAS_PERMS_BY_NAME(QUOTENAME(s.name),'SCHEMA','CONTROL') FROM sys.schemas s WHERE s.name NOT IN ('sys','INFORMATION_SCHEMA')
            UNION ALL SELECT HAS_PERMS_BY_NAME(QUOTENAME(s.name)+'.'+QUOTENAME(t.name),'OBJECT','ALTER') FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0
            UNION ALL SELECT HAS_PERMS_BY_NAME(QUOTENAME(s.name)+'.'+QUOTENAME(t.name),'OBJECT','CONTROL') FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0
            UNION ALL SELECT HAS_PERMS_BY_NAME(QUOTENAME(s.name)+'.'+QUOTENAME(t.name),'OBJECT','TAKE OWNERSHIP') FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0
        ) p OPTION (MAXDOP 1)
        """;
    public const string Company = """
        SELECT TOP (@limit) CASE WHEN Slug LIKE 'demo-farm-%' OR Name LIKE 'DEMO %' THEN 1 ELSE 0 END AS DemoMarker
        FROM dbo.Companies WITH (FORCESEEK) WHERE Id=@company OPTION (MAXDOP 1)
        """;
    public const string Policies = """
        SELECT TOP (@limit) Version,CountryCode,CurrencyCode,CurrencyMinorUnits,TimeZoneId,EffectiveFrom,EffectiveTo,
            CASE WHEN ApprovedBy IS NOT NULL AND LEN(ApprovedBy)>0 AND ApprovedBy<>CreatedBy AND LEN(CreatedBy)>0 AND ApprovedUtc IS NOT NULL THEN 1 ELSE 0 END AS IndependentApproval,
            CASE WHEN LEN(LTRIM(RTRIM(ReviewBasis)))>0 THEN 1 ELSE 0 END AS HasReviewBasis,
            CASE WHEN ReviewBasis LIKE '%DEMO%' OR ReviewBasis LIKE '%FICTIONAL%' THEN 1 ELSE 0 END AS DemoMarker
        FROM dbo.CompanyBusinessPolicies WITH (FORCESEEK) WHERE CompanyId=@company AND Status='Approved' OPTION (MAXDOP 1)
        """;
    public const string CurrencyLock = """
        SELECT TOP (@limit) CurrencyCode,CurrencyMinorUnits,CASE WHEN LEN(ConfirmedBy)>0 AND ConfirmedUtc IS NOT NULL THEN 1 ELSE 0 END AS HasConfirmation
        FROM dbo.CompanyCurrencyLocks WITH (FORCESEEK) WHERE CompanyId=@company OPTION (MAXDOP 1)
        """;
    public const string AuditMarkers = """
        SELECT TOP (@limit) CASE WHEN Action IN ('DemoSeedCompletedV3','EnterpriseDemoCompletedV1','DemoSeedFingerprintV3')
            OR Reference LIKE 'FARM-DEMO-%' OR Reference LIKE 'FARM-ENTERPRISE-DEMO-%' OR ActorId='DEMO-CLI' THEN 1 ELSE 0 END AS DemoMarker
        FROM dbo.Audits WITH (FORCESEEK) WHERE CompanyId=@company OPTION (MAXDOP 1)
        """;
    public const string TaxMarkers = """
        SELECT TOP (@limit) CASE WHEN Code LIKE 'DEMO-%' OR Name LIKE '%FICTIONAL%' OR Reason LIKE '%DEMO%' OR Reason LIKE '%FICTIONAL%' THEN 1 ELSE 0 END AS DemoMarker
        FROM dbo.TaxPolicies WITH (FORCESEEK) WHERE CompanyId=@company OPTION (MAXDOP 1)
        """;
}
