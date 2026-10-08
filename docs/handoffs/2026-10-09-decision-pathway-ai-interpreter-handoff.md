# OMM Decision Pathway AI Interpreter Handoff

Date: 2026-10-09
Repository: `C:\Users\User\source\repos\OMMv2`
Application: OMM.Public / OMM.Shared

## User goal

Build a member-facing decision-support pathway. A member types a free-form concern such as:

> US FED increases interest rates by 0.25 points. What could happen to Maybank?

The AI should classify the message and suggest the next clarification question. It must not make the financial calculation or silently issue a buy/sell recommendation.

## Current working-tree state

Uncommitted changes are intentional and have not been committed or pushed:

- `OMM.Public/Components/Pages/DecisionPathway.razor`
- `OMM.Public/Components/Pages/DecisionPathway.razor.css`
- `OMM.Public/Program.cs`
- `OMM.Public/appsettings.json`
- `OMM.Admin/Program.cs`
- `OMM.Admin/appsettings.json`
- `OMM.Shared/DecisionSupport/InquiryIntent.cs`
- `OMM.Shared/DecisionSupport/GoogleAiOptions.cs`
- `OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs`
- `OMM.Shared/DecisionSupport/DecisionSupportServiceCollectionExtensions.cs`

Do not discard unrelated working-tree changes. Do not commit or push without user approval.

## What has been implemented

### UI

`/decision-pathway` has a member question textbox near the top of the page:

- textarea id: `member-question`
- bound field: `memberQuestion`
- button: `Understand my question`
- handler: `InterpretQuestionAsync`
- injected service: `IInquiryIntentInterpreter IntentInterpreter`

The result is rendered on the same page with signal type, mentioned stock, intent, confidence, and the first missing question. If AI is unavailable, guided questions remain available.

### Shared AI boundary

`IInquiryIntentInterpreter` returns `InquiryIntentResult`:

- `IsAvailable`
- `Summary`
- `SignalType`
- `EventName`
- `MentionedSecurity`
- `MentionedSector`
- `MentionedCountry`
- `PossibleScopes`
- `Intent`
- `MissingQuestions`
- `Confidence`
- safe `ErrorMessage`

`GoogleAiIntentInterpreter` calls the Gemini REST `generateContent` endpoint server-side. It sends only the member’s free-text question. It does not send user ID, email, holdings, transactions, or portfolio values.

### Model fallback

Models are attempted in configured order. The current committed appsettings sample is:

```json
"GoogleAi": {
  "Enabled": false,
  "ApiKey": "",
  "Models": [
    "gemma-4-31b-it",
    "gemini-flash-latest",
    "gemini-2.5-flash-lite"
  ],
  "Model": "",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta",
  "TimeoutSeconds": 20
}
```

`Model` is a legacy single-model fallback if `Models` is empty.

The user supplied a `GET https://generativelanguage.googleapis.com/v1beta/models` response showing these relevant model IDs with `generateContent`:

- `gemma-4-31b-it`
- `gemini-flash-latest`
- `gemini-flash-lite-latest`
- `gemini-2.5-flash`
- `gemini-2.5-flash-lite`
- `gemini-3.5-flash`
- `gemini-3.5-flash-lite`

The user proposed Gemma 4 31B as primary and Gemini Flash as fallback. A reasonable tested order from the supplied list is:

```text
gemma-4-31b-it
gemini-flash-lite-latest
gemini-2.5-flash
```

## User configuration

API key should remain in User Secrets or deployment environment variables. Model names may remain in committed appsettings.

For local Public development:

```powershell
dotnet user-secrets set "GoogleAi:Enabled" "true" --project OMM.Public
dotnet user-secrets set "GoogleAi:ApiKey" "REDACTED" --project OMM.Public
```

If overriding the model list through User Secrets:

```powershell
dotnet user-secrets set "GoogleAi:Models:0" "gemma-4-31b-it" --project OMM.Public
dotnet user-secrets set "GoogleAi:Models:1" "gemini-flash-lite-latest" --project OMM.Public
dotnet user-secrets set "GoogleAi:Models:2" "gemini-2.5-flash" --project OMM.Public
```

Do not print or paste the actual API key into logs, chat, source files, or this handoff.

## Observed failure

The member saw:

```text
All configured AI models are temporarily unavailable. You can continue with guided questions.
```

The service was changed to append safe failure summaries, for example:

```text
gemma-4-31b-it: HTTP 400; gemini-flash-lite-latest: HTTP 429; gemini-2.5-flash: HTTP 403
```

However, the running application must be restarted/rebuilt before the newer diagnostic text can appear. An existing `OMM.Public` process previously held build output files (`OMM.Public.exe`/DLL), causing normal build attempts to fail with MSB3027/MSB3021. A prior isolated `OMM.Public` build succeeded before the latest diagnostics change; the latest full Public build was not confirmed because of the running-process/build-server lock.

## Most likely technical issue to investigate

The fallback loop currently treats all provider failures uniformly. The likely next step is to capture and inspect the provider response body safely for HTTP 400/403/404/429, without logging the API key.

Potential causes:

1. API key is not loaded by the running process, is restricted incorrectly, or lacks access: 401/403.
2. Model access or model ID issue: 404.
3. Free-tier/project quota or rate limit: 429.
4. Gemini request schema rejected: 400.
5. Current process is running old compiled code.
6. `responseSchema`/structured-output shape is not accepted by one or more available models.

## Important code review points

Inspect `OMM.Shared/DecisionSupport/GoogleAiIntentInterpreter.cs` carefully:

- It uses `x-goog-api-key` on the server-side request.
- It uses `POST {BaseUrl}/models/{model}:generateContent`.
- It requests `application/json` and supplies a schema.
- It catches provider failures and tries the next model.
- It currently does not expose the provider response body to the UI.

The request schema uses nullable JSON Schema types such as `"type": ["string", "null"]`. If a provider rejects this form, simplify the schema or use strings with empty values. Keep the response DTO validation deterministic.

Also inspect whether Gemma and Gemini models accept the same `systemInstruction`, `generationConfig.responseMimeType`, and `generationConfig.responseSchema` shape through the selected API version.

## Safe diagnostic plan

1. Stop the running `OMM.Public` process before rebuilding. Do not kill unrelated processes.
2. Set a temporary local logging level for `OMM.Shared.DecisionSupport.GoogleAiIntentInterpreter` if needed.
3. Log only:
   - model ID
   - HTTP status code
   - response content type
   - sanitized provider error message, with credentials removed
4. Do not log request headers or API key.
5. Test one simple plain-text `generateContent` request per model.
6. Test the structured JSON request separately.
7. Compare which model accepts plain text versus structured JSON.
8. Rebuild `OMM.Public` after stopping the app.
9. Test the page with:
   - `US FED increases interest rates by 0.25 points`
   - `What happens to Maybank after the dividend?`
   - `Should I sell my Maybank stock?`

## Acceptance criteria

- The textbox is visible at `/decision-pathway`.
- Clicking the button calls the server-side interpreter.
- A successful model response displays a structured interpretation.
- If model 1 fails, model 2 is attempted; if model 2 fails, model 3 is attempted.
- The UI identifies the safe reason for complete failure without exposing secrets.
- API key never appears in browser code, rendered HTML, logs, Git diff, or persisted member data.
- AI output only classifies and clarifies; OMM calculations and decision-path transitions remain deterministic.
- `dotnet build OMM.Public/OMM.Public.csproj --no-restore` succeeds after the running app is stopped.
- Do not run `dotnet test` unless the user explicitly authorizes it, per repository `AGENTS.md`.

## Repository constraints

- Shared/cross-cutting services belong in `OMM.Shared`; this is why the interpreter is there.
- Do not invent portfolio or market data. PostgreSQL/member data is authoritative.
- Do not silently aggregate mixed currencies.
- Preserve scaffolded Identity components.
- Do not commit or push without user approval.
- `git diff --stat` omits untracked files; use `git status --short` too.
