# Step titles and Servings parsing

Type: grilling
Status: resolved
Blocked by: 01

## Question

Two parser rules: (1) HowToStep `name` (sometimes a real title like "Cook the potato mixture", sometimes generic like "Schritt 1"): drop it, prefix it to the Step, or keep it as a Step title? (2) `recipeYield`: keep Servings as free text only, or also parse an integer? See [research](../../../research/schema-org-recipe-variants.md) §5.

## Answer

- **HowToStep `name`**: always dropped. A Step is `text`; `name` is used only when `text` is missing. There's no Step title.
- **Servings**: free text only, with no parsed integer in V1. Selection: number → its string; string → as-is; array → the longest element (`["4","4 Portionen"]` → "4 Portionen", `["10"]` → "10"); `QuantitativeValue` → `value` (+ ` unitText`); empty/missing → null (no badge).
- **Cook View Servings**: shown as authored in the Badge, with a lucide `Users` icon. Nothing is appended (bare "28" stays "28").
- **Newlines in a Step**: kept inside one Step. `\r\n` → `\n`, 3+ newlines collapse to 2, then trim. Rendered with `whitespace-pre-line`. A single-blob instruction string is still split on newlines into Steps.
- **Glossary**: the Servings definition is sharpened (free text, as authored, not a number).
