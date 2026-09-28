# schema.org/Recipe variants on real sites: what the V1 Import parser must handle

Researched 2026-09-28. Method: fetched live recipe pages with `curl` from a local machine (Chrome desktop User-Agent), pulled every `<script type="application/ld+json">` block, located the `Recipe` node, and recorded the shape of each relevant property. Where a site blocked the fetch, a Wayback Machine raw snapshot (`web.archive.org/web/2026id_/…`) was used and is marked **(WB)**. The scripts are in the session scratchpad and are not committed.

## TL;DR

- **Support JSON-LD only.** 22 of 23 sites with a fetched recipe page carry the Recipe in JSON-LD. The one exception, Smitten Kitchen (Jetpack Recipe block), uses microdata and has no `recipeInstructions` at all, so microdata support would not have saved it.
- JSON-LD comes in three **container shapes**: a single object, a top-level array, or `@graph`. It is often split across **several `<script>` blocks** (lecker.de has 8), so scan all of them.
- **`@id` references must be resolved** within `@graph`. On chefkoch.de, `image` and `author` are only `{"@id": "…#primaryimage"}`.
- **Durations are mostly ISO 8601, but not always.** Condé Nast sites (bonappetit, epicurious) emit `"20 minutes"` / `"1 hour"`. `XmlConvert.ToTimeSpan` handles every ISO variant seen.
- **Text needs HTML-entity decoding and tag stripping**: `&#39;`, `&quot;`, `&nbsp;`, `<br />`, and HTML in `description`.
- **Recommendation:** hand-roll the parser on `System.Text.Json` `JsonDocument`, with AngleSharp (or HtmlAgilityPack) to extract the script blocks. Do not use Schema.NET.
- **Bot-blocking is a bigger threat to Import than parser variance.** allrecipes, seriouseats, simplyrecipes (People Inc, HTTP 402), foodnetwork (Akamai, 403), budgetbytes, sallysbakingaddiction, cookieandkate, thewoksoflife (Cloudflare, 403 challenge) all refused a plain HTTP fetch.

## 1. Survey results

| Site | Lang | Fetch | Container | `@type` | recipeInstructions | Durations | recipeYield | image |
|---|---|---|---|---|---|---|---|---|
| bbcgoodfood.com | en | 200 | object (1 of 4 blocks) | `"Recipe"` | HowToStep[] | `PT35M` | number `14` | ImageObject[] |
| bbc.co.uk/food | en | 200 | `@graph` | `"Recipe"` | **string[]** | `PT30M`, `PT1H` | `"Serves 12"` | ImageObject |
| cooking.nytimes.com | en | 200 | object (1 of 4) | `"Recipe"` | HowToStep[] | total only | `"18 5-inch cookies"` | ImageObject[] (4, `contentUrl`) |
| bonappetit.com | en | 200 | object | `"Recipe"` | HowToStep[] | **`"20 minutes"`** | `"Makes 16"` | string[] |
| epicurious.com | en | 200 | object | `"Recipe"` | HowToStep[] | **`"1 hour"`** | `"Makes 16"` | string[] |
| seriouseats.com (WB) | en | 402 live | top-level array | **`["Recipe"]`** | HowToStep[] (with step `image`) | `PT75M` | `"28"` | ImageObject |
| foodnetwork.com (WB) | en | 403 live | top-level array | `"Recipe"` | HowToStep[] | **`P0Y0M0DT0H20M0.000S`** | `"8 servings"` | ImageObject[] |
| allrecipes.com | en | 402 live and WB | n/a | n/a | n/a | n/a | n/a | n/a |
| delish.com | en | 200 | top-level array | `"Recipe"` | HowToStep[] | `PT0S` cookTime | `"4 serving(s)"` | ImageObject[] |
| tasty.co | en | 200 | object (1 of 3) | `"Recipe"` | HowToStep[] | `PT1H5M` | `"12 cookies"` | string |
| tasteofhome.com | en | 200 | object | `"Recipe"` | HowToStep[] with `name` title | **`PT01H15M`** | `"2 potpies (8 servings each)"` | ImageObject |
| kingarthurbaking.com | en | 200 | `@graph` | `"Recipe"` | **string[]** | `PT12M` | `["36","36 cookies"]` | ImageObject |
| recipetineats.com (WPRM+Yoast) | en | 200 | `@graph` | `"Recipe"` | **HowToSection[]** | **`PT240M`** | `["10"]` | string[] |
| minimalistbaker.com (WPRM+Yoast) | en | 200 | `@graph` | `"Recipe"` | HowToStep[] (`name` == `text`) | `PT60M` | `["4"]` | string[] |
| jamieoliver.com | en | 200 | top-level array | `"Recipe"` | HowToStep[] | **none** | `"4 to 6"` | ImageObject[] |
| ricardocuisine.com | en | 200 | `@graph` | `"Recipe"` | **HowToSection[]** | `PT20M` | `"4 serving(s)"` | string[] |
| smittenkitchen.com | en | 200 | **microdata only** | n/a | **absent** | `P0DT1H15M0S` (`<time datetime>`) | text | absent |
| chefkoch.de | de | 200 | `@graph` | `"Recipe"` | **HowToSection[1]** "Zubereitung" wrapping HowToStep | `PT1H0M` | `["4","4 Portionen"]` | **`{"@id":…}` ref** |
| lecker.de | de | 200 | object (8th of 8 blocks) | `"Recipe"` | HowToStep[] (`name` "Schritt 1") | `PT1H5M` | `"20 Stücke"` | ImageObject[] |
| essen-und-trinken.de | de | 200 | top-level array | `"Recipe"` | **string[]** | `PT1H15M` | number `4` | ImageObject[] (string width) |
| kitchenstories.com/de (WPRM+Yoast) | de | 200 | `@graph` | `"Recipe"` | HowToStep[] | `PT20M` | `["4","4 Portionen"]` | string[] |
| kochbar.de | de | 200 | object | `"Recipe"` | HowToStep[] (text has `\r\n`) | **none** | `"2 servings"` | string |
| oetker.de | de | 200 | object | `"Recipe"` | HowToStep[] with **`&nbsp;<br />`** | `PT85M` | `"etwa 12 Stück"` | string |
| gutekueche.at | de | 200 | top-level array | `"Recipe"` | **string[]** | `PT20M` | `"4 Portionen"` | string[] |

Every page parsed had exactly one Recipe node. Every JSON-LD block parsed with a strict JSON parser; no comments, CDATA or trailing commas were seen.

Blocked, with no fallback tried: rewe.de (403), sallysbakingaddiction, cookieandkate, budgetbytes, thewoksoflife (Cloudflare "Just a moment…"), simplyrecipes (402).

## 2. Variant-by-variant rules

### JSON-LD vs microdata vs RDFa
- Web-wide, JSON-LD is on 41% of pages and microdata on 26%. The RDFa figure (66%) is inflated because Open Graph counts as RDFa ([Web Almanac 2024, Structured data](https://almanac.httparchive.org/en/2024/structured-data)).
- Google's Recipe docs use JSON-LD in every example ([Google Search Central: Recipe](https://developers.google.com/search/docs/appearance/structured-data/recipe)).
- In this survey, 22/23 sites used JSON-LD, 1 used microdata, and 0 used RDFa. The microdata page (Jetpack block) lacks instructions and image, so it would fail anyway under the "Import fails → nothing saved" rule.
- The legacy hRecipe microformat survives on about 1.8k domains in Common Crawl ([WDC 2024-12 stats](https://webdatacommons.org/structureddata/2024-12/stats/stats.html)). Ignore it.
- **V1: JSON-LD only.** Microdata can be added later behind the same seam if it's ever needed.

### Container and `@type`
- Collect **all** `script[type="application/ld+json"]` blocks. The type attribute may be unquoted or use different case, so match it case-insensitively.
- Each block's root can be an object, an array, or an object with `@graph`. Walk everything recursively and pick the first node whose `@type` is `"Recipe"`, or an array containing `"Recipe"` (seriouseats: `["Recipe"]`).
- Also accept `WebPage.mainEntity` → Recipe. recipe-scrapers handles this case ([`_schemaorg.py`](https://github.com/hhursev/recipe-scrapers/blob/main/recipe_scrapers/_schemaorg.py)).
- Compare types after stripping a `http(s)://schema.org/` prefix.
- **`@id` resolution:** build an `@id → node` map from all blocks. When a property value is an object whose only key is `@id`, replace it with the mapped node. Required for chefkoch `image` (→ ImageObject in `@graph`) and for author on chefkoch and recipetineats.

### recipeInstructions
schema.org allows `CreativeWork | ItemList | Text` ([schema.org/Recipe](https://schema.org/Recipe)). Google allows HowToStep (recommended), HowToSection, or Text ([Google](https://developers.google.com/search/docs/appearance/structured-data/recipe)). Normalise to a flat list of Step texts:

| Input | Handling |
|---|---|
| `string` (one blob) | Not seen in this sample. Google says it will auto-split. Split on newlines; if there's only one line, it becomes one Step. |
| `string[]` | One Step each (bbc.co.uk/food, kingarthur, essen-und-trinken, gutekueche). |
| `HowToStep` | Use `text`. If `name` is present and `text` does not start with it, keep `name` as a prefix or title. WPRM duplicates `name` = `text`; lecker uses `"Schritt 1"`. `text` can be missing, in which case use `name`. |
| `HowToSection` | Flatten its `itemListElement` recursively (decided rule). `itemListElement` can be a single object instead of an array (NYT, per recipe-scrapers). Section `name` is lost: acceptable per the flattening decision. |
| `ItemList` / object with `itemListElement` | Treat like a section. |
| nested `[[…]]` | Flatten (recipe-scrapers handles this). |

Step text can contain `\n` (bonappetit/epicurious "Do Ahead:" notes) or `\r\n` (kochbar). Decide whether to keep them inside one Step. Recommendation: keep, and normalise `\r\n` → `\n`.

### Durations (prepTime / cookTime / totalTime / performTime)
- The spec says ISO 8601 ([Google](https://developers.google.com/search/docs/appearance/structured-data/recipe)). `System.Xml.XmlConvert.ToTimeSpan` was tested locally on .NET 10 and correctly parses every ISO form seen: `PT35M`, `PT240M`, `PT1H0M`, `PT01H15M`, `P0Y0M0DT0H20M0.000S`, `P0DT1H15M0S`, `PT0S`, `P1DT2H`.
- It **throws** on `"20 minutes"` and `"1 hour"` (Condé Nast), on `PT20.5M` (fractional minutes), and on lowercase `pt20m`. Fallback: upper-case the value and retry, then try a small regex for `(\d+)\s*(h|hour|hours|Std|Stunde[n]?|min|minute[s]?|Minute[n]?)`, else treat as missing.
- **Never fail an Import because of a bad duration.** All times are optional; jamieoliver and kochbar have none at all.
- Treat `PT0S` as "not given". Do not derive totalTime; lecker has cookTime == totalTime with no prepTime. recipe-scrapers also accepts `{maxValue: …}` duration objects.

### recipeYield
Allowed types are `QuantitativeValue | Text`; Google also allows Integer. Shapes seen:
- number (`14`, `4`)
- string (`"Serves 12"`, `"4 to 6"`, `"etwa 12 Stück"`, `"2 potpies (8 servings each)"`)
- array of strings, usually `[number-as-string, human string]` (`["4","4 Portionen"]`, `["36","36 cookies"]`) or a single `["10"]`

Recommendation: store it as free text. For arrays, take the longest or last element (`"4 Portionen"`), or the first if there's only one. If a numeric Servings value is ever wanted, take the first integer found.

### image
Allowed types are `ImageObject | URL`. Shapes seen:
- a string
- a string[] (often 3–4 crops of the same image)
- an ImageObject (`url`, or `contentUrl` on NYT)
- an ImageObject[]
- an `{"@id"}` ref (chefkoch)

Algorithm: resolve the `@id`, then if it's an array take the first element, then if it's an object use `url ?? contentUrl`. Resolve relative URLs against the page URL. `width`/`height` can be strings (essen-und-trinken). Absent on Smitten Kitchen. If the image is missing or the download fails, do not fail the Import (open question below).

### Text: entities and markup
Observed inside JSON string values, so already JSON-decoded yet still HTML-encoded:
- `&#39;` in name (seriouseats)
- `&quot;` in description (recipetineats/WPRM)
- `&nbsp;` in steps (lecker, oetker)
- `<br />` in steps (oetker)
- HTML with newlines in description (bbc.co.uk/food)

`WebUtility.HtmlDecode` handles these, but produces U+00A0 for `&nbsp;`, so normalise it to a space. Double-encoding like `&amp;amp;` only decodes one level, so decode twice if an `&[a-z#0-9]+;` pattern remains. Order: HTML-decode → strip tags (turn `<br>` into `\n`) → collapse whitespace → trim. Apply this to name, description, ingredient Lines and Step text.

### Ingredients
Every site used `recipeIngredient: string[]`. Keep the legacy `ingredients` property as a fallback, since it's superseded but still read by recipe-scrapers. recipe-scrapers also handles `PropertyValue` objects, so stringify any non-string. Lines contain odd spacing (`"6  Eigelbe"`, WPRM `"1  onion (, finely chopped …)"`). Collapsing whitespace is enough; Lines stay free text.

### What counts as a failed Import (suggested)
No Recipe node, or no `name`, or zero ingredients and zero steps. Everything else is optional.

## 3. .NET approach: recommendation

**Hand-roll on `System.Text.Json.JsonDocument`/`JsonElement`, with an HTML parser to pull out the script blocks.**

- **Schema.NET**: last release 13.0.0 on 2023-12-17, targeting net6/net7/netstandard2.0 (from the [NuGet registration](https://api.nuget.org/v3/registration5-gz-semver2/schema.net/index.json)). The README frames it as a way to *emit* JSON-LD, and deserialization is a contributed add-on ([README](https://github.com/RehanSaeed/Schema.NET)). It maps durations through a `TimeSpanToISO8601DurationValuesJsonConverter`, so non-ISO values like `"20 minutes"` would not come through as usable durations. It also does not resolve `@graph`/`@id`, and its 1.6 MB typed-POCO surface is heavy for about 8 fields. Not recommended.
- **json-ld.net** (a full JSON-LD processor: expand/flatten would resolve `@id`): last release 1.0.7 on 2022-03-01. It's unmaintained and overkill; a 20-line `@id` map does the job.
- **dotNetRDF** (3.5.2, 2026-06) is maintained but is an RDF toolkit. Overkill.
- **HTML extraction**: AngleSharp 1.8.2 (2026-09-18) and HtmlAgilityPack 1.13.0 (2026-08-24) are both actively maintained. AngleSharp is a spec-compliant HTML5 parser, which matters if microdata is ever added. A regex over `<script type=application/ld+json>` would also work for V1, but a parser is more robust to attribute order and quoting.
- **Prior art to mirror**: Python [recipe-scrapers](https://github.com/hhursev/recipe-scrapers) (15.12.0, 2026-08-08, actively maintained) is the de-facto reference. Its `_schemaorg.py` lists the real-world quirks and matches everything above. Port the logic, not the per-site scrapers.

Suggested module shape (deep module, one seam): `RecipeJsonLdParser.Parse(string html, Uri pageUrl) → ParsedRecipe | ImportFailure`. It is pure, with no HTTP, so it can be tested entirely from saved HTML fixtures. Fetching and image download sit behind a separate seam.

JSON options: `JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = Skip }`, and catch `JsonException` per block (skip that block, don't fail the page).

## 4. HTML test fixtures to save

Save the full HTML (`curl -L --compressed` with a browser UA) under something like `backend/tests/Fixtures/recipes/`:

| Fixture | URL | Exercises |
|---|---|---|
| chefkoch-senfsosse.html | https://www.chefkoch.de/rezepte/1356761240410766/Senfsosse-nach-Omaart.html | `@graph`, `@id` refs for image and author, single HowToSection wrapper, yield array, `PT10M` |
| recipetineats-lasagna.html | https://www.recipetineats.com/lasagna/ | WPRM+Yoast `@graph`, multiple HowToSections, `PT240M`, `&quot;`, author `@id`, 2 blocks |
| ricardo-saltimbocca.html | https://www.ricardocuisine.com/en/recipes/5696-chicken-saltimbocca-with-basil-and-capers | `@graph`, 3 HowToSections |
| lecker-schoko-topfkuchen.html | https://www.lecker.de/ganz-ohne-mehl-und-mehr-als-saftig-schoko-buttermilch-kuchen-aus-dem-topf-130389.html | Recipe in 8th of 8 blocks, `&nbsp;`, HowToStep `name` "Schritt 1", ImageObject[] |
| oetker-apfelkuchen.html | https://www.oetker.de/rezepte/r/apfelkuchen-sehr-fein | `<br />` and `&nbsp;` in steps, `performTime`, image as a string |
| essen-und-trinken-kuerbis-lasagne.html | https://www.essen-und-trinken.de/rezepte/44236-rzpt-kuerbis-lasagne | top-level array, string[] instructions, numeric yield, only totalTime |
| bbcfood-chocolate-cake.html | https://www.bbc.co.uk/food/recipes/easy_chocolate_cake_31070 | `@graph`, string[] instructions, HTML in description |
| bbcgoodfood-chocolate-cake.html | https://www.bbcgoodfood.com/recipes/easy-chocolate-cake | multiple blocks, numeric yield, ImageObject[] |
| bonappetit-cookies.html | https://www.bonappetit.com/recipe/bas-best-chocolate-chip-cookies | **non-ISO durations**, `\n` in step |
| foodnetwork-roast-chicken.html (WB) | https://web.archive.org/web/20260924003053id_/https://www.foodnetwork.com/recipes/ina-garten/perfect-roast-chicken-recipe-1940592 | `P0Y0M0DT…0.000S` durations, top-level array |
| seriouseats-cookies.html (WB) | https://web.archive.org/web/20260928131437id_/https://www.seriouseats.com/the-food-lab-best-chocolate-chip-cookie-recipe | `@type: ["Recipe"]`, `&#39;` in name, single ImageObject |
| nyt-cookies.html | https://cooking.nytimes.com/recipes/1015819-chocolate-chip-cookies | ImageObject[] with `contentUrl`, totalTime only |
| kingarthur-cookies.html | https://www.kingarthurbaking.com/recipes/classic-chocolate-chip-cookies-recipe | `@graph`, string[] instructions |
| jamieoliver-bobotie.html | https://www.jamieoliver.com/recipes/beef/bobotie/ | no durations at all, `"4 to 6"` |
| tasteofhome-potpie.html | https://www.tasteofhome.com/recipes/favorite-chicken-potpie/ | `PT01H15M`, HowToStep name as title |
| smittenkitchen-banana-bread.html | https://smittenkitchen.com/2020/03/ultimate-banana-bread/ | **negative case**: microdata only, so expect an Import failure |
| (synthetic) no-recipe.html, broken-json.html, single-string-instructions.html | n/a | failure path, malformed block skipped, blob instructions split |

Also consider a multi-recipe page (roundup / ItemList) to pin down the "take first" rule. None was captured here.

## 5. Unresolved questions

1. **Bot-blocked sites** (allrecipes and the rest of People Inc, foodnetwork, Cloudflare-protected WP blogs): accept the Import failure in V1, or add browser-like headers or headless fetch later? The block happens before parsing, so no parser choice fixes it.
2. **Image download failure or no image**: fail the Import or save without an image? Suggest: save without.
3. **HowToStep `name`** when it's a real title (tasteofhome "Cook the potato and carrot mixture", lecker "Schritt 1"): drop it, prefix it, or keep it as a step title? Suggest: drop it unless it's meaningful. It's hard to tell meaningful from generic, so the simplest option is to drop it.
4. **recipeYield**: free text only, or also a parsed integer Servings?
5. **Microdata**: explicitly out of scope for V1? Suggest yes.

## Sources
- schema.org Recipe type: https://schema.org/Recipe
- Google Search Central, Recipe structured data: https://developers.google.com/search/docs/appearance/structured-data/recipe
- HTTP Archive Web Almanac 2024, Structured data: https://almanac.httparchive.org/en/2024/structured-data
- Web Data Commons 2024-12 extraction stats: https://webdatacommons.org/structureddata/2024-12/stats/stats.html
- recipe-scrapers `_schemaorg.py` (main, fetched 2026-09-28): https://github.com/hhursev/recipe-scrapers/blob/main/recipe_scrapers/_schemaorg.py ; PyPI 15.12.0
- NuGet registrations (versions and dates): schema.net, json-ld.net, anglesharp, htmlagilitypack, dotnetrdf via https://api.nuget.org/v3/registration5-gz-semver2/<id>/index.json
- Schema.NET README: https://github.com/RehanSaeed/Schema.NET
- Site data: the live fetches and Wayback snapshots listed in §1 and §4 (2026-09-28).
