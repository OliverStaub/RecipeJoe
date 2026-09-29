# RecipeJoe

A personal, single-user recipe manager: import recipes from the web, browse them, and read them while cooking.

## Language

**Recipe**:
A single dish's record: title, servings, timing, an ordered list of Ingredient Lines, an ordered list of Steps, an optional image, and (if imported) a Source.
_Avoid_: Dish (a Recipe describes how to make a dish, it isn't the dish itself)

**Ingredient Line**:
One line of a Recipe's ingredient list, as authored (e.g. "2 cups flour, sifted") — free text, not decomposed into quantity/unit/food fields in V1.
_Avoid_: Ingredient (implies a standalone structured entity, which V1 doesn't have)

**Step**:
One instruction in a Recipe's ordered cooking procedure.
_Avoid_: Instruction (reserve for the Recipe's instruction list as a whole, if ever named)

**Servings**:
How much a Recipe makes, as authored — free text (e.g. "4 Portionen", "12 cookies", "2 potpies (8 servings each)"). Not a number in V1.
_Avoid_: Yield (schema.org's term; Servings is what a cook actually thinks in)

**Source**:
The URL a Recipe was Imported from. Recipes entered by hand have no Source.
_Avoid_: Origin, URL

**Import**:
Fetching a page at a URL, parsing its embedded schema.org/Recipe structured data, and creating a Recipe from it. Only understands standard structured data — never bespoke per-site scraping.
_Avoid_: Scrape, Ingest

**Recipe Draft**:
A Recipe that is not yet in the Library, e.g. what an Import produces. Saving it adds it to the Library. After a redirect, its Source is the page the Recipe was actually found on.
_Avoid_: Parsed recipe, Candidate

**Library**:
The full set of a user's saved Recipes — what the searchable list screen shows.
_Avoid_: Collection, Catalog

**Cook View**:
The Recipe detail screen, laid out for reading a Recipe step-by-step while actively cooking.
_Avoid_: Detail page, Reader view
