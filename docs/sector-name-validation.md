# Public-user searchable-text policy

## Scope
This policy protects text that public users can add or modify. It is not applied to admin-owned master data such as `Sector`, `Country`, `Market`, or `Institution`.

## Current public inputs covered
- `Mine.Name`
- `Mine.Holdings` when provided
- `Burden.Name`
- `IncomeRecord.Source`
- `Goal.Title`
- `MinerProfile.Name`

Email addresses, passwords, one-time codes, recovery codes, IDs, dates, numeric values, currency codes, and read-only master-data selections keep their own validation rules.

## Enforcement
- `OMM.Shared/Validation/SearchableTextPolicy.cs` is the central Unicode policy replacement point.
- `OMM.Public/Validation/PublicInputValidator.cs` provides public-page messages and service-boundary validation.
- Public Blazor save handlers reject invalid input and display an alert before calling the service.
- `OMM.Public/Services/MockMineService.cs` validates again before adding public records.

## Review reminder
If legitimate users need additional scripts, full-width punctuation, combining marks, or other Unicode characters, update the shared policy and review every public field listed above. Validate the original input before normalization so compatibility characters cannot be silently converted into accepted characters.
