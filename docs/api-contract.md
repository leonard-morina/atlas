# API contract

What mobile builds against. The machine-readable version is [openapi-v1.json](openapi-v1.json) (exported from the
service, paths as mobile calls them). Locally the API is at `http://localhost:5070`, Swagger UI of the service at
`http://localhost:5162/swagger`. The note to the mobile team is at the end.

## Endpoints

| | |
|---|---|
| `POST /applications` | Submit an application. Answers with the decision when it's there within 10 seconds, otherwise with where it stands. |
| `GET /applications/{applicationId}` | Where an application stands. Poll this after a `202`. |

Both are anonymous for now (open question). Paths have no version, the gateway maps them to v1.

## POST /applications

Headers:

- `Idempotency-Key` (required): a UUID the app creates **once per application** and sends again on every retry of it.
  A retry with the same key and the same body gets the original answer back, never a second application.
- `Content-Type: application/json`

Body:

```json
{
  "firstName": "Ana",
  "lastName": "Petrova",
  "dateOfBirth": "1991-03-04",
  "country": "MB",
  "nationality": "MKD",
  "identifier": { "type": "NATIONAL_ID", "value": "0403991450014" },
  "email": "ana@example.com",
  "phone": "+38970123456",
  "documents": [
    { "type": "PASSPORT", "image": "<base64>" },
    { "type": "SELFIE",   "image": "<base64>" }
  ],
  "termsAccepted": true
}
```

| Field | Rules |
|---|---|
| `firstName`, `lastName` | required, max 100 characters |
| `dateOfBirth` | `YYYY-MM-DD`, in the past. If the identifier encodes a birth date, it must match |
| `country` | market of residence: `MA` … `MF` |
| `nationality` | ISO 3166-1 alpha-3, e.g. `MKD`. Not the same as `country`, screening needs the nationality |
| `identifier.type` | `NATIONAL_ID`, or `PASSPORT` (MF only, for residents without a Personal Number) |
| `identifier.value` | as issued, leading zeros matter. Format and check digit per market (see below) |
| `identifier.issuingCountry` | passports only, ISO alpha-3 (a passport number alone doesn't identify a person) |
| `email` | required, an email address |
| `phone` | international format, e.g. `+38970123456` |
| `documents` | exactly one `PASSPORT` or `ID_CARD`, and one `SELFIE`. Base64, at most 10 MB each |
| `termsAccepted` | must be `true` |

National identifiers per market: MA, MB, MF: 13 digits (Personal Number); MC: 8 digits + a check letter; MD: 10
digits; ME: 10 digits. The whole request may be at most 28 MB.

### Answers

| Status | When | Body |
|---|---|---|
| `201 Created` | decided within the wait | `{ "applicationId", "status" }`, status is a decision (below) |
| `202 Accepted` | not decided yet, or referred to a person | `{ "applicationId", "status" }`, status `PROCESSING` or `REFERRED`. `Location: /applications/{id}` |
| `400 Bad Request` | the request can't be read: broken JSON, an unknown value, missing or invalid `Idempotency-Key` | problem details, `errors` per field where we know it |
| `409 Conflict` | this person already has an application in progress in this market | problem details |
| `413 Payload Too Large` | body over 28 MB | (from the gateway) |
| `422 Unprocessable Entity` | readable but invalid, or the `Idempotency-Key` was used for a different application | problem details, `errors` per field |
| `429 Too Many Requests` | more than 10 submissions a minute from one address | problem details, `Retry-After` header (seconds) |

A replayed answer (same key, same body) has the header `Idempotent-Replayed: true`.

Examples (real responses):

```json
201  { "applicationId": "6c55b9bf-2abe-42d7-b289-4307de8c88cc", "status": "APPROVED" }

422  { "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
       "title": "One or more validation errors occurred.", "status": 422,
       "errors": { "identifier.value": ["Check digit does not match."],
                   "documents": ["Exactly one identity document (PASSPORT or ID_CARD) and one SELFIE are required."],
                   "termsAccepted": ["Terms must be accepted."] } }

400  { "title": "The request body could not be read.", "status": 400,
       "detail": "The value of 'identifier.type' could not be read.",
       "errors": { "identifier.type": ["Must be one of: NATIONAL_ID, PASSPORT."] } }
```

## GET /applications/{applicationId}

```json
200  { "applicationId": "6c55b9bf-…", "status": "APPROVED",
       "submittedAt": "2026-10-05T08:44:42.52+00:00", "decidedAt": "2026-10-05T08:44:43.02+00:00" }
404  problem details, no such application
```

It only ever tells the status, never personal data and never why. Telling a customer they matched a sanctions list
would be tipping off.

## Statuses

| Status | Meaning | Final? | What the app does |
|---|---|---|---|
| `PROCESSING` | checks still running (a slow provider) | no | poll the GET, every few seconds |
| `APPROVED` | approved, the account is being opened | no, becomes `ACCOUNT_OPENED` | show approved, poll for the account |
| `ACCOUNT_OPENED` | approved and the current account is open | yes | done |
| `REJECTED` | not approved | yes | the customer may apply again |
| `REFERRED` | a compliance officer must decide, up to 48 hours | no | tell the customer it's being reviewed, check back later. Never say why |
| `AWAITING_BRANCH_VISIT` | approved, but in MD the customer must sign in a branch first | yes, for the app | tell them to visit a branch |

## Note to the mobile team

Hi all,

I know you were told the API from the ticket wouldn't change. It changed a bit, sorry, and here's why, it's all
things the ticket couldn't know about. The shape is still one POST with everything in it and one answer back for most
customers.

**What changed in the request**

1. `nationalId` is now `identifier: { type, value, issuingCountry }`. In MF a lot of residents (refugees mostly)
   don't have a national ID at all and onboard with a passport, and a passport number only identifies someone together
   with the issuing country. Every market also has its own format, the rules are above.
2. New `nationality` field. `country` is where the customer lives, but sanctions screening needs their nationality,
   and those are often not the same.
3. New `Idempotency-Key` header, a UUID per application. Please make one when the user taps submit, store it with the
   application on the phone, and send the same one on every retry. Then a retry after a timeout or a dropped connection
   (the "train in a tunnel" case) gets the original answer back instead of a second application, or a `409`.

**What changed in the answer**

4. It's not always `201` with `APPROVED`/`REJECTED`. Most customers get that within a second. But:
   - if a provider is slow we answer `202 PROCESSING` after 10 seconds, the app polls `GET /applications/{id}`;
   - on a possible sanctions/PEP match it's `202 REFERRED`. A compliance officer has to review it, by regulation,
     and that can take up to 48 hours. We can't say why it's being reviewed (that's tipping off);
   - in MD it's `AWAITING_BRANCH_VISIT`: the regulator wants a signature in person before the account works;
   - after `APPROVED` the account opening takes a bit (core banking needs 20–90 seconds, and is closed 22:00–06:00
     local time), the status turns to `ACCOUNT_OPENED` when it's done.

   So we do need polling for those cases, I know you didn't want that. The alternative for the long cases would be a
   push notification, which I'd like to talk about (it's in my open questions). There's no card ordering yet, there's
   no card system to talk to.
5. Errors are problem details: `400` if we can't read the request, `422` with errors per field if it's not valid,
   `409` if the person already applied. Plus `413` over 28 MB and `429` with `Retry-After` when there are too many
   submissions from one address.

**Things I'd like to agree with you**

- Images: at most 10 MB each, the whole request 28 MB. Is that enough for your camera settings?
- Save and resume: what we support now is retrying the same submission. If someone stops halfway through the steps,
  the images sit on the phone until they submit. Is that encrypted on your side? If not, I'd rather we upload the
  documents first and submit after (a small v2 of this endpoint), I don't want passport scans lying around unencrypted
  for days.
- The `202` cases: how you want to show "we're still checking" and "we're reviewing it", and whether push
  notifications are an option.

From here, changes to v1 only add things (new optional fields, new statuses you can treat as "check back later"),
anything breaking would be a v2 next to it.

Thanks, and shout if anything's unclear!
