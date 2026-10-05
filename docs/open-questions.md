# Open questions

None of these block anything: for each one I picked something and built it. These are what I'd take to the people
involved on day one.

## Questions that could change the design

1. **Data residency (Compliance 1).** Personal data "must not leave the customer's country of residence", but Platform
   runs one region and one SQL instance. Is the region enough, or does each market need its own storage? *Built:* one
   deployment, images stored under the market's name so they can be split. The answer can change the architecture, so
   it's first (design note).
2. **The referral loop.** Compliance 3 requires a person to review a possible match, the ticket says no human review.
   How does the officer's decision get back to us? *Built:* `REFERRED`, waiting (sketch in decisions.md).
3. **The three minutes and the card (AC2, AC6).** They can't hold for referrals (up to 48 h), at night (core banking is
   closed 22:00–06:00) or in MD (branch visit), and there's no card system. Is "most customers in seconds, the rest told
   what's happening" OK for the board demo?
4. **Access logging and retention (§2, §4, §5).** Which accesses must be logged and where? And for a rejected applicant
   who asks to be deleted, does the ten-year retention win over erasure?

## Assumptions I made, please confirm

- Our share of core banking's 5 calls per market: **2**, configurable.
- Market time zones for the night window: **MA, MB Belgrade; MC, MD Bucharest; ME, MF Istanbul**.
- After an unanswered OpenAccount: **5 lookups a minute apart**, then operations.
- When account opening gives up, the customer **stays `APPROVED`** and operations get an event.
- Someone who already has an account from a branch **gets a second one** (core banking doesn't check).
- A rejected customer may **apply again straight away**. No minimum age check.
- ME identifiers: **format only**, the check digit algorithm isn't published. Three-digit years: **9xx = 19xx, 0xx = 20xx**.
- World-Check gets the applicant's **nationality** (ISO alpha-3), not the market code the ticket's example sends.

## Mistakes in the material

- The second MA/MB test vector `2307980312076` is listed with birth date 1998-07-23 but encodes 23-07-1980.
- The ticket's example `nationalId` `0403991450016` fails its own check digit (it should end in 4).
