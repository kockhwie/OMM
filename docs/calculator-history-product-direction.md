# Calculator History & Save as Mine — Product Direction

> **STATUS: FUTURE DIRECTION.** This document records the agreed product behavior.
> It is not an implementation task and must not be treated as part of Phase 3.

## Purpose

The financial calculators should be useful without registration while also providing
a convenient, explicit path for a user to turn a calculation into a tracked Mine.
This is intended to improve calculator retention and reduce the effort required to
add a Mine.

## User flow

```text
Visitor searches Maybank and calculates a dividend
        ↓
Calculation is added to recent history
        ↓
Visitor searches CIMB, Hong Leong Bank, and other stocks
        ↓
History keeps the separate calculation results
        ↓
User selects “Save as Mine”
        ↓
Registered user: save as a Mine
Guest user: invite the user to register or sign in
        ↓
After registration/sign-in, return to the selected calculation
        ↓
Create the Mine from the preserved calculation
```

## Guest behavior

- Guests may use calculators without creating an account.
- Guest history is stored locally in the browser until the user registers, signs in,
  clears it, or the browser storage is removed.
- Guest history must not be uploaded to the server without explicit user action.
- Selecting **Save as Mine** while logged out should show a clear prompt explaining
  that registration/sign-in is required to keep the Mine and track it later.
- After registration or sign-in, preserve the selected calculation and return the user
  to the save flow instead of making them repeat the calculation.

## Registered-user behavior

- Registered users may have their calculation history saved in the database.
- Every history query must be scoped to the authenticated user's ID on the server;
  never trust a user ID supplied by the browser.
- **Save as Mine** is an explicit user action. A calculation must not automatically
  become a Mine merely because it appears in history.
- The original calculation should remain as historical context after a Mine is created;
  creating a Mine must not silently overwrite the original calculation.
- Users should eventually be able to review, reuse, and delete their own history.

## Domain distinction

These are different concepts and should remain different entities/models:

```text
CalculationHistory
    What the user calculated at a particular time.

Mine
    A financial asset or holding the user has explicitly chosen to track.
```

The future conversion should be explicit and auditable:

```text
CalculationHistory → user selects Save as Mine → Mine
```

## Privacy and security requirements

- Do not store guest financial inputs server-side by default.
- Protect registered history with the same user-ownership rules as other member data.
- Validate selected stock codes against the authoritative stock data source.
- Do not put passwords, tokens, or sensitive financial values in logs.
- Provide deletion behavior for calculation history and Mines according to the product's
  retention policy.
- Explain to users what is stored when they register or select **Save as Mine**.

## UI reuse requirement

The public `/tools` view and authenticated dashboard tools view should reuse the same
calculator and stock-search components. The wrappers may differ by layout and storage
behavior, but calculation rules and form behavior must not be duplicated.

## Scheduling note

This feature should be planned after the Mine ownership model and authenticated member
storage are stable. It is not part of the current admin layout, stock CRUD, or
reference-data work.
