# OMM Agent Notes

## Master-data audit foreign keys

`OMM.Admin` uses admin identities in the `admin` PostgreSQL schema, while master-data tables (`Country`, `Exchange`, `Market`, `Sector`, `SubSector`, `Institution`, and `Stock`) live in the `public` schema. Do not configure `AuditableEntity.CreatedByUserId`, `ModifiedByUserId`, or `DeletedByUserId` as foreign keys to `AspNetUsers` for these shared master-data tables. Store these values as nullable text IDs.

If PostgreSQL reports an error such as:

- `23503: violates foreign key constraint FK_<Table>_AspNetUsers_<AuditField>`
- `42704: constraint FK_<Table>_AspNetUsers_<AuditField> does not exist`

apply both fixes:

1. Map the audit properties as `text` in `OMM.Admin/Data/MasterDataDbContext.cs` and remove the identity relationship from `OMM.Public/Data/ApplicationDbContext.cs`.
2. Add/apply a migration that removes the old audit foreign keys and indexes from the `public` master-data tables. Because database environments can be out of sync, use PostgreSQL `DROP CONSTRAINT IF EXISTS` and `DROP INDEX IF EXISTS` rather than EF `DropForeignKey`/`DropIndex` operations that fail when an object is already absent.

Do not work around this by setting `CreatedByUserId` to null; preserve the admin user ID as audit data.
